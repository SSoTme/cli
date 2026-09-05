# Step 13 — Effortless authentication workload, and wire the auth commands to it

**Goal:** `login`, `projectLogin`, `logout`, and `plan` stop being local stubs and talk to a real,
published Effortless tool. For now that tool always succeeds and nothing enforces anything; it exists so
the CLI seam is real and the service can grow behind it when subscriptions or per-tool login arrive.

Owner (D18, 2026-09-04): "create one CPLN workload that scales to zero after 5 minutes, because the
commands it supports will never be called until we start selling subscriptions, or requiring a login model
to run certain tools. For now we're not even tracking who's running which tools, or when."

## Cross-repo work (not in this repo)

In `../api.effortlessapi.com/Versioned-Stable-SSoTme-Tools/` (private repo):

1. Scaffold `tools/effortless/effortless-auth/` (name proposed) with `scripts/create-ssotme-tool`, the
   same shape as every other published tool (`workload/`, `start.sh`, its own local dev port read from
   its `Program.cs`).
2. Routes, all returning success with a minimal JSON body for now:
   `POST /login` (email → would send a magic link; returns `{ ok: true, token: "<preview>" }`),
   `POST /verify` (code → token), `POST /project-login` (project id → project token),
   `GET /plan` (→ `{ plan: "preview", enforced: false }`), `POST /logout` (→ `{ ok: true }`).
3. CPLN: minimum instances 0, scale-to-zero after 300 s idle, smallest CPU/memory class.
4. Publish it with **`scripts/publish-tool.sh effortless-auth`** (the transpiler-server must be running).
   Never hand-roll a publish. While developing, run it locally and point the CLI at it with
   `effortless -setToolUrl effortless-auth=http://localhost:<port>`; remove the override afterwards.

## CLI side (this repo)

- `login` / `projectLogin` / `plan` / `logout` resolve `effortless/effortless/effortless-auth` through the
  normal catalog path (R0 freshness applies; a missing catalog entry is a clear error naming the tool).
- Each command POSTs its route and treats any 2xx as success. `login` and `projectLogin` store a returned
  token where step 10 puts tokens (`~/.effortless/…`, or `effortless.env` for project login); `logout`
  clears local tokens as today, then calls the route best-effort.
- Output tells the truth: `Signed in (preview: the authentication service does not enforce accounts yet).`
- Tool execution and `buildOnTrigger` stay unauthenticated. No usage tracking is added.
- `MagicLinkAuth.cs` stub bodies are replaced by the REST calls; the seam (`AuthCommands`) is unchanged.

## Rulebook changes

`HttpEndpoints`: replace `bridge-auth` with the five `effortless-auth` routes (liveness probed at
implementation time). `CliOptions`: help text for the four commands loses "currently unavailable".
`UserMessages`: the preview line. `ConfigFiles`: token files unchanged from step 10.
`ToolResolutionRules`: none (it is an ordinary catalog tool).

## Tests

Mock auth tool in the E2E mock server. `auth-login-calls-tool-and-stores-token`,
`auth-project-login-writes-env`, `auth-plan-prints-preview`, `auth-logout-clears-then-calls`,
`auth-tool-missing-from-catalog-is-clear-error`, `auth-tool-5xx-is-clear-error`,
`tools-still-run-without-login`. Existing `auth-*` stub tests are updated (`keep-modified`).

## Done when

The workload is published and `[latest]` resolves to it; all four commands round-trip against it and
against the mock; scale-to-zero verified in CPLN; tests green; `RefactorSteps.step-13.Status` → `done`.

## Done 2026-09-05

- **Workload.** `tools/effortless/effortless-auth/` in `api.effortlessapi.com/Versioned-Stable-SSoTme-Tools`
  (scaffolded with `create-ssotme-tool`, dev port 30080, `start.sh`). Routes as specified, plus the standard
  transpile contract on `POST /` (`-p mode=login|verify|project-login|plan|logout` → `auth-result.json`).
  Tokens are unsigned `preview.<base64url json>` strings; nothing verifies them yet.
- **CPLN.** `minScale 0`, `scaleToZeroDelay 300`, `maxScale 1`, 200m/256Mi, container port 30000 (the
  convention every published tool uses; the dev port stays 30080). Published three times with
  `publish-tool.sh` after creating the Airtable record through the transpiler-server's
  `POST /api/transpilers/import-existing` (the tool had never been created in the UI). `[latest]` resolves
  to `v2026.09.05.1228` through the live bridge (`effortless effortless-auth -listVersions`).
- **OPEN: the workload has not come online.** Every version (100m/128Mi on 30080, 200m/256Mi on 30080,
  200m/256Mi on 30000) sits at "Health Check Failed", the edge answers 421, and the container writes no
  logs, although the pushed image runs and answers `/plan` locally (63 MiB). The two other tools the owner
  published earlier on 2026-09-05 (`rulebook-to-progress-report` 02:10, `rulebook-to-node-postgres-api`
  03:22) show the same "Health Check Failed" while everything published on or before 2026-09-02 is ready,
  so this looks like a Control Plane-side problem today rather than the tool. Re-check with
  `cpln workload get-deployments effortless-effortless-auth-v2026-09-05-1228 --gvc ssotme-tools`; if it stays
  down once the other two recover, the next thing to try is a redeploy from the UI. Until then the CLI
  commands report "Could not reach the authentication service" (a clear error, no token written); tools
  and builds are unaffected because they never call it.
- **CLI.** `MagicLinkAuth` now makes the REST calls; `AuthCommands` is unchanged apart from passing the
  invocation to `projectLogin`. Resolution is the ordinary catalog path: a `tool_urls.json` override for
  `effortless-auth` wins (local dev loop), else R0 freshness and the catalog head. `logout` clears local
  tokens first and calls `/logout` best-effort using cache-only resolution so it never triggers a refresh.
  `plan` works without a token because the service does not require one; the output says so.
- **Tests.** `MockAuthTool` harness; `AuthTests` rewritten (`AuthTestBridge` removed); eight new cases
  including `tools-still-run-without-login` and `auth-local-override-used-for-dev`.
- `bridge-auth` was replaced in `HttpEndpoints` by the five `auth-*` routes.
