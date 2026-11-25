const [baseUrl, pairCountText = "10"] = process.argv.slice(2);
if (!baseUrl)
  throw new Error("Usage: node matchmaking-load.mjs <local-base-url> [pairs]");
const base = new URL(baseUrl);
if (!["127.0.0.1", "localhost"].includes(base.hostname))
  throw new Error("Local server only.");
const pairCount = Number(pairCountText);
if (!Number.isInteger(pairCount) || pairCount < 1 || pairCount > 100)
  throw new Error("Pairs must be 1–100.");
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
  return { index, cookie, playerId: identity.playerId, searchId: null };
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
  return response.status === 204 ? null : response.json();
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

async function waitForCategory(player, gameId) {
  for (let attempt = 0; attempt < 100; attempt++) {
    const state = await request(player, `/api/v1/games/${gameId}/state`);
    if (
      state.status === "active" &&
      state.round?.status === "categorySelection"
    )
      return state;
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  throw new Error(`Game ${gameId}: next round did not start`);
}

const started = performance.now();
const players = await Promise.all(
  Array.from({ length: pairCount * 2 }, (_, index) => guest(index)),
);
await Promise.all(
  players.map(async (player) => {
    const search = await request(
      player,
      "/api/v1/matchmaking/searches",
      "POST",
      { displayName: `Load ${player.index}` },
    );
    player.searchId = search.searchId;
  }),
);
const paired = await Promise.all(
  players.map(async (player) => {
    for (let attempt = 0; attempt < 50; attempt++) {
      const state = await request(player, "/api/v1/matchmaking/status");
      if (state.status === "matched") return state;
      await new Promise((resolve) => setTimeout(resolve, 100));
    }
    throw new Error(`Player ${player.index}: not matched`);
  }),
);
await Promise.all(
  players.map((player) =>
    request(
      player,
      `/api/v1/matchmaking/searches/${player.searchId}/accept`,
      "POST",
      {},
    ),
  ),
);
const games = await Promise.all(
  players.map(async (player) => {
    for (let attempt = 0; attempt < 50; attempt++) {
      const state = await request(player, "/api/v1/matchmaking/status");
      if (state.status === "ready" && state.gameId) return state.gameId;
      await new Promise((resolve) => setTimeout(resolve, 100));
    }
    throw new Error(`Player ${player.index}: match not ready`);
  }),
);
if (
  new Set(games).size !== pairCount ||
  new Set(paired.map((state) => state.matchId)).size !== pairCount
) {
  throw new Error("Matchmaking produced the wrong number of distinct matches.");
}
const readySeconds = ((performance.now() - started) / 1_000).toFixed(2);
if (complete) {
  const aborters = [];
  try {
    const groups = new Map();
    games.forEach((gameId, index) =>
      groups.set(gameId, [...(groups.get(gameId) ?? []), players[index]]),
    );
    await Promise.all(
      [...groups].map(async ([gameId, pair]) => {
        if (pair.length !== 2) throw new Error(`Game ${gameId}: wrong roster`);
        for (const player of pair) {
          const controller = new AbortController();
          aborters.push(controller);
          const stream = await fetch(
            new URL(`/api/v1/games/${gameId}/events`, base),
            {
              headers: { cookie: player.cookie },
              signal: controller.signal,
            },
          );
          if (!stream.ok)
            throw new Error(`Game ${gameId}: SSE HTTP ${stream.status}`);
        }
        for (let round = 0; round < 6; round++) {
          const state = await waitForCategory(pair[0], gameId);
          const bidder = pair.find(
            (player) => player.playerId === state.round.activePlayerId,
          );
          const caller = pair.find((player) => player !== bidder);
          if (!bidder || !caller)
            throw new Error(`Game ${gameId}: unknown turn`);
          await act(bidder, gameId, {
            type: "selectCategory",
            categoryId: "european-capitals",
          });
          await act(bidder, gameId, { type: "placeBid", wordCount: 1 });
          await act(caller, gameId, { type: "callBluff" });
          await act(bidder, gameId, { type: "submitWord", word: "Stockholm" });
        }
        const result = await request(pair[0], `/api/v1/games/${gameId}/state`);
        if (result.status !== "completed" || !result.result)
          throw new Error(`Game ${gameId}: not completed`);
      }),
    );
  } finally {
    for (const controller of aborters) controller.abort();
  }
}
console.log(
  `${pairCount} simultaneous Online matches ready in ${readySeconds} s${complete ? `; six rounds completed in ${((performance.now() - started) / 1_000).toFixed(2)} s` : ""}`,
);
