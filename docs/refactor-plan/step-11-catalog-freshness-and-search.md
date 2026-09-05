# Step 11 — Catalog freshness v2 and a searchable tool catalog

**Goal:** the remote tool catalog stays current without anyone typing `-upgrade` or `-refreshTools`, and
the tool list is genuinely searchable rather than a name-substring grep.

## What already exists (do not rebuild)

`R0-catalog-freshness` (step-03a) already guarantees that every catalog-dependent invocation uses a list
confirmed within the preceding 24 hours, and that project tools track it. That *is* the "every 24 hours
or so" rule. `help`/`version`/auth/URL management/settings/describe/clean stay offline. Direct-URL,
`-execute`, and (after step-12) local tools are not catalog-versioned and are excluded.

## New: refresh on complete timeout (D29, confirmed)

New rule `R11-refresh-on-timeout`, added to `ToolResolutionRules`:

- Trigger: a **catalog-resolved** tool's POST fails with a connection-level failure: DNS resolution
  failure, connection refused, TLS handshake failure, or the request exceeding `waitTimeout` with no
  response bytes. A tool that answers (even with 4xx/5xx) is *not* a complete timeout; the existing
  `RetryRules` matrix owns that.
- Action: force one catalog refresh (ignoring the 24h stamp). If the tool's head URL changed, print
  `Tool <name> moved; retrying against <new-url>` and retry once. If unchanged, or the retry fails the
  same way, fail hard with the existing message plus the catalog age.
- Bound: at most one forced refresh per CLI invocation for one-shot commands, no matter how many steps
  time out. Long-running modes (`buildOnTrigger`, `serve`) may force a refresh at most once per
  10 minutes. Owner (D29): a workload that cannot be reached after the connection retries is offline;
  check the latest build numbers and try the build again at least once.
- Excluded: `-targetUrl`, bare URLs, `tool_urls.json` overrides, local tools. Those never consult the
  catalog, so a refresh cannot help.
- Refresh failure during this path is fatal, same as today. No stale-catalog fallback.

## New: searchable catalog

Today `RemoteToolsIndex.ListTools(search)` matches a substring of `account/category/tool` and prints
name + head version. The catalog entry carries, per version: `versionNumber`, `createdAt`, `isHeadVersion`,
`isActive`, `isPublic`, `requiresAPIKey`, `versionCount`, `monthlyRequestCount`, `defaultPortNumber`. It
carries **no description or tags**. So search has a CLI half and a bridge half.

CLI half (this repo):

- `-searchTools <text>` matches account, category, tool name, and `description`/`tags` when present
  (case-insensitive, all terms must match).
- Filters, usable with `listTools` and `searchTools`: `-category <c>`, `-account <a>` (reuses the existing
  option), `-updatedSince <yyyy-mm-dd>`, `-headOnly`, `-requiresKey true|false`.
- `-sort name|updated|popular` (popular = `monthlyRequestCount`).
- `-json` emits the filtered entries as JSON for scripts and skills.
- Table output: name, head version, head date, versions, key-required, and description when present.
- `info` prints the catalog `fetchedAt`, its age ("catalog age" = time since the tool list was last
  refreshed), and when the next automatic refresh is due (D23).
- Search is always answered from the local cache (which R0 keeps ≤ 24h old); it never goes to the network
  by itself. `-refreshTools` remains the explicit "now".

Bridge half (out of this repo; file against `cli-cloud-bridge` / transpiler-server): add per-tool
`description`, `tags[]`, `inputKinds[]`, `outputKinds[]` to the list payload. The CLI treats every one of
them as optional so the order of shipping does not matter.

## Rulebook changes

`ToolResolutionRules`: add R11. `CliOptions`: new rows `category`, `updatedSince`, `headOnly`,
`requiresKey`, `sort`, `json` (all `modifier`, parents `listTools`/`searchTools`), each with tier,
rationale, summary, example (step-09 fields). `WirePayloadFields`: add the optional catalog fields.
`UserMessages`: the moved/retrying and catalog-age lines. `HttpEndpoints`: unchanged.

## Tests

Mock bridge fixture with descriptions/tags on some tools and none on others. E2E: `catalog-search-text`,
`catalog-search-multi-term`, `catalog-filter-category`, `catalog-filter-updated-since`,
`catalog-sort-popular`, `catalog-json`, `catalog-info-shows-age`, `timeout-forces-one-refresh`,
`timeout-retries-once-on-moved-url`, `timeout-does-not-refresh-for-target-url`,
`timeout-second-step-does-not-refresh-again`. Unit: R11 trigger classification per failure type.

## Done when

R11 implemented and bounded; search filters and `-json` work against the cache; `info` shows catalog age;
bridge fields consumed when present; the bridge-side ticket exists; tests green;
`RefactorSteps.step-11.Status` → `done`.

## Done 2026-09-05

- **R11** lives in `TranspileClientResult.ConnectionFailure` (set only when the workload never produced an
  HTTP response: host not found, connection refused/reset, TLS failure, or no bytes within `waitTimeout`),
  `CliInvocation.IsCatalogResolved` (set by `ToolResolver` only on a catalog hit; `-targetUrl`, bare URLs and
  `tool_urls.json` overrides leave it false), `ToolResolver.ReResolveFromCatalog`, and
  `CommandDispatcher.TryRecoverFromCompleteTimeout`. One forced refresh per process; when `buildOnTrigger`
  is active the budget renews every 10 minutes (`ForcedRefreshInterval`). A refresh failure prints the
  existing fatal message and the step fails with the original error.
- **Legacy wait-for-cook is untouched.** After a connection-refused abort the client still waits out the
  step's own `waitTimeout` (the characterized v1 behavior), so R11 kicks in after that wait. Note that
  `build -waitTimeout N` is not inherited by steps; the timeout in a step's `CommandLine` governs.
- **The catalog already carries `description`** (189/200 live entries) plus `metaData.createdAt`,
  `versionCount`, `requiresAPIKey`, `monthlyRequestCount`; the plan's "no description" assumption was
  stale. `category` is empty on every entry, so the canonical-name segment is the effective category.
  `tags[]`, `inputKinds[]`, `outputKinds[]` are consumed when present and were filed as
  <https://github.com/EffortlessAPI/api.effortlessapi.com/issues/63>.
- **Output.** `listTools`/`searchTools` print an aligned table (NAME, HEAD, UPDATED, VERSIONS, KEY,
  DESCRIPTION truncated to 60) or `-json`. New modifiers `-category`, `-updatedSince`, `-headOnly`,
  `-requiresKey`, `-sort`, `-json` (all `ParentOption: listTools`; `-account` is reused). Bad values are
  explicit errors. `info` prints `Catalog: fetched <utc>, age <h m>, next automatic refresh <utc>`.
- Tests: `tests/fixtures/index/catalog-search.json`, `CatalogSearchTests` (13 cases incl. the four
  `timeout-*` cases against a reserved closed port), `unit-r11-classification`.
