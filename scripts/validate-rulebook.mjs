#!/usr/bin/env node

import { readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import path from "node:path";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const rulebookPath = path.join(
  root,
  "effortless-rulebook",
  "effortless-rulebook.json",
);
const errors = [];

let rulebook;
try {
  rulebook = JSON.parse(await readFile(rulebookPath, "utf8"));
} catch (error) {
  console.error(`Rulebook JSON is invalid: ${error.message}`);
  process.exit(1);
}

validateSingleLineLeaves(rulebook);

const tables = new Map(
  Object.entries(rulebook).filter(
    ([, value]) =>
      value &&
      typeof value === "object" &&
      Array.isArray(value.schema) &&
      Array.isArray(value.data),
  ),
);

for (const [tableName, table] of tables) {
  if (!isNonEmptyString(table.Description)) {
    errors.push(`${tableName}: table Description is required`);
  }

  if (table.schema.length === 0) {
    errors.push(`${tableName}: schema must contain at least one field`);
    continue;
  }

  for (const field of table.schema) {
    if (!isNonEmptyString(field.name)) {
      errors.push(`${tableName}: every field must have a name`);
    }
    if (!isNonEmptyString(field.Description)) {
      errors.push(`${tableName}.${field.name ?? "<unnamed>"}: Description is required`);
    }
  }

  validatePrimaryKeys(tableName, table);
}

for (const [tableName, table] of tables) {
  for (const field of table.schema.filter(
    (candidate) => candidate.type === "relationship",
  )) {
    validateForeignKey(tableName, table, field);
  }
}

validateTestCasePrimaryOptions();

if (errors.length > 0) {
  for (const error of errors) {
    console.error(`- ${error}`);
  }
  console.error(
    `Rulebook validation failed with ${errors.length} error${errors.length === 1 ? "" : "s"}.`,
  );
  process.exit(1);
}

console.log(
  `Rulebook validation passed (${tables.size} tables, ${countFields()} fields).`,
);

function validateSingleLineLeaves(value, jsonPath = "$") {
  if (typeof value === "string") {
    if (value.includes("\n") || value.includes("\r")) {
      errors.push(`${jsonPath}: string leaves must be single-line`);
    }
    return;
  }

  if (Array.isArray(value)) {
    value.forEach((item, index) =>
      validateSingleLineLeaves(item, `${jsonPath}[${index}]`),
    );
    return;
  }

  if (value && typeof value === "object") {
    for (const [key, item] of Object.entries(value)) {
      validateSingleLineLeaves(item, `${jsonPath}.${key}`);
    }
  }
}

function validatePrimaryKeys(tableName, table) {
  const primaryField = table.schema[0]?.name;
  if (!primaryField) {
    return;
  }

  const seen = new Set();
  table.data.forEach((row, index) => {
    const value = row[primaryField];
    if (value === null || value === undefined || value === "") {
      errors.push(
        `${tableName}.data[${index}].${primaryField}: primary key is required`,
      );
      return;
    }

    const key = `${value}`;
    if (seen.has(key)) {
      errors.push(`${tableName}.${primaryField}: duplicate value '${key}'`);
    }
    seen.add(key);
  });
}

function validateForeignKey(tableName, table, field) {
  const target = tables.get(field.RelatedTo);
  if (!target) {
    errors.push(
      `${tableName}.${field.name}: relationship target '${field.RelatedTo}' does not exist`,
    );
    return;
  }

  const targetKey = target.schema[0]?.name;
  const targetValues = new Set(
    target.data.map((row) => `${row[targetKey]}`),
  );

  table.data.forEach((row, index) => {
    const value = row[field.name];
    if (value === null || value === undefined || value === "") {
      if (field.nullable === false) {
        errors.push(
          `${tableName}.data[${index}].${field.name}: non-null relationship is required`,
        );
      }
      return;
    }

    if (!targetValues.has(`${value}`)) {
      errors.push(
        `${tableName}.data[${index}].${field.name}: '${value}' does not resolve to ${field.RelatedTo}.${targetKey}`,
      );
    }
  });
}

function validateTestCasePrimaryOptions() {
  const dispositions = new Map(
    rulebook.Dispositions.data.map((row) => [row.DispositionId, row]),
  );
  const options = new Map(
    rulebook.CliOptions.data.map((row) => [row.CliOptionId, row]),
  );
  const allowedStatuses = new Set(["blocked-by-decision", "deleted"]);

  rulebook.TestCases.data.forEach((testCase, index) => {
    const option = options.get(testCase.PrimaryOption);
    const retained =
      option &&
      dispositions.get(option.Disposition)?.IsRetained === true;
    if (!retained && !allowedStatuses.has(testCase.Status)) {
      errors.push(
        `TestCases.data[${index}].PrimaryOption: '${testCase.PrimaryOption}' is not retained and status '${testCase.Status}' is not allowed`,
      );
    }
  });
}

function isNonEmptyString(value) {
  return typeof value === "string" && value.trim().length > 0;
}

function countFields() {
  return [...tables.values()].reduce(
    (total, table) => total + table.schema.length,
    0,
  );
}
