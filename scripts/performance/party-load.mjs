const [baseUrl, matchCountText = "10", holdSecondsText = "0"] =
  process.argv.slice(2);
if (!baseUrl)
  throw new Error(
    "Usage: node party-load.mjs <local-base-url> [matches] [hold-seconds]",
  );
const base = new URL(baseUrl);
if (!["127.0.0.1", "localhost"].includes(base.hostname))
  throw new Error("Local server only.");
const matchCount = Number(matchCountText);
if (!Number.isInteger(matchCount) || matchCount < 1 || matchCount > 50)
  throw new Error("Matches must be 1–50.");
const holdSeconds = Number(holdSecondsText);
if (!Number.isInteger(holdSeconds) || holdSeconds < 0 || holdSeconds > 60)
  throw new Error("Hold time must be 0–60 seconds.");
const complete = process.argv.includes("--complete");

async function guest(index) {
  const response = await fetch(new URL("/api/v1/guests", base), {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: "{}",
  });
  if (!response.ok) throw new Error(`Guest ${index}: HTTP ${response.status}`);
  const cookie = response.headers.get("set-cookie")?.split(";", 1)[0];
  if (!cookie) throw new Error(`Guest ${index}: missing cookie`);
  const identity = await response.json();
  return { index, cookie, playerId: identity.playerId };
}

async function request(player, path, method = "GET", body) {
  const response = await fetch(new URL(path, base), {
    method,
    headers: {
      cookie: player.cookie,
      ...(body ? { "content-type": "application/json" } : {}),
    },
    ...(body ? { body: JSON.stringify(body) } : {}),
    signal: AbortSignal.timeout(10_000),
  });
  if (!response.ok)
    throw new Error(`Player ${player.index} ${path}: HTTP ${response.status}`);
  return response.json();
}

async function waitForConnections(host, lobbyId) {
  for (let attempt = 0; attempt < 50; attempt++) {
    const lobby = await request(host, `/api/v1/lobbies/${lobbyId}`);
    if (
      lobby.players.length === 2 &&
      lobby.players.every((player) => player.isConnected)
    )
      return lobby;
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  throw new Error(`Lobby ${lobbyId}: players did not connect`);
}

async function act(player, gameId, action) {
  const path = `/api/v1/games/${gameId}`;
  for (let attempt = 0; attempt < 5; attempt++) {
    const state = await request(player, `${path}/state`);
    const response = await fetch(new URL(`${path}/actions`, base), {
      method: "POST",
      headers: { cookie: player.cookie, "content-type": "application/json" },
      body: JSON.stringify({
        ...action,
        actionId: crypto.randomUUID(),
        expectedVersion: state.version,
      }),
      signal: AbortSignal.timeout(10_000),
    });
    if (response.status === 409) continue;
    if (!response.ok)
      throw new Error(
        `Player ${player.index} ${action.type}: HTTP ${response.status}`,
      );
    return;
  }
  throw new Error(
    `Player ${player.index} ${action.type}: too many version conflicts`,
  );
}

const started = performance.now();
const aborters = [];
try {
  const players = await Promise.all(
    Array.from({ length: matchCount * 2 }, (_, index) => guest(index)),
  );
  const games = await Promise.all(
    Array.from({ length: matchCount }, async (_, index) => {
      const host = players[index * 2];
      const rival = players[index * 2 + 1];
      const created = await request(host, "/api/v1/lobbies", "POST", {
        requestId: crypto.randomUUID(),
        displayName: `Host ${index}`,
      });
      await request(rival, "/api/v1/lobbies/join", "POST", {
        lobbyCode: created.lobbyCode,
        displayName: `Rival ${index}`,
      });
      const path = `/api/v1/lobbies/${created.id}`;
      for (const player of [host, rival]) {
        const controller = new AbortController();
        aborters.push(controller);
        const stream = await fetch(new URL(`${path}/events`, base), {
          headers: { cookie: player.cookie },
          signal: controller.signal,
        });
        if (!stream.ok)
          throw new Error(`Lobby ${created.id}: SSE HTTP ${stream.status}`);
      }
      let lobby = await waitForConnections(host, created.id);
      if (complete) {
        lobby = await request(host, `${path}/settings`, "POST", {
          expectedVersion: lobby.version,
          settings: {
            maximumPlayers: 2,
            roundCount: 1,
            challengeSeconds: 12,
            allowedDifficulties: ["easy"],
          },
        });
      }
      lobby = await request(host, `${path}/ready`, "POST", {
        expectedVersion: lobby.version,
        isReady: true,
      });
      lobby = await request(rival, `${path}/ready`, "POST", {
        expectedVersion: lobby.version,
        isReady: true,
      });
      if (!lobby.canStart) throw new Error(`Lobby ${created.id}: cannot start`);
      lobby = await request(host, `${path}/start`, "POST", {
        expectedVersion: lobby.version,
      });
      if (!lobby.gameId || lobby.status !== "started")
        throw new Error(`Lobby ${created.id}: match did not start`);
      if (complete) {
        const state = await request(
          host,
          `/api/v1/games/${lobby.gameId}/state`,
        );
        const bidder =
          state.round.activePlayerId === host.playerId ? host : rival;
        const caller = bidder === host ? rival : host;
        await act(bidder, lobby.gameId, {
          type: "selectCategory",
          categoryId: "european-capitals",
        });
        await act(bidder, lobby.gameId, { type: "placeBid", wordCount: 1 });
        await act(caller, lobby.gameId, { type: "callBluff" });
        await act(bidder, lobby.gameId, {
          type: "submitWord",
          word: "Stockholm",
        });
        const result = await request(
          host,
          `/api/v1/games/${lobby.gameId}/state`,
        );
        if (result.status !== "completed" || !result.result)
          throw new Error(`Game ${lobby.gameId}: not completed`);
      }
      return lobby.gameId;
    }),
  );
  if (new Set(games).size !== matchCount)
    throw new Error("Party games were not distinct.");
  console.log(
    `${matchCount} simultaneous Party matches ${complete ? "completed" : "started"} with ${matchCount * 2} SSE connections in ${((performance.now() - started) / 1_000).toFixed(2)} s`,
  );
  if (holdSeconds)
    await new Promise((resolve) => setTimeout(resolve, holdSeconds * 1_000));
} finally {
  for (const controller of aborters) controller.abort();
}
