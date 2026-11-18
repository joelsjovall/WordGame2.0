# Continuous integration

[`.github/workflows/ci.yml`](../.github/workflows/ci.yml) runs for every pull request targeting `main`, every push to `main`, and manual dispatch. No path filters exclude documentation or workflow changes. New runs cancel older runs for the same ref.

## Jobs and reporting

| Check                    | Responsibility                                                                                                                |
| ------------------------ | ----------------------------------------------------------------------------------------------------------------------------- |
| Backend build and tests  | Restore, formatting, Release build with warnings as errors, all three unit test projects, API integration tests               |
| Frontend build and tests | `npm ci`, strict TypeScript, ESLint, Prettier, Vitest component/client tests, production build                                |
| Browser smoke tests      | Install Chromium and Linux dependencies; run existing desktop and mobile Playwright tests against their managed local servers |
| Dependency scanning      | Test the npm audit policy; audit npm and direct/transitive NuGet packages                                                     |
| Secret scanning          | Scan complete V2 Git history with checksum-verified Gitleaks and fully redacted output                                        |
| CI status                | Always summarize every job and fail if any required job fails, is skipped, or is cancelled                                    |

Jobs run independently on Ubuntu 24.04 using the latest serviced .NET 10 SDK and Node.js 24. npm installs the committed lockfile; its download cache does not replace `npm ci`. Actions are pinned to verified release commit SHAs. Explicit timeouts bound failures.

Open a failed job to see its named command and logs. Backend TRX, frontend JUnit, and Playwright HTML/JUnit reports are uploaded even after failures when available, retained for seven days. Playwright also retains failure screenshots/traces. The final job writes a result table to the Actions summary. Builds failing before tests produce no test report; the failing build step remains the source of the error.

To enforce checks before merges, select **CI status** as a required status check in the `main` branch rules after its first run. The workflow defines checks; it does not change repository rules or prevent an administrator bypassing them. Do not add a required check for a job with path-based conditional execution without preserving the final gate.

## Dependency policy

`node scripts/check-npm-audit.mjs` runs npm audit from `frontend/`, reports individual advisories and totals, and fails for high/critical advisories. Registry errors and malformed/missing reports fail the job. Low/moderate findings remain visible for follow-up. The scanner requires registry access, not production services or credentials.

There is one temporary exception: **picomatch / GHSA-fc5q-39xc-vjpf**, at high severity, expires **2026-12-18 UTC**. At foundation verification, no patched registry release was available; npm reports 12 affected development dependency paths for this single advisory. The exception matches the advisory URL, package, severity, and expiry; it does not exempt other packages or severities. Remove it when a compatible fix is available. Review the upstream advisory and dependency tree before renewing it; do not extend the expiry silently or use `npm audit fix --force` to downgrade the toolchain.

The NuGet restore explicitly enables auditing for direct and transitive packages at high/critical severity and treats warnings as errors. This also fails on audit-source availability warnings; a network failure is not a clean scan.

Test the npm policy, including rejection of new advisories and expiration:

```sh
node --test scripts/tests/*.test.mjs
node scripts/check-npm-audit.mjs
dotnet restore backend/WordGame.sln --force-evaluate -p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=high -warnaserror
```

## Secret scanning and permissions

Gitleaks CLI 8.30.1 is downloaded from its official release and checked against a pinned SHA-256 before execution. It needs no license, personal access token, or project secret. A complete checkout lets it inspect V2 history, including deleted files. To reproduce after installing that version:

```sh
gitleaks git . --redact=100 --no-banner --log-opts="--all"
```

Never print findings without redaction or upload unredacted reports. Resolve actual leaks by removing the value and rotating the credential; deleting a file alone does not remove a historical leak. No V1 history is checked out or scanned here, and a clean V2 scan does not verify rotation of the previously exposed V1 credential.

CI requests only `contents: read`, checks out without persisting Git credentials, and uses `pull_request` rather than `pull_request_target`. It has no deployment steps or production credentials. Fork pull requests use the same local datasets, in-memory sessions, and test hosts. Dependency registries, browser/scanner downloads, GitHub, and artifact storage require network access.

## Maintenance and extension

See [automated testing](testing.md) for suite organization and local commands. Add tests to the existing suites so CI discovers them automatically. Keep gameplay and multiplayer tests on the shared authoritative engine and isolated local services. Browser tests already run because the homepage/navigation smoke flows exist; add multi-context player journeys as multiplayer features arrive.

When adding a new test project, update the explicit backend unit-project loop or add a named integration step. When adding a job, include it in `ci.needs` so the final gate cannot pass without it. Keep exit codes meaningful: do not use `continue-on-error`, `--pass-with-no-tests`, or pipelines that hide failures. Add diagnostic artifacts only when useful, with short retention and no sensitive content.

Update pinned action SHAs with their version comments after reviewing upstream releases. Update scanner versions and checksums together, and validate edits with [actionlint](https://github.com/rhysd/actionlint). Frontend checkout uses LF endings so Prettier behaves consistently on Windows and Linux.

V1's [CI at the reviewed revision](https://github.com/joelsjovall/wordgame/blob/dd9c2399a3f852cd7a49fce309286bea9e78e3f0/.github/workflows/ci.yml) informed restore/build/lint, test artifacts, security checks, and summary reporting. V2 uses its current projects, runtimes, in-process API tests, and Playwright-managed servers. No legacy deployment, database, Postman, or repeated build dependencies were copied.

Primary references: [setup-node](https://github.com/actions/setup-node), [setup-dotnet](https://github.com/actions/setup-dotnet), [Gitleaks](https://github.com/gitleaks/gitleaks), and [NuGet auditing](https://learn.microsoft.com/nuget/concepts/auditing-packages).
