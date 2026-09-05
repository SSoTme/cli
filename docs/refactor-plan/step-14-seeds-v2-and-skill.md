# Step 14 — Seeds v2: seed sources, verbs, and the effortless-seeds skill

**Goal:** seeds are a first-class, well-explained feature. Seed discovery works across a configurable
list of GitHub accounts with `ssotme` and `effortlessapi` as the two defaults, there are verbs to manage
that list, and a skill in `../effortless-skills` teaches Claude how to use the whole seed architecture.

Owner (D1, D25): do not drop seeds. A seed is a public repository with `effortless.json` at its root;
cloning preserves `.git` and never runs downloaded code; explicit `$key$` replacement stays; Airtable
schema guessing stays gone. "ssotme and effortlessapi should just be 2 default endpoints."

## Seed sources

- Stored at `~/.effortless/seed_sources.json`: an ordered list of GitHub accounts. When the file is
  absent the list is `["ssotme", "effortlessapi"]`. `EFFORTLESS_SEED_GITHUB_ACCOUNT` (existing) is
  prepended for that invocation.
- New verbs: `listSeedSources`, `addSeedSource <account>`, `removeSeedSource <account>`. Removing a
  default is allowed (the file then records the full list explicitly).
- `listSeeds` lists across every source, grouped by account, and shows the repo description.
- `cloneSeed <account>/<repo>` clones that one; `cloneSeed <repo>` searches the sources in order and
  clones the first match, printing which account it came from; ambiguity across sources is an error that
  lists the candidates.
- Discovery rule unchanged: the repo must have `effortless.json` at the root. `ssotme-seed.json` →
  `effortless-seed.json` (both accepted; step 10).

## The skill (cross-repo)

Create `../effortless-skills/skills/effortless-seeds/SKILL.md`, following that repo's `CLAUDE.md`
(the `SKILL.md` is authoritative today; also add the row to its rulebook so it does not lag):

- Triggers: "list seeds", "clone a seed", "start from a seed", "make this repo a seed", "seed sources",
  "effortless seed".
- Scope gate: any project, or no project (cloning a seed creates one).
- Content: what a seed is and is not; discovery rule; the source list and its verbs; the `$key$`
  replacement contract and `seed-config-values.json` / `seed-secrets-values.json`; how to author and
  publish a seed (public repo, `effortless.json` at root, an `effortless-seed.json` for tokens, a README
  that says what the seed produces); how nested seeds relate to `buildWithSubprojects` (step 09).
- Then: push the skills repo (that push is for `effortless-skills` only; this repo still does not push
  before step 16) and run its `install.sh` so the installed `effortless-cli` skill and the new
  `effortless-seeds` skill match v2. Update the `effortless-cli` skill's command list once step 09 lands.

## Rulebook changes

`CliOptions`: three new rows (Tier secondary, category `seeds`); `listSeeds`/`cloneSeed` help rewritten.
`ConfigFiles`: `~/.effortless/seed_sources.json`. `EnvVariables`: `EFFORTLESS_SEED_GITHUB_ACCOUNT`
description updated. `UserMessages`: source list, ambiguity error, "cloned from <account>".

## Tests

Mock GitHub API in the E2E server with two accounts. `seed-sources-default-list`, `seed-source-add`,
`seed-source-remove-default`, `list-seeds-across-sources`, `clone-seed-qualified`,
`clone-seed-bare-first-match`, `clone-seed-bare-ambiguous`, `clone-seed-preserves-git-and-runs-nothing`,
`seed-replacements-effortless-seed-json`.

## Done when

Verbs work; discovery spans the sources; the skill exists, is pushed, and is installed locally; tests
green; `RefactorSteps.step-14.Status` → `done`.

## As built (2026-09-05)

- **Sources.** `SeedSources` (`Seeds/SeedSources.cs`) owns `~/.effortless/seed_sources.json`
  (`{ "sources": [...] }`); an absent file means `[ssotme, effortlessapi]` and `listSeedSources` marks those
  `(default)`. Any write stores the complete list, so a removed default stays removed.
  `EFFORTLESS_SEED_GITHUB_ACCOUNT` is prepended in memory only and shown as `(env)`. Account names are
  validated against GitHub's `[A-Za-z0-9-]{1,39}` rule.
- **Verbs.** `listSeedSources` (alias `lss`), `addSeedSource` (`ass`), `removeSeedSource` (`rss`); the two
  value-taking barewords were added to the generator's `consumedStringOptions` set.
- **Bare-name rule.** `cloneSeed <repo>` queries every source and requires exactly one match; a name present
  in two sources (or matching both a full and a short name in one) is the ambiguity error. "First match"
  therefore means the single source that has it, which the CLI names before cloning.
- **Mock GitHub.** The E2E harness's `MockGitHubServer` serves the users/repos API, the raw
  `effortless.json` probe, and real bare git repositories over git's dumb HTTP protocol, so `cloneSeed`
  runs an actual `git clone`. `SeedCatalogClient` gained `EFFORTLESS_SEED_GITHUB_API` /
  `EFFORTLESS_SEED_GITHUB_RAW` base-URL seams for it.
- **Legacy fix.** The legacy CLI wrote `seed-secrets-values.json` but read `seed-secret-values.json`; v2
  reads both and writes the singular name.
- **Skill.** `../effortless-skills/skills/effortless-seeds/SKILL.md` written; `effortless-cli` gained a v2
  command map (categories, verbs, resolution order) and `effortless-orchestrator` routes to the new skill;
  `Skills`/`SkillBodies` rows added to that repo's rulebook; `install.sh --yes` run so the installed copies
  match. **Not committed or pushed**: that repo's `CLAUDE.md` says the user reviews and commits manually,
  which overrides this step's "push the skills repo" instruction. Owner: review and push `effortless-skills`.
- **Open item (pre-existing, not step 14).** The legacy `~/.ssotme/seed_cache/<seed>/cache/**` "move these
  files into a fresh clone and unzip *.zip" behaviour was kept as a `ConfigFiles` row in step 04 but never
  implemented in v2; the row now says so. Decide: implement, or drop the row and the migration of that
  directory.
