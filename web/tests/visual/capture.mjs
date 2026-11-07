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
 *   npm run visual                     # seeds local D1, starts a dev server on a free port
 *   VISUAL_BASE_URL=http://127.0.0.1:5173 npm run visual
 *   CHROME_PATH=/path/to/chrome npm run visual
 *
 * Exit codes: 0 = clean, 1 = horizontal overflow and/or console errors found
 * (also written to index.json), 2 = setup failure (no Chrome, server never ready).
 */
import puppeteer from "puppeteer-core";
import { spawn, execFileSync } from "node:child_process";
import net from "node:net";
import fs from "node:fs/promises";
import fsSync from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const WEB_ROOT = path.resolve(HERE, "../..");
const OUT_DIR = path.join(HERE, "out");
const MANAGED_PORT = 5199;

/**
 * PR-facing evidence directory. The viewport screenshots are copied here as
 * `<viewport>/<route>-<state>.png` so the pull request can be reviewed from
 * the images alone; the raw run (including full-page shots and the focus
 * states) stays in `web/tests/visual/out/`.
 */
const EVIDENCE_DIR = path.resolve(
  WEB_ROOT,
  process.env.VISUAL_EVIDENCE_DIR ?? "../specs/075-visual-gate/evidence",
);

/**
 * Routes and the states of the Studio that have to be reviewed.
 * `state` becomes part of the file name (`<route>-<state>.png`).
 * `waitFor` is the marker that proves the state is really on screen: the
 * capture waits for it before it reads the pixels, so a screenshot can never
 * show the loading state instead of the state it is named after.
 */
const ROUTES = [
  { name: "home", state: "ready", path: "/", waitFor: "a[data-row-link]" },
  { name: "home", state: "error", path: "/?state=error", waitForText: "Catalog unavailable" },
  { name: "home", state: "empty", path: "/?state=empty", waitForText: "No songs published yet" },
  // The catalog request is held so the loading state is on screen.
  { name: "home", state: "loading", path: "/", waitForText: "Loading catalog", holdCatalog: true },
  { name: "chart", state: "editor", path: "/chart?songId=who&version=1", waitFor: ["canvas", ".ytp-player"], waitForText: "Review & Publish" },
  { name: "chart", state: "empty", path: "/chart", waitForText: "Import" },
  { name: "publish", state: "song", path: "/publish?songId=who&version=1", waitFor: ".ytp-player", waitForText: "Publish a song" },
  { name: "publish", state: "empty", path: "/publish", waitForText: "Publish a song" },
];

/**
 * Viewports. `1440x900@200%` is a 1440x900 window with the browser zoom set
 * to 200%: the CSS viewport becomes 720x450 and the device pixel ratio 2, so
 * the screenshot keeps the 1440x900 device pixel size of the window while the
 * layout is the one a zoomed-in user really sees.
 */
const VIEWPORTS = [
  { label: "1440x900", width: 1440, height: 900, deviceScaleFactor: 1 },
  { label: "1280x720", width: 1280, height: 720, deviceScaleFactor: 1 },
  { label: "1024x768", width: 1024, height: 768, deviceScaleFactor: 1 },
  { label: "390x844", width: 390, height: 844, deviceScaleFactor: 2, isMobile: true, hasTouch: true },
  { label: "1440x900@200%", width: 720, height: 450, deviceScaleFactor: 2 },
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
    "C:/Program Files/Chrome/Application/chrome.exe",
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

/**
 * Wait until the dev server answers the catalog API.
 *
 * The first request of a cold dev server compiles the worker bundle and takes
 * ~50s, so every request gets a generous timeout of its own; the server is
 * only "ready" when the API really answers.
 */
async function waitForServer(base, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  let last = "no attempt";
  while (Date.now() < deadline) {
    try {
      const res = await fetch(`${base}/api/v1/catalog`, {
        signal: AbortSignal.timeout(120_000),
      });
      last = `HTTP ${res.status}`;
      if (res.ok) return true;
    } catch (error) {
      last = String(error).slice(0, 120);
    }
    await sleep(1000);
  }
  console.error(`[visual] server not ready: ${last}`);
  return false;
}

/**
 * Fetch every route once before the capture. The dev server compiles a route
 * on its first request; without the warm-up the first puppeteer navigation
 * would race the compilation and time out.
 */
async function warmUpRoutes(base, routes) {
  for (const route of routes) {
    const url = `${base}${route.path}`;
    const deadline = Date.now() + 180_000;
    let status = "no attempt";
    while (Date.now() < deadline) {
      try {
        const res = await fetch(url, { signal: AbortSignal.timeout(120_000) });
        status = `HTTP ${res.status}`;
        if (res.status < 500) break;
      } catch (error) {
        status = String(error).slice(0, 120);
      }
      await sleep(1000);
    }
    console.log(`[visual] warm-up ${route.name}-${route.state}: ${status}`);
  }
}

/**
 * Find a free TCP port. A stale dev server of another worktree on the
 * preferred port would answer the captures with the wrong application, so the
 * capture always binds a port it verified to be free.
 */
async function findFreePort(start) {
  for (let port = start; port < start + 50; port += 1) {
    const free = await new Promise((resolve) => {
      const server = net.createServer();
      server.once("error", () => resolve(false));
      server.once("listening", () => server.close(() => resolve(true)));
      server.listen(port, "127.0.0.1");
    });
    if (free) return port;
  }
  throw new Error(`no free port in ${start}..${start + 50}`);
}

async function startDevServer() {
  const env = {
    ...process.env,
    FLASHBEAT_ADMIN_TOKEN: process.env.FLASHBEAT_ADMIN_TOKEN ?? "local-dev-only",
  };
  console.log("[visual] seeding local D1 (void db seed)…");
  execFileSync("npx", ["void", "db", "seed"], { cwd: WEB_ROOT, env, stdio: "inherit", shell: process.platform === "win32" });
  const port = await findFreePort(MANAGED_PORT);
  const base = `http://127.0.0.1:${port}`;
  console.log(`[visual] starting dev server at ${base}…`);
  // `detached` puts the dev server in its own process group: killing the group
  // also kills the vite process behind `npx`, which otherwise survives and
  // keeps the port occupied.
  const child = spawn("npx", ["vp", "dev", "--port", String(port), "--host", "127.0.0.1"], {
    cwd: WEB_ROOT,
    env,
    stdio: "ignore",
    shell: process.platform === "win32",
    detached: process.platform !== "win32",
  });
  const stop = () => {
    try {
      if (process.platform === "win32") child.kill("SIGTERM");
      else process.kill(-child.pid, "SIGTERM");
    } catch {
      /* already gone */
    }
  };
  if (!(await waitForServer(base, 300_000))) {
    stop();
    throw new Error(`dev server did not become ready at ${base}`);
  }
  return { base, stop };
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

/**
 * Wait until the DOM stopped changing. The dev server delivers the client
 * bundle module by module, so a fixed sleep captures the loading state of a
 * screen that is already settled. The signature is the text plus the markup
 * length of the body; three equal readings in a row mean the page is done.
 */
async function waitForStableDom(page, timeoutMs = 30_000) {
  const deadline = Date.now() + timeoutMs;
  let previous = null;
  let stable = 0;
  while (Date.now() < deadline) {
    const signature = await page.evaluate(
      () => `${document.body.innerText.length}:${document.body.innerHTML.length}`,
    );
    if (signature === previous) {
      stable += 1;
      if (stable >= 3) return true;
    } else {
      stable = 0;
      previous = signature;
    }
    await sleep(250);
  }
  return false;
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
    if (!(await waitForServer(base, 120_000))) {
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

  await warmUpRoutes(base, ROUTES);

  const browser = await puppeteer.launch({
    executablePath: chrome,
    headless: true,
    args: ["--disable-gpu", "--no-first-run", "--disable-extensions", "--hide-scrollbars"],
  });

  const captures = [];
  let failed = false;

  try {
    const page = await browser.newPage();

    /**
     * The catalog request can be held per route so a transient state (the
     * loading state of the catalog panel) is on screen when the pixels are
     * read. The held request is released right after the viewport screenshot,
     * so the full-page shot of that route shows the settled state.
     * Every other request is passed straight through.
     */
    let holdCatalog = false;
    let releaseCatalog = null;
    let catalogRequestSeen = null;
    let markCatalogSeen = null;
    await page.setRequestInterception(true);
    page.on("request", (request) => {
      if (holdCatalog && request.url().includes("/api/v1/catalog")) {
        markCatalogSeen?.();
        releaseCatalog = () => {
          releaseCatalog = null;
          void request.continue();
        };
        return;
      }
      void request.continue();
    });

    // The viewport is the outer loop: `page.setViewport` makes puppeteer
    // reload the page, so switching the viewport for every capture would load
    // every route twice. One viewport switch, then one navigation per route.
    for (const vp of VIEWPORTS) {
      await page.setDefaultNavigationTimeout(120_000);
      await page.setDefaultTimeout(60_000);
      try {
        await page.setViewport(vp);
      } catch (error) {
        // The reload puppeteer does for the viewport change can time out on a
        // heavy page; the metrics themselves are already applied.
        console.log(`[visual] viewport ${vp.label}: reload timeout ignored (${String(error).slice(0, 80)})`);
      }
      for (const route of ROUTES) {
        const consoleErrors = [];
        const onConsole = (msg) => {
          if (msg.type() === "error") consoleErrors.push(msg.text().slice(0, 300));
        };
        const onPageError = (err) => consoleErrors.push(String(err).slice(0, 300));
        page.on("console", onConsole);
        page.on("pageerror", onPageError);

        holdCatalog = route.holdCatalog === true;
        if (holdCatalog) {
          catalogRequestSeen = new Promise((resolve) => {
            markCatalogSeen = resolve;
          });
        } else {
          catalogRequestSeen = null;
          markCatalogSeen = null;
        }
        const url = `${base}${route.path}`;
        await page.goto(url, { waitUntil: "domcontentloaded", timeout: 120_000 });
        if (route.waitFor) {
          const selectors = Array.isArray(route.waitFor) ? route.waitFor : [route.waitFor];
          for (const selector of selectors) {
            await page.waitForSelector(selector, { timeout: 60_000 }).catch(() => {});
          }
        }
        if (route.waitForText) {
          await page
            .waitForFunction(
              (text) => (document.body.innerText ?? "").includes(text),
              { timeout: 60_000 },
              route.waitForText,
            )
            .catch(() => {});
        }
        // Give the client-side render (React + Konva + the API round trip) time
        // to settle before the pixels are read.
        await sleep(1000);
        if (catalogRequestSeen) {
          // The catalog request of the held route has to be in flight before
          // the loading state is measured.
          await Promise.race([catalogRequestSeen, sleep(5000)]);
          await sleep(500);
        }
        const settled = await waitForStableDom(page);

        const measured = await page.evaluate(measurePage);
        const baseName = `${route.name}-${route.state}_${vp.label.replace(/[^0-9x@%]/g, "")}`;
        const shot = path.join(dir, `${baseName}.png`);
        await page.screenshot({ path: shot }); // viewport shot — what the user actually sees
        // The held catalog request is released here: the viewport shot above is
        // the transient state, the full-page shot below is the settled state.
        if (releaseCatalog) {
          releaseCatalog();
          await sleep(1500);
        }
        const fullShot = path.join(dir, `${baseName}.full.png`);
        await page.screenshot({ path: fullShot, fullPage: true }); // for human review of below-fold

        const entry = {
          route: route.name,
          state: route.state,
          url,
          viewport: vp.label,
          file: path.basename(shot),
          fullPageFile: path.basename(fullShot),
          bytes: (await fs.stat(shot)).size,
          settled,
          measured,
          consoleErrors,
        };
        if (route.holdCatalog) {
          entry.note = "viewport shot shows the held loading state; the full-page shot is taken after the catalog request was released";
        }

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
          `[visual] ${route.name}-${route.state} @ ${vp.label}: ` +
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

  const overflowViewports = captures.filter((c) => c.measured.horizontalScroll).map((c) => `${c.route}-${c.state}@${c.viewport}`);
  const errorRoutes = captures.filter((c) => c.consoleErrors.length > 0).map((c) => `${c.route}-${c.state}@${c.viewport}`);

  const report = {
    generatedAt: new Date().toISOString(),
    base,
    chrome: path.basename(chrome),
    tool: "puppeteer-core (real headless Chromium)",
    routes: ROUTES.map((r) => `${r.name}-${r.state} ${r.path}`),
    viewports: VIEWPORTS.map((v) => v.label),
    captures,
    summary: {
      horizontalOverflow: overflowViewports,
      consoleErrors: errorRoutes,
      exitCode: failed ? 1 : 0,
    },
    needsHumanVisualReview: {
      note: "The flags above are DOM measurements, NOT a pixel/design review. Spacing, visual hierarchy, information density, interaction states, copy and brand match (design-skills + unslop signs) must be judged by a human or model looking at the actual PNGs (use *.full.png for below-fold areas).",
      perCapture: captures.map((c) => `${c.route}-${c.state} @ ${c.viewport}: ${c.file} (+ ${c.fullPageFile})`),
    },
  };

  await fs.writeFile(path.join(dir, "index.json"), JSON.stringify(report, null, 2) + "\n");
  console.log(`[visual] report: ${path.join(dir, "index.json")}`);

  // Copy the viewport screenshots into the PR-facing evidence directory.
  const evidence = [];
  for (const capture of captures) {
    const target = path.join(EVIDENCE_DIR, capture.viewport, `${capture.route}-${capture.state}.png`);
    await fs.mkdir(path.dirname(target), { recursive: true });
    await fs.copyFile(path.join(dir, capture.file), target);
    evidence.push({
      viewport: capture.viewport,
      route: capture.route,
      state: capture.state,
      file: path.relative(path.resolve(WEB_ROOT, ".."), target),
      raw: path.relative(path.resolve(WEB_ROOT, ".."), path.join(dir, capture.file)),
      measured: capture.measured,
      consoleErrors: capture.consoleErrors,
    });
  }
  const evidenceReport = { ...report, evidence };
  await fs.writeFile(path.join(EVIDENCE_DIR, "index.json"), JSON.stringify(evidenceReport, null, 2) + "\n");
  console.log(`[visual] evidence: ${evidence.length} images in ${EVIDENCE_DIR}`);

  console.log(`[visual] horizontal overflow: ${overflowViewports.length ? overflowViewports.join(", ") : "none"}`);
  console.log(`[visual] console errors: ${errorRoutes.length ? errorRoutes.join(", ") : "none"}`);
  console.log(failed ? "[visual] FAIL (exit 1)" : "[visual] PASS");
  process.exitCode = failed ? 1 : 0;
}

main().catch((err) => {
  console.error("[visual] setup failure:", err);
  process.exit(2);
});
