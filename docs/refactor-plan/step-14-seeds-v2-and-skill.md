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
