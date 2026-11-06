/**
 * Database tests for the festival D1 schema (issue #14).
 *
 * The tests build a throw-away SQLite database from the *committed* migration
 * files, so they prove that the SQL in `db/migrations/` is the schema that
 * ships. They never touch the local dev D1 in `.void/`.
 */
import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";
import Ajv2020 from "ajv/dist/2020.js";
import Database from "better-sqlite3";
import { drizzle } from "drizzle-orm/better-sqlite3";
import * as schema from "../db/schema.ts";
import seed from "../db/seed.ts";

const webRoot = fileURLToPath(new URL("..", import.meta.url));
const migrationsDir = join(webRoot, "db", "migrations");
const chartFixturePath = join(webRoot, "db", "fixtures", "who.chart.json");
const songFixturePath = join(webRoot, "db", "fixtures", "who.song.json");
const contractChartSchemaPath = join(webRoot, "..", "contracts", "schemas", "chart.v1.schema.json");

const chartText = readFileSync(chartFixturePath, "utf-8");
const chart = JSON.parse(chartText);
const song = JSON.parse(readFileSync(songFixturePath, "utf-8"));

/** A fresh in-memory database built from the committed migrations. */
function createMigratedDatabase() {
  const sqlite = new Database(":memory:");
  // D1 enforces foreign keys; SQLite does not by default.
  sqlite.pragma("foreign_keys = ON");
  const files = readdirSync(migrationsDir)
    .filter((file) => file.endsWith(".sql"))
    .sort();
  assert.ok(files.length > 0, `no migration files found in ${migrationsDir}`);
  for (const file of files) {
    sqlite.exec(readFileSync(join(migrationsDir, file), "utf-8"));
  }
  return sqlite;
}

function tableNames(sqlite) {
  return sqlite
    .prepare("SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'")
    .all()
    .map((row) => row.name)
    .filter((name) => !name.startsWith("_void_"))
    .sort();
}

function columnNames(sqlite, table) {
  return sqlite
    .prepare(`SELECT name FROM pragma_table_info('${table}')`)
    .all()
    .map((row) => row.name);
}

function assertRejected(label, run) {
  assert.throws(run, /constraint/i, `${label} must be rejected by the schema`);
}

test("the chart fixture satisfies the frozen contract schema", () => {
  const contract = JSON.parse(readFileSync(contractChartSchemaPath, "utf-8"));
  const ajv = new Ajv2020({ strict: false, allErrors: true });
  const validate = ajv.compile(contract);
  assert.ok(validate(chart), ajv.errorsText(validate.errors));
});

test("the committed migrations create exactly the three contract tables", () => {
  const sqlite = createMigratedDatabase();
  assert.deepEqual(tableNames(sqlite), ["chart_versions", "scores", "songs"]);
  assert.deepEqual(columnNames(sqlite, "songs"), [
    "id",
    "title",
    "artist",
    "youtubeId",
    "bpm",
    "level",
    "durationSec",
    "currentVersion",
  ]);
  assert.deepEqual(columnNames(sqlite, "chart_versions"), [
    "songId",
    "version",
    "json",
    "noteCount",
    "createdAt",
  ]);
  assert.deepEqual(columnNames(sqlite, "scores"), [
    "id",
    "songId",
    "version",
    "nickname",
    "p",
    "g",
    "b",
    "m",
    "score",
    "createdAt",
  ]);
});

test("the seed publishes the chart file verbatim", async () => {
  const sqlite = createMigratedDatabase();
  const db = drizzle(sqlite, { schema });
  await seed({ dialect: "sqlite", db, schema });

  const songRows = sqlite.prepare("SELECT * FROM songs").all();
  assert.equal(songRows.length, 1);
  assert.deepEqual(songRows[0], song);

  const chartRows = sqlite.prepare("SELECT * FROM chart_versions").all();
  assert.equal(chartRows.length, 1);
  assert.equal(chartRows[0].songId, song.id);
  assert.equal(chartRows[0].version, song.currentVersion);
  assert.equal(chartRows[0].noteCount, chart.notes.length);
  assert.equal(chartRows[0].json, chartText);
});

test("the schema rejects rows the contract would reject", () => {
  const sqlite = createMigratedDatabase();
  const insertSong = sqlite.prepare(
    `INSERT INTO songs (id, title, artist, youtubeId, bpm, level, durationSec, currentVersion)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?)`,
  );
  const validSong = [
    song.id,
    song.title,
    song.artist,
    song.youtubeId,
    song.bpm,
    song.level,
    song.durationSec,
    song.currentVersion,
  ];

  insertSong.run(...validSong);
  assert.equal(sqlite.prepare("SELECT COUNT(*) AS c FROM songs").get().c, 1);

  assertRejected("song id with upper case letters", () =>
    insertSong.run("Who", ...validSong.slice(1)),
  );
  assertRejected("song id that is too short", () => insertSong.run("ab", ...validSong.slice(1)));
  assertRejected("song id with an unsupported character", () =>
    insertSong.run("wh_o", ...validSong.slice(1)),
  );
  assertRejected("empty song title", () =>
    insertSong.run("empty-title", "", ...validSong.slice(2)),
  );
  assertRejected("short youtube id", () =>
    insertSong.run("short-youtube", ...validSong.slice(1, 3), "short", ...validSong.slice(4)),
  );
  assertRejected("bpm below the contract range", () =>
    insertSong.run("low-bpm", ...validSong.slice(1, 4), 0, ...validSong.slice(5)),
  );
  assertRejected("level above the contract range", () =>
    insertSong.run("high-level", ...validSong.slice(1, 5), 100, ...validSong.slice(6)),
  );
  assertRejected("zero duration", () =>
    insertSong.run("zero-duration", ...validSong.slice(1, 6), 0, ...validSong.slice(7)),
  );
  assertRejected("duration above the contract range", () =>
    insertSong.run("long-duration", ...validSong.slice(1, 6), 1801, ...validSong.slice(7)),
  );

  const insertChart = sqlite.prepare(
    "INSERT INTO chart_versions (songId, version, json, noteCount) VALUES (?, ?, ?, ?)",
  );
  insertChart.run(song.id, 1, chartText, chart.notes.length);
  assertRejected("chart version zero", () =>
    insertChart.run(song.id, 0, chartText, chart.notes.length),
  );
  assertRejected("duplicate chart version", () =>
    insertChart.run(song.id, 1, chartText, chart.notes.length),
  );
  assertRejected("chart of an unknown song", () =>
    insertChart.run("unknown-song", 1, chartText, chart.notes.length),
  );

  const insertScore = sqlite.prepare(
    `INSERT INTO scores (songId, version, nickname, p, g, b, m, score)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?)`,
  );
  insertScore.run(song.id, 1, "player", 10, 2, 1, 0, 900000);
  assertRejected("empty nickname", () => insertScore.run(song.id, 1, "", 10, 2, 1, 0, 900000));
  assertRejected("nickname longer than 16 characters", () =>
    insertScore.run(song.id, 1, "x".repeat(17), 10, 2, 1, 0, 900000),
  );
  assertRejected("score above the contract maximum", () =>
    insertScore.run(song.id, 1, "player", 10, 2, 1, 0, 1000001),
  );
  assertRejected("negative judgment count", () =>
    insertScore.run(song.id, 1, "player", -1, 2, 1, 0, 900000),
  );
  assertRejected("score of an unknown song", () =>
    insertScore.run("unknown-song", 1, "player", 10, 2, 1, 0, 900000),
  );
});
