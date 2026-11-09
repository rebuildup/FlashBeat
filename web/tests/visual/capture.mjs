#!/usr/bin/env node
/**
 * FlashBeat Web Studio — real-pixel visual capture (Issue #75).
 *
 * Drives a REAL headless Chromium (system Chrome via puppeteer-core, no browser
 * download) over the Studio routes at fixed viewports, records measured layout
 * facts, and saves screenshots + index.json under out/<timestamp>/.
 *
 * This tool measures the DOM. It does NOT perform a pixel/design review; the
 * index.json separates `measured` facts from `needsHumanVisualReview` items.
 *
 * Usage:
 *   npm run visual                     # seeds local D1, starts dev server on :5199
 *   VISUAL_BASE_URL=http://127.0.0.1:5173 npm run visual
 *   CHROME_PATH=/path/to/chrome npm run visual
 *
 * Exit codes: 0 = clean, 1 = horizontal overflow and/or console errors found
 * (also written to index.json), 2 = setup failure (no Chrome, server never ready).
 */
import puppeteer from "puppeteer-core";
import { spawn, execFileSync } from "node:child_process";
import fs from "node:fs/promises";
import fsSync from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const WEB_ROOT = path.resolve(HERE, "../..");
const OUT_DIR = path.join(HERE, "out");
const MANAGED_PORT = 5199;

const ROUTES = [
  { name: "home", path: "/" },
  { name: "chart", path: "/chart?songId=who&version=1", waitFor: "canvas" },
  { name: "publish", path: "/publish" },
];

const VIEWPORTS = [
  { label: "1440x900", width: 1440, height: 900, deviceScaleFactor: 1 },
  { label: "1280x720", width: 1280, height: 720, deviceScaleFactor: 1 },
  { label: "390x844", width: 390, height: 844, deviceScaleFactor: 2, isMobile: true, hasTouch: true },
];

function findChrome() {
  if (process.env.CHROME_PATH && fsSync.existsSync(process.env.CHROME_PATH)) {
    return process.env.CHROME_PATH;
  }
  const candidates = [
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
    "/Applications/Google Chrome Dev.app/Contents/MacOS/Google Chrome Dev",
    "/Applications/Chromium.app/Contents/MacOS/Chromium",
    "/usr/bin/google-chrome-stable",
    "/usr/bin/google-chrome",
    "/usr/bin/chromium",
    "/usr/bin/chromium-browser",
    "C:/Program Files/Google/Chrome/Application/chrome.exe",
  ];
  for (const c of candidates) if (fsSync.existsSync(c)) return c;
  try {
    return execFileSync("which", ["google-chrome"], { encoding: "utf8" }).trim();
  } catch {
    return null;
  }
}

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

async function waitForServer(base, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const res = await fetch(`${base}/api/v1/catalog`);
      if (res.ok) return true;
    } catch {
      /* not up yet */
    }
    await sleep(500);
  }
  return false;
}

async function startDevServer() {
  const env = {
    ...process.env,
    FLASHBEAT_ADMIN_TOKEN: process.env.FLASHBEAT_ADMIN_TOKEN ?? "local-dev-only",
  };
  console.log("[visual] seeding local D1 (void db seed)…");
  execFileSync("npx", ["void", "db", "seed"], { cwd: WEB_ROOT, env, stdio: "inherit", shell: process.platform === "win32" });
  const base = `http://127.0.0.1:${MANAGED_PORT}`;
  console.log(`[visual] starting dev server at ${base}…`);
  const child = spawn("npx", ["vp", "dev", "--port", String(MANAGED_PORT), "--host", "127.0.0.1"], {
    cwd: WEB_ROOT,
    env,
    stdio: "ignore",
    shell: process.platform === "win32",
  });
  if (!(await waitForServer(base, 180_000))) {
    child.kill("SIGTERM");
    throw new Error(`dev server did not become ready at ${base}`);
  }
  return { base, stop: () => child.kill("SIGTERM") };
}

/** In-page measurement of visible-element layout facts. Runs in the browser. */
function measurePage() {
  const vw = document.documentElement.clientWidth;
  const vh = window.innerHeight;
  const doc = document.scrollingElement;
  const horizontalScroll = doc.scrollWidth > doc.clientWidth + 1;

  const describe = (el, r) => ({
    tag: el.tagName.toLowerCase(),
    id: el.id || undefined,
    class: typeof el.className === "string" && el.className ? el.className.split(/\s+/).slice(0, 3).join(" ") : undefined,
    text: (el.textContent ?? "").trim().replace(/\s+/g, " ").slice(0, 60) || undefined,
    rect: { x: Math.round(r.x), y: Math.round(r.y), w: Math.round(r.width), h: Math.round(r.height) },
  });

  const clipped = [];
  const smallFonts = [];
  const smallTargets = [];
  let belowFold = 0;
  let visible = 0;

  for (const el of document.body.querySelectorAll("*")) {
    const r = el.getBoundingClientRect();
    if (r.width <= 0 || r.height <= 0) continue;
    const s = getComputedStyle(el);
    if (s.display === "none" || s.visibility === "hidden" || Number(s.opacity) === 0) continue;
    visible++;
    if (r.right > vw + 1 || r.left < -1) clipped.push(describe(el, r));
    if (r.bottom > vh + 1) belowFold++;
    const hasText = [...el.childNodes].some((n) => n.nodeType === Node.TEXT_NODE && n.textContent.trim());
    if (hasText) {
      const fontSize = Number.parseFloat(s.fontSize);
      if (fontSize < 12) smallFonts.push({ ...describe(el, r), fontSize });
    }
    if (el.matches('a,button,input,select,textarea,[role="button"],[role="tab"],[role="switch"],[tabindex]')) {
      if (Math.min(r.width, r.height) < 32) smallTargets.push({ ...describe(el, r), w: Math.round(r.width), h: Math.round(r.height) });
    }
  }

  return {
    viewport: { width: vw, height: vh },
    horizontalScroll,
    documentScrollWidth: doc.scrollWidth,
    visibleElementCount: visible,
    clippedElements: { count: clipped.length, samples: clipped.slice(0, 10) },
    belowFoldElements: { count: belowFold, note: "informational; vertical scroll is expected" },
    smallFontElements: { count: smallFonts.length, samples: smallFonts.slice(0, 15) },
    smallTapTargets: { count: smallTargets.length, samples: smallTargets.slice(0, 15) },
  };
}

function describeActiveElement() {
  const el = document.activeElement;
  if (!el || el === document.body) return { focused: false };
  const r = el.getBoundingClientRect();
  return {
    focused: true,
    tag: el.tagName.toLowerCase(),
    id: el.id || undefined,
    ariaLabel: el.getAttribute("aria-label") || undefined,
    text: (el.textContent ?? "").trim().replace(/\s+/g, " ").slice(0, 60) || undefined,
    rect: { x: Math.round(r.x), y: Math.round(r.y), w: Math.round(r.width), h: Math.round(r.height) },
  };
}

async function main() {
  const chrome = findChrome();
  if (!chrome) {
    console.error("[visual] no Chrome/Chromium found; set CHROME_PATH");
    process.exit(2);
  }
  let base = process.env.VISUAL_BASE_URL?.replace(/\/$/, "");
  let stopServer = () => {};
  if (base) {
    console.log(`[visual] using provided base URL ${base}`);
    if (!(await waitForServer(base, 10_000))) {
      console.error(`[visual] ${base} is not responding`);
      process.exit(2);
    }
  } else {
    ({ base, stop: stopServer } = await startDevServer());
  }
  console.log(`[visual] chrome: ${chrome}`);

  const stamp = new Date().toISOString().replace(/[:.]/g, "-");
  const dir = path.join(OUT_DIR, stamp);
  await fs.mkdir(dir, { recursive: true });

  const browser = await puppeteer.launch({
    executablePath: chrome,
    headless: true,
    args: ["--disable-gpu", "--no-first-run", "--disable-extensions"],
  });

  const captures = [];
  let failed = false;

  try {
    const page = await browser.newPage();
    for (const route of ROUTES) {
      for (const vp of VIEWPORTS) {
        const consoleErrors = [];
        const onConsole = (msg) => {
          if (msg.type() === "error") consoleErrors.push(msg.text().slice(0, 300));
        };
        const onPageError = (err) => consoleErrors.push(String(err).slice(0, 300));
        page.on("console", onConsole);
        page.on("pageerror", onPageError);

        await page.setViewport(vp);
        const url = `${base}${route.path}`;
        await page.goto(url, { waitUntil: "networkidle2", timeout: 60_000 });
        if (route.waitFor) {
          await page.waitForSelector(route.waitFor, { timeout: 15_000 }).catch(() => {});
        }
        await sleep(1_200);

        const measured = await page.evaluate(measurePage);
        const baseName = `${route.name}_${vp.label.replace(/[^0-9x]/g, "")}`;
        const shot = path.join(dir, `${baseName}.png`);
        await page.screenshot({ path: shot }); // viewport shot — what the user actually sees
        const fullShot = path.join(dir, `${baseName}.full.png`);
        await page.screenshot({ path: fullShot, fullPage: true }); // for human review of below-fold

        const entry = {
          route: route.name,
          url,
          viewport: vp.label,
          file: path.basename(shot),
          fullPageFile: path.basename(fullShot),
          bytes: (await fs.stat(shot)).size,
          measured,
          consoleErrors,
        };

        // Keyboard-focus states at the desktop viewport only.
        if (vp.label === "1440x900") {
          entry.focus = [];
          await page.keyboard.press("Tab");
          await sleep(150);
          const f1 = await page.evaluate(describeActiveElement);
          const f1File = `${baseName}_focus1.png`;
          await page.screenshot({ path: path.join(dir, f1File) });
          entry.focus.push({ tabs: 1, ...f1, file: f1File });
          await page.keyboard.press("Tab");
          await page.keyboard.press("Tab");
          await sleep(150);
          const f3 = await page.evaluate(describeActiveElement);
          const f3File = `${baseName}_focus3.png`;
          await page.screenshot({ path: path.join(dir, f3File) });
          entry.focus.push({ tabs: 3, ...f3, file: f3File });
        }

        page.off("console", onConsole);
        page.off("pageerror", onPageError);

        if (measured.horizontalScroll || consoleErrors.length > 0) failed = true;
        captures.push(entry);
        console.log(
          `[visual] ${route.name} @ ${vp.label}: ` +
            `hScroll=${measured.horizontalScroll} clipped=${measured.clippedElements.count} ` +
            `smallFonts=${measured.smallFontElements.count} smallTargets=${measured.smallTapTargets.count} ` +
            `consoleErrors=${consoleErrors.length}`,
        );
      }
    }
  } finally {
    await browser.close();
    stopServer();
  }

  const overflowViewports = captures.filter((c) => c.measured.horizontalScroll).map((c) => `${c.route}@${c.viewport}`);
  const errorRoutes = captures.filter((c) => c.consoleErrors.length > 0).map((c) => `${c.route}@${c.viewport}`);

  const report = {
    generatedAt: new Date().toISOString(),
    base,
    chrome: path.basename(chrome),
    tool: "puppeteer-core (real headless Chromium)",
    routes: ROUTES.map((r) => r.path),
    viewports: VIEWPORTS.map((v) => v.label),
    captures,
    summary: {
      horizontalOverflow: overflowViewports,
      consoleErrors: errorRoutes,
      exitCode: failed ? 1 : 0,
    },
    needsHumanVisualReview: {
      note: "The flags above are DOM measurements, NOT a pixel/design review. Spacing, visual hierarchy, information density, interaction states, copy and brand match (design-skills + unslop signs) must be judged by a human or model looking at the actual PNGs (use *.full.png for below-fold areas).",
      perCapture: captures.map((c) => `${c.route} @ ${c.viewport}: ${c.file} (+ ${c.fullPageFile})`),
    },
  };

  await fs.writeFile(path.join(dir, "index.json"), JSON.stringify(report, null, 2) + "\n");
  console.log(`[visual] report: ${path.join(dir, "index.json")}`);
  console.log(`[visual] horizontal overflow: ${overflowViewports.length ? overflowViewports.join(", ") : "none"}`);
  console.log(`[visual] console errors: ${errorRoutes.length ? errorRoutes.join(", ") : "none"}`);
  console.log(failed ? "[visual] FAIL (exit 1)" : "[visual] PASS");
  process.exitCode = failed ? 1 : 0;
}

main().catch((err) => {
  console.error("[visual] setup failure:", err);
  process.exit(2);
});
