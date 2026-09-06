#!/usr/bin/env node

import { execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const packageJson = JSON.parse(
  readFileSync(path.join(root, "package.json"), "utf8"),
);
const expectedAliases = ["aic", "aicapture", "effortless", "ssotme"];

assertEqual(packageJson.name, "@effortlessapi/cli", "package name");
assertEqual(
  Object.keys(packageJson.bin).sort().join(","),
  expectedAliases.join(","),
  "binary aliases",
);
for (const alias of expectedAliases) {
  assertEqual(packageJson.bin[alias], "cli.js", `${alias} shim`);
}

const work = mkdtempSync(path.join(tmpdir(), "effortless-npm-package-"));
const packResult = JSON.parse(
  run("npm", ["pack", "--json", "--pack-destination", work], {
    cwd: root,
  }),
);
if (!Array.isArray(packResult) || packResult.length !== 1) {
  throw new Error(`Expected one npm tarball, got: ${JSON.stringify(packResult)}`);
}

const tarball = path.join(work, packResult[0].filename);
const prefix = path.join(work, "prefix");
run("npm", ["install", "--global", "--prefix", prefix, tarball], {
  cwd: work,
  stdio: "inherit",
});

for (const alias of expectedAliases) {
  const executable =
    process.platform === "win32"
      ? path.join(prefix, `${alias}.cmd`)
      : path.join(prefix, "bin", alias);
  const output = run(executable, ["-version"], {
    cwd: work,
    maxBuffer: 20 * 1024 * 1024,
  });
  const lastLine = output.trim().split(/\r?\n/).at(-1);
  assertEqual(lastLine, packageJson.version, `${alias} -version`);
}

console.log(
  `Verified ${packageJson.name}@${packageJson.version} from ${tarball}`,
);
console.log(`Aliases: ${expectedAliases.join(", ")}`);

function run(command, args, options = {}) {
  if (process.platform !== "win32") {
    return execFileSync(command, args, {
      encoding: "utf8",
      ...options,
    });
  }

  const commandLine = [command, ...args].map(quoteWindows).join(" ");
  return execFileSync(
    process.env.ComSpec ?? "cmd.exe",
    ["/d", "/s", "/c", commandLine],
    {
      encoding: "utf8",
      ...options,
    },
  );
}

function quoteWindows(value) {
  return `"${String(value).replaceAll('"', '""')}"`;
}

function assertEqual(actual, expected, label) {
  if (actual !== expected) {
    throw new Error(`${label}: expected '${expected}', got '${actual}'`);
  }
}
