# Performance baseline (F056)

These are local development measurements, not Render capacity guarantees. The provisional beta workload is 10 simultaneous Party matches and 10 Online matches (up to 60 players and roughly 60 SSE connections). This workload still needs a multiplayer load run before it can be approved for launch.

| Probe | Target | Observed |
| --- | --- | --- |
| Initial JavaScript on the homepage | Under 80 kB gzip | 100.68 → 72.22 kB after route splitting |
| Initial CSS on the homepage | Under 5 kB gzip | 6.18 → 3.15 kB after route splitting |
| `/health`, 500 requests at concurrency 20 | p95 under 100 ms, no errors | p95 39.7 ms, 0 errors |
| `/api/v1/categories/`, same load | p95 under 100 ms, no errors | p95 46.0 ms, 0 errors |
| Queue transition, 2,000 players, 1,000 updates, no SSE watchers | Median under 50 ms per 1,000 updates | 267.3 → 0.7 ms after comparing only watched players |

The frontend figures come from `cd frontend && npm run build`. The API figures came from a local Release backend on Windows using `node scripts/performance/http-load.mjs http://127.0.0.1:5088 /health 500 20` and the same command for `/api/v1/categories/`. The queue figures came from `dotnet run --project scripts/performance/WordGame.MatchmakingBench.csproj -c Release -- 2000 1000`; each figure is the median of three runs. Hardware, scheduler load, and local development configuration affect these numbers.

The queue previously copied every player record before each transition, even with no event subscribers. It now snapshots only watched players. The API's 15-second SSE heartbeats and bounded one-item watcher channels limit idle traffic and queued notifications. Category answers are loaded from local JSON at startup. Session cleanup removes expired lobbies and retained matches; active games and guest identities remain in memory.

**Remaining release measurements:** simultaneous full Party and Online matches, matchmaking bursts over HTTP, gameplay action latency, CPU and memory under sustained load, mobile navigation timing on a mid-range device, and an approved Render free-instance capacity check. The current single-instance architecture cannot scale horizontally without shared session and event storage. Render sleep or restart interrupts active matches; add durable active state before promising uninterrupted play.
