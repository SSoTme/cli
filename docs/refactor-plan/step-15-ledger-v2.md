# Step 15 — Ledger v2: hash manifests replace `.zfs`, readable keys (proposed, non-blocking)

**Status:** proposed. Owner (D13) is not opposed; it is not critical. It may ship inside v2 or as the
first v2.x change. It does **not** block step 16. Recorded here so the reasoning is not lost.

## What the ledger is for

Every time the CLI writes files for a tool it records exactly what it wrote, per tool, per execution,
so that `clean` can remove precisely those files, `purge` can find orphans, and a run can be compared
against the last one. The `.zfs` file (gzipped FileSet XML) keeps the exact bytes, which allows a
byte-for-byte comparison or a full restore. The owner has never leveraged the restore.

## Proposal

- Replace `<root>/.effortless/<RelativePath>/<toolKey>.zfs` with
  `<root>/.effortless/<RelativePath>/<toolKey>.manifest.json`: tool, resolved version and URL,
  started/finished UTC, and for **every file actually written to disk** its relative path, byte length,
  and SHA-256. Same behavior everywhere: `clean` deletes exactly the manifest's files; `purge` uses
  manifests to find orphans; `-debug` writes nothing extra (the manifest is already readable);
  `skipClean` and `preserveZFS` keep their semantics (`preserveZFS` renamed `preserveLedger`, old name
  an alias). Byte-for-byte restore is dropped knowingly.
- Keys: restore the readable hyphenated form. `http://localhost:4242/rulebook-to-owl` becomes
  `http-localhost-4242-rulebook-to-owl`, not `httplocalhost4242rulebooktoowl` (the current
  `SanitizeUrlForFilename` output, which the owner called garbage). Catalog tools keep `LowerHyphenName`;
  local tools (step 12) use `local-<name>`.
- Migration, on project load: each `.zfs` is read once, converted to a manifest (hashes computed from the
  recorded bytes), written under the new key, and the `.zfs` deleted. Idempotent; failure is fatal with
  the file named.

## Rulebook changes

`ConfigFiles`: the manifest row replaces the `.zfs` and debug `.xml` rows. `LifecycleStates`: the
write/clean states reference the manifest. `CliOptions`: `preserveLedger` (+ alias). `UserMessages`:
migration line.

## Tests

`ledger-manifest-written-per-execution`, `ledger-clean-deletes-exactly-manifest`,
`ledger-purge-finds-orphans`, `ledger-key-is-readable`, `ledger-zfs-migrates-once`,
`ledger-skip-clean-preserved`, `ledger-preserve-ledger-alias`.

## Done when

No `.zfs` is written or read outside the migration; keys are readable; tests green;
`RefactorSteps.step-15.Status` → `done` (or explicitly deferred to v2.x in the README table).
