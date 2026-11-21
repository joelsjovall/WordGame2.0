# Free hosting setup (F053)

The initial no-cost setup uses one Render web service for the React frontend and ASP.NET Core API, plus one Neon PostgreSQL database for completed Online match records. One origin keeps guest cookies and SSE simple; one backend instance keeps in-memory multiplayer state consistent. The frontend builds with `VITE_API_BASE_URL` empty, so browser requests use the same HTTPS origin. Production CORS permits no cross-origin browser calls. Render terminates HTTPS; the API checks browser write origins against `RENDER_EXTERNAL_URL` (or `Public__Origin` for a custom domain). The existing `/health` endpoint supplies a liveness check.

Render's [free web service](https://render.com/docs/free) sleeps after 15 idle minutes, may restart, has no persistent disk, and can take about a minute to wake. It is suitable for a demo, **not reliable production hosting**. Neon [offers a free PostgreSQL plan](https://neon.com/pricing) with usage limits. Do not select a paid plan or enter payment details unless you decide to later.

## First deployment

1. Create free Render and Neon accounts. In Neon, create a project near Render's Frankfurt region and copy its host, database, username, and password. Form a private Npgsql connection string: `Host=<Neon host>;Database=<database>;Username=<user>;Password=<password>;SSL Mode=VerifyFull`. The app enforces certificate and host verification. Keep the real string out of GitHub and PRs.
2. After this branch is merged, in Render choose **New → Blueprint**, connect this GitHub repository, and select `render.yaml`. The Blueprint defines one free Docker web service, one instance, manual deployments, and `/health` checks. It prompts for `ConnectionStrings__Statistics`; enter the Neon connection string as a private environment variable. No Render database or disk is needed.
3. Open the assigned `https://…onrender.com` URL. Render supplies TLS and redirects HTTP to HTTPS. Check `/health`, load a deep link directly, then use two separate browser profiles to create/join a Party lobby and finish a match. Repeat with Online matchmaking and finish an Online match. Verify SSE updates arrive without reloading.
4. In Neon, verify `online_match_history` contains the completed Online match. Restart the Render service and check that the row still exists. The browser cannot recover its previous guest identity after a restart yet, so previous statistics cannot be read through `/players/me` with a newly issued identity.

The database table is created on first statistics read/write. If the connection string is absent on Render, startup fails instead of silently storing history on ephemeral disk. Never use the previously exposed V1 credential.

## Updates and recovery

`autoDeployTrigger: off` prevents a merged commit from interrupting an active match. Schedule a quiet window, then use Render **Manual Deploy → Deploy latest commit** after CI passes. Deploys and idle restarts drop lobbies, matchmaking, guest sessions, SSE connections, and active games; players must start new sessions. Completed Online records remain in Neon. Render rollback restores the prior image but does not restore interrupted games; if data needs recovery, use Neon's restore tools within the free plan's available window. Keep a separate backup before risky database changes.

The architecture does not support multiple backend instances. Before increasing traffic, measure concurrent games, SSE connections, memory, and free-tier usage. Move active state and event distribution to shared infrastructure only when measured demand requires it. Guest identity persistence and a live end-to-end deployment check remain required before F053 can satisfy every acceptance criterion.

## Credential safety

Keep the Neon connection string only in Render's private `ConnectionStrings__Statistics` environment variable. Do not put it in `render.yaml`, a frontend `VITE_` variable, screenshots, logs, or tracked appsettings files. Local `.env` and private production settings are ignored by Git. If a credential is exposed, rotate it in the provider before redeploying. The old V1 repository contains a publicly committed database connection string; verify that its account is revoked or its password changed before public release. CI runs Gitleaks against repository history; also enable GitHub's native secret scanning and push protection in repository security settings if the account offers them. Production exception logs omit exception messages and values, retaining only the exception type and trace ID.
