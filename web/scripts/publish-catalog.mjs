#!/usr/bin/env node
/**
 * Publish the original chart data of the Unity project into the festival D1.
 *
 * The source of every song is `Assets/Game/Resources/<title>.json` (the
 * legacy chart format Unity loads). The metadata of every song comes from the
 * parallel arrays of `Assets/Game/Scripts/Songs/GManager.cs` — the same arrays
 * the fixed catalog of the game is built from. Nothing is invented: the chart
 * document is handed to the API as it is, and the metadata is the data the
 * game itself uses.
 *
 * The script does three phases:
 *
 *   1. plan     — every chart file is mapped to a song id and checked against
 *                 the contract (notes, level, duration, video id).
 *   2. publish  — one `PUT /api/v1/admin/songs/{songId}` per song.
 *   3. verify   — the catalog and every published snapshot are read back and
 *                 compared with the source file (notes, BPM, offset, maxBlock,
 *                 lyrics). A difference ends the script with a non zero exit.
 *
 * Usage:
 *
 *   FLASHBEAT_ADMIN_TOKEN=… node web/scripts/publish-catalog.mjs --dry-run
 *   FLASHBEAT_ADMIN_TOKEN=… node web/scripts/publish-catalog.mjs
 *   FLASHBEAT_ADMIN_TOKEN=… node web/scripts/publish-catalog.mjs --include-empty
 *
 * Options:
 *
 *   --api <url>       API base URL (default: https://flashbeat.rebuildup.dev)
 *   --resources <dir> chart directory (default: Assets/Game/Resources)
 *   --gmanager <file> GManager source with the metadata arrays
 *   --dry-run         only print the plan, publish nothing
 *   --attempt-blocked  also send the blocked songs once (the API rejects them)
 */
import { readFileSync, readdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..");

const args = process.argv.slice(2);
const option = (name, fallback) => {
  const index = args.indexOf(name);
  return index >= 0 && args[index + 1] ? args[index + 1] : fallback;
};
const flag = (name) => args.includes(name);

const apiOrigin = option("--api", "https://flashbeat.rebuildup.dev").replace(/\/+$/, "");
const resourcesDir = resolve(repoRoot, option("--resources", "Assets/Game/Resources"));
const gmanagerPath = resolve(repoRoot, option("--gmanager", "Assets/Game/Scripts/Songs/GManager.cs"));
const dryRun = flag("--dry-run");
const attemptBlocked = flag("--attempt-blocked") || flag("--include-empty");
const token = process.env.FLASHBEAT_ADMIN_TOKEN ?? "";

/**
 * Song ids of the catalog.
 *
 * The contract asks for `^[a-z0-9-]{3,64}$`, so a Japanese title needs an id
 * of its own. `who` and `lost-umbrella` are the ids the catalog already
 * serves; the rest follow the same rule (romanised title).
 */
const SONG_IDS = {
  ロストアンブレラ: "lost-umbrella",
  まにまに: "manimani",
  Who: "who",
  ツイッターランド: "twitterland",
  終焉逃避行: "shuuen-boukou",
  エウタナシア: "euthanasia",
  Chartreuse: "chartreuse",
  Psyched: "psyched",
  Dogbite: "dogbite",
  チュートリアル: "tutorial",
  狂喜蘭舞: "kyouki-ranbu",
  Poison: "poison",
  セルフィー: "selfie",
  "4th smile": "4th-smile",
  Calamity: "calamity",
  パノプティコン: "panopticon",
  GURU: "guru",
  阿吽のビーツ: "agon-beats",
  混沌ブギ: "konton-bugi",
  春嵐: "shunran",
  ビビビビ: "bibibibi",
  "灰Φ倶楽部": "hai-kurakubu",
  オーバーライド: "override",
  イガク: "igaku",
  テレキャスタービーボーイ: "tele-caster-boy",
  一龠: "ichikan",
  ニセモノ: "nisemono",
};

/** Charts that are never part of the festival catalog. */
const EXCLUDED_FILES = new Set(["CloudCatalogConfig.json", "E2EProbeSong.json"]);

/**
 * Levels the contract cannot hold.
 *
 * `SongPublishRequest.level` is `1..99` and the D1 table has the same CHECK
 * constraint, so the tutorial (level 0 in the game list) cannot be published
 * with its own value. The chart itself stays untouched; only the level of the
 * catalog entry is normalised to the smallest value the contract allows.
 */
const LEVEL_OVERRIDES = {
  チュートリアル: 1,
};

const SONG_ID_PATTERN = /^[a-z0-9-]{3,64}$/;
const YOUTUBE_ID_PATTERN = /^[A-Za-z0-9_-]{11}$/;

/** Read one array literal of the GManager source. */
function parseGManagerArray(source, name) {
  const match = source.match(new RegExp(`${name}\\s*=\\s*\\{([^}]*)\\}`, "s"));
  if (!match) {
    throw new Error(`GManager has no array ${name}`);
  }
  // The last entry of the numeric arrays is commented out; it was a leftover
  // of a third catalog placeholder and is not part of the song list.
  const body = match[1].replace(/\/\*[\s\S]*?\*\//g, "");
  const items = [...body.matchAll(/"([^"]*)"|(-?\d+(?:\.\d+)?f?)/g)].map((item) =>
    item[1] !== undefined ? item[1] : Number.parseFloat(item[2]),
  );
  return items;
}

/** Metadata of the songs, keyed by the title of the chart file. */
function readMetadata() {
  const source = readFileSync(gmanagerPath, "utf-8");
  const names = parseGManagerArray(source, "SongName");
  const musicians = parseGManagerArray(source, "Musician");
  const urls = parseGManagerArray(source, "SongURL");
  const bpms = parseGManagerArray(source, "SBPM");
  const levels = parseGManagerArray(source, "Slevel");
  const hits = parseGManagerArray(source, "Shit");
  const lengths = parseGManagerArray(source, "SongLong");

  const byTitle = new Map();
  names.forEach((title, index) => {
    // The first entry wins: `ニセモノ` is listed twice, the second entry was a
    // leftover of a third catalog placeholder.
    if (byTitle.has(title)) return;
    byTitle.set(title, {
      title,
      artist: musicians[index] ?? "",
      youtubeId: urls[index] ?? "",
      gmanagerBpm: bpms[index] ?? 0,
      level: levels[index] ?? 0,
      gmanagerHits: hits[index] ?? 0,
      durationSec: lengths[index] ?? 0,
    });
  });
  return byTitle;
}

/** The plan for one chart file. */
function planSong(file, metadata) {
  const title = file.replace(/\.json$/, "");
  const chart = JSON.parse(readFileSync(join(resourcesDir, file), "utf-8"));
  const meta = metadata.get(title);
  const id = SONG_IDS[title] ?? null;

  const notes = Array.isArray(chart.notes) ? chart.notes : [];
  const lastNoteSec = notes.length
    ? Math.max(...notes.map((note) => (note.num * 60) / (chart.BPM * note.LPB)))
    : 0;

  const problems = [];
  const normalisations = [];
  if (!meta) problems.push("no entry in the GManager song list");
  if (!id) problems.push("no song id for the title");
  else if (!SONG_ID_PATTERN.test(id)) problems.push(`song id "${id}" breaks the contract pattern`);
  if (!YOUTUBE_ID_PATTERN.test(meta?.youtubeId ?? "")) {
    problems.push(`video id "${meta?.youtubeId ?? ""}" breaks the contract pattern`);
  }
  if (!notes.length) problems.push("the chart has no notes");

  let level = meta?.level ?? 0;
  if (level < 1 || level > 99) {
    const override = LEVEL_OVERRIDES[title];
    if (override === undefined) {
      problems.push(`level ${level} is outside 1..99`);
    } else {
      normalisations.push(`level ${level} -> ${override} (the contract holds 1..99)`);
      level = override;
    }
  }

  if ((meta?.durationSec ?? 0) <= 0) problems.push("no song length");
  else if (lastNoteSec > meta.durationSec) {
    problems.push(`the last note lands at ${lastNoteSec.toFixed(1)}s, the song is ${meta.durationSec}s`);
  }

  return {
    file,
    id,
    title: meta?.title ?? title,
    artist: meta?.artist ?? "",
    youtubeId: meta?.youtubeId ?? "",
    level,
    sourceLevel: meta?.level ?? 0,
    durationSec: meta?.durationSec ?? 0,
    chart,
    notes: notes.length,
    bpm: chart.BPM,
    offset: chart.offset,
    maxBlock: chart.maxBlock,
    lyrics: chart.lyrics ?? null,
    gmanagerBpm: meta?.gmanagerBpm ?? 0,
    gmanagerHits: meta?.gmanagerHits ?? 0,
    lastNoteSec,
    normalisations,
    problems,
  };
}

/** Compare the source file with the snapshot the API serves. */
function compareCharts(source, stored) {
  const differences = [];
  const check = (name, left, right) => {
    const a = JSON.stringify(left);
    const b = JSON.stringify(right);
    if (a !== b) differences.push(`${name}: source ${a} != stored ${b}`);
  };

  check("notes", source.notes, stored.notes);
  check("BPM", source.BPM, stored.BPM);
  check("offset", source.offset, stored.offset);
  check("maxBlock", source.maxBlock, stored.maxBlock);
  check("name", source.name, stored.name);
  check("lyrics", source.lyrics ?? null, stored.lyrics ?? null);
  return differences;
}

async function fetchCatalog() {
  const response = await fetch(`${apiOrigin}/api/v1/catalog`);
  if (!response.ok) {
    throw new Error(`GET /api/v1/catalog answered ${response.status}`);
  }
  return response.json();
}

async function publish(song) {
  const response = await fetch(`${apiOrigin}/api/v1/admin/songs/${song.id}`, {
    method: "PUT",
    headers: {
      accept: "application/json",
      authorization: `Bearer ${token}`,
      "content-type": "application/json",
    },
    body: JSON.stringify({
      title: song.title,
      artist: song.artist,
      youtubeId: song.youtubeId,
      level: song.level,
      durationSec: song.durationSec,
      chart: song.chart,
    }),
  });
  const body = await response.json().catch(() => null);
  return { status: response.status, body };
}

async function fetchChart(id, version) {
  const response = await fetch(`${apiOrigin}/api/v1/charts/${id}?version=${version}`);
  if (!response.ok) {
    return { status: response.status, body: null };
  }
  return { status: response.status, body: await response.json() };
}

const metadata = readMetadata();
const files = readdirSync(resourcesDir)
  .filter((file) => file.endsWith(".json") && !EXCLUDED_FILES.has(file))
  .sort();

const songs = files.map((file) => planSong(file, metadata));
const blocked = songs.filter((song) => song.problems.length > 0);
const publishable = songs.filter((song) => song.problems.length === 0);

console.log(`# plan (${songs.length} chart files, ${publishable.length} publishable)`);
console.log("| file | id | notes | BPM | offset | maxBlock | level | duration | state |");
console.log("|---|---|---|---|---|---|---|---|---|");
for (const song of songs) {
  const state = song.problems.length
    ? song.problems.join("; ")
    : song.normalisations.length
      ? `ok (${song.normalisations.join("; ")})`
      : "ok";
  console.log(
    `| ${song.file} | ${song.id ?? "-"} | ${song.notes} | ${song.bpm} | ${song.offset} | ${song.maxBlock} | ${song.sourceLevel}${song.level !== song.sourceLevel ? `->${song.level}` : ""} | ${song.durationSec} | ${state} |`,
  );
}
for (const song of blocked) {
  console.error(`blocked: ${song.file} — ${song.problems.join("; ")}`);
}

if (dryRun) {
  console.log("\n# dry run: nothing was published");
  process.exit(0);
}

if (!token) {
  console.error("FLASHBEAT_ADMIN_TOKEN is missing");
  process.exit(2);
}

const before = await fetchCatalog();
console.log(`\n# catalog before: version ${before.catalogVersion}, ${before.songs.length} songs`);

const results = [];
for (const song of publishable) {
  const result = await publish(song);
  results.push({ song, result });
  const summary =
    result.status === 200
      ? `chartVersion ${result.body.chartVersion}, catalogVersion ${result.body.catalogVersion}`
      : JSON.stringify(result.body);
  console.log(`published ${song.id} (${song.file}) -> ${result.status} ${summary}`);
}

// The blocked songs are tried once, so the report can show what the API
// answers for them. A rejected publish writes nothing.
if (attemptBlocked) {
  for (const song of blocked) {
    const result = await publish(song);
    console.log(
      `attempted ${song.id} (${song.file}) -> ${result.status} ${JSON.stringify(result.body)}`,
    );
  }
}

const after = await fetchCatalog();
console.log(`\n# catalog after: version ${after.catalogVersion}, ${after.songs.length} songs`);

const beforeById = new Map(before.songs.map((song) => [song.id, song]));
let failures = 0;
console.log("\n# comparison (source file vs the snapshot the API serves)");
console.log(
  "| id | title | notes | BPM | offset | maxBlock | lyrics | chartVersion | noteCount | state |",
);
console.log("|---|---|---|---|---|---|---|---|---|---|");
for (const { song, result } of results) {
  const listed = after.songs.find((entry) => entry.id === song.id);
  const previous = beforeById.get(song.id);
  const snapshot = listed ? await fetchChart(song.id, listed.chartVersion) : { status: 0, body: null };
  const differences =
    snapshot.status === 200 ? compareCharts(song.chart, snapshot.body.chart) : ["no snapshot"];
  if (differences.length > 0) {
    failures += 1;
  }
  const lyrics = song.lyrics ? Object.keys(song.lyrics).length : 0;
  const version = previous
    ? `${previous.chartVersion} -> ${listed?.chartVersion}`
    : `new -> ${listed?.chartVersion}`;
  console.log(
    `| ${song.id} | ${song.title} | ${song.notes} | ${song.bpm} | ${song.offset} | ${song.maxBlock} | ${lyrics} | ${version} | ${listed?.noteCount} | ${differences.length ? differences.join("; ") : "identical"} |`,
  );
}

console.log(`\n# ${results.length} songs published, ${failures} with differences`);
process.exit(failures > 0 ? 1 : 0);
