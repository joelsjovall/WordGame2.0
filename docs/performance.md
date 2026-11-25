# Performance baseline (F056)

These are local development measurements, not Render capacity guarantees. The provisional beta workload is 10 simultaneous Party matches and 10 Online matches (up to 60 players and roughly 60 SSE connections). This workload still needs a multiplayer load run before it can be approved for launch.

| Probe | Target | Observed |
| --- | --- | --- |
| Initial JavaScript on the homepage | Under 80 kB gzip | 100.68 → 72.22 kB after route splitting |
| Initial CSS on the homepage | Under 5 kB gzip | 6.18 → 3.15 kB after route splitting |
| `/health`, 500 requests at concurrency 20 | p95 under 100 ms, no errors | p95 39.7 ms, 0 errors |
| `/api/v1/categories/`, same load | p95 under 100 ms, no errors | p95 46.0 ms, 0 errors |
| Queue transition, 2,000 players, 1,000 updates, no SSE watchers | Median under 50 ms per 1,000 updates | 267.3 → 0.7 ms after comparing only watched players |
| 10 Online pairs entering and accepting matches concurrently | All distinct matches ready within 5 seconds | 10 matches ready in 0.70 seconds |
| 10 simultaneous Party matches, 20 lobby SSE connections, one complete round each | All complete without errors within 10 seconds | 10 complete in 3.40 seconds |
| 10 simultaneous Online matches, 20 game SSE connections, six rounds each | All complete without errors within 40 seconds | 10 complete in 20.43 seconds |
| Homepage ready, Pixel 5 browser emulation with 4× CPU throttle | Median under 2 seconds on local network | Median 1.04 seconds across five runs |

The frontend figures come from `cd frontend && npm run build`. The API figures came from a local Release backend on Windows using `node scripts/performance/http-load.mjs http://127.0.0.1:5088 /health 500 20` and the same command for `/api/v1/categories/`. The queue figures came from `dotnet run --project scripts/performance/WordGame.MatchmakingBench.csproj -c Release -- 2000 1000`; each figure is the median of three runs. The Online pairing result came from `node scripts/performance/matchmaking-load.mjs http://127.0.0.1:5089 10`. The full-match probes used `node scripts/performance/party-load.mjs http://127.0.0.1:5093 10 0 --complete` and `node scripts/performance/matchmaking-load.mjs http://127.0.0.1:5094 10 --complete`. Local rate limits were raised to 1,000 requests/minute for guest creation, matchmaking, and gameplay. Each probe creates separate guest cookies; full-match probes hold SSE connections through completion. Hardware, scheduler load, and local development configuration affect these numbers.

With 10 Party and 10 Online matches starting concurrently in one local process, the Party start probe took 3.83 seconds and Online matching took 0.75 seconds. The process working set was about 79 MB before this run and 100 MB afterward. Adding 10 further Party matches with 20 open SSE streams brought it to about 114 MB during the hold. These snapshots include retained sessions and do not establish a leak-free steady state; a longer soak test is still needed.

The mobile result used a local production Vite preview and `node frontend/scripts/measure-mobile.mjs http://127.0.0.1:5176/` with Playwright's Pixel 5 profile and 4× CPU throttling. It approximates a mid-range phone's CPU but does not represent mobile network delay or a physical device.

The queue previously copied every player record before each transition, even with no event subscribers. It now snapshots only watched players. The API's 15-second SSE heartbeats and bounded one-item watcher channels limit idle traffic and queued notifications. Category answers are loaded from local JSON at startup. Session cleanup removes expired lobbies and retained matches; active games and guest identities remain in memory.

**Remaining release measurements:** sustained CPU and memory, physical mobile network/device timing, and an approved Render free-instance capacity check. The current single-instance architecture cannot scale horizontally without shared session and event storage. Render sleep or restart interrupts active matches; add durable active state before promising uninterrupted play.
