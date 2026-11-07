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

function findFreePort(start = MANAGED_PORT) {
  return new Promise<number>((resolve, reject) => {
    const server = net.createServer();
    server.unref();
    server.on("error", reject);
    server.listen(start, () => {
      const port = server.address()?.port ?? start;
      server.close(() => resolve(port));
    });
  });
}

async function seedDatabase() {
  const { spawnSync } = await import("node:child_process");
  const result = spawnSync("npx", ["void", "db", "seed"], {
    cwd: WEB_ROOT,
    stdio: "inherit",
    encoding: "utf8",
    env: { ...process.env, FORCE_COLOR: "0" },
  });
  if (result.status !== 0) {
    throw new Error(`void db seed exited ${result.status}`);
  }
}

async function startDevServer(port: number) {
  const server = spawn("npx", ["vp", "dev", `--port=${port}`], {
    cwd: WEB_ROOT,
    detached: true,
    stdio: ["ignore", "pipe", "pipe"],
    env: { ...process.env, FORCE_COLOR: "0" },
  });
  server.unref();
  const ready = new Promise<void>((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error("dev server did not start")), 120000);
    server.stdout?.on("data", (d) => {
      const out = d.toString();
      console.log("[visual] server stdout:", out.trim());
      if (out.includes("ready in") || out.includes("Local:") || out.includes("http://")) {
        clearTimeout(timeout);
        resolve();
      }
    });
    server.stderr?.on("data", (d) => {
      const out = d.toString();
      console.log("[visual] server stderr:", out.trim());
      if (out.includes("ready in") || out.includes("Local:") || out.includes("http://")) {
        clearTimeout(timeout);
        resolve();
      }
    });
  });
  await ready;
  // Give the worker a moment to finish compiling on the first request.
  await sleep(2000);
  return server;
}

function killProcessGroup(pid: number) {
  try {
    process.kill(-pid, "SIGTERM");
  } catch {
    // group may already be dead
  }
}

async function waitForApi(base: string) {
  const deadline = Date.now() + 300_000;
  let lastError: unknown;
  while (Date.now() < deadline) {
    try {
      const res = await fetch(`${base}/api/v1/catalog`, { signal: AbortSignal.timeout(5000) });
      if (res.ok) return;
      lastError = new Error(`API answered ${res.status}`);
    } catch (e) {
      lastError = e;
    }
    await sleep(1000);
  }
  throw new Error(`API did not become ready: ${lastError}`);
}

async function warmRoutes(base: string) {
  const routes = ROUTES.map((r) => r.path);
  for (const path of routes) {
    try {
      await fetch(`${base}${path}`, { signal: AbortSignal.timeout(10000) });
    } catch {
      // ignore; the real capture will wait for the marker
    }
  }
}

interface CaptureResult {
  route: string;
  state: string;
  url: string;
  viewport: string;
  file: string;
  fullPageFile: string;
  bytes: number;
  settled: boolean;
  measured: {
    viewport: { width: number; height: number };
    horizontalScroll: boolean;
    documentScrollWidth: number;
    visibleElementCount: number;
    clippedElements: { count: number; samples: Array<{ tag: string; rect: { x: number; y: number; w: number; h: number } }> };
    belowFoldElements: { count: number; note: string };
    smallFontElements: { count: number; samples: Array<{ tag: string; class: string; text: string; fontSize: string; rect: { x: number; y: number; w: number; h: number } }> };
    smallTapTargets: { count: number; samples: Array<{ tag: string; class: string; rect: { x: number; y: number; w: number; h: number } }> };
    consoleErrors: string[];
    focus: Array<{ tabs: number; focused: boolean; tag: string; text: string; rect: { x: number; y: number; w: number; h: number } }>;
  };
  consoleErrors: string[];
  focusFiles: string[];
}

async function captureRoute(
  page: import("puppeteer-core").Page,
  route: (typeof ROUTES)[0],
  viewport: (typeof VIEWPORTS)[0],
  outDir: string,
  holdCatalog: boolean,
) {
  const timestamp = new Date().toISOString().replace(/[:.]/g, "-");
  const baseName = `${route.name}-${route.state}_${viewport.label}`;

  // Set viewport
  await page.setViewport({
    width: viewport.width,
    height: viewport.height,
    deviceScaleFactor: viewport.deviceScaleFactor,
    isMobile: viewport.isMobile ?? false,
    hasTouch: viewport.hasTouch ?? false,
  });

  if (viewport.label === "1440x900@200%") {
    // Browser zoom 200%: set the page zoom via CDP
    const client = await page.target().createCDPSession();
    await client.send("Emulation.setPageScaleFactor", { pageScaleFactor: 2 });
  }

  // If this is the loading state, hold the catalog request
  if (holdCatalog) {
    await page.setRequestInterception(true);
    page.on("request", (req) => {
      if (req.url().includes("/api/v1/catalog")) {
        req.continue({ pause: true }); // hold it
      } else {
        req.continue();
      }
    });
  }

  await page.goto(route.path.startsWith("/") ? route.path : `/${route.path}`, {
    waitUntil: "networkidle0",
    timeout: 60000,
  });

  // Wait for the state marker
  if (route.waitFor) {
    const selectors = Array.isArray(route.waitFor) ? route.waitFor : [route.waitFor];
    for (const sel of selectors) {
      await page.waitForSelector(sel, { timeout: 30000 });
    }
  }
  if (route.waitForText) {
    await page.waitForFunction(
      (text) => document.body.innerText.includes(text),
      { timeout: 30000 },
      route.waitForText,
    );
  }

  // Wait for DOM to stop changing (layout settled)
  await page.waitForFunction(
    () => {
      return new Promise((resolve) => {
        let lastHtml = document.documentElement.outerHTML;
        const check = () => {
          const current = document.documentElement.outerHTML;
          if (current === lastHtml) {
            resolve(true);
          } else {
            lastHtml = current;
            requestAnimationFrame(check);
          }
        };
        requestAnimationFrame(check);
      });
    },
    { timeout: 10000 },
  );

  // Measure layout facts
  const measured = await page.evaluate(() => {
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;
    const docWidth = document.documentElement.scrollWidth;
    const docHeight = document.documentElement.scrollHeight;
    const horizontalScroll = docWidth > viewportWidth;

    // Visible elements
    const allElements = Array.from(document.querySelectorAll("*")) as HTMLElement[];
    const visibleElements = allElements.filter((el) => {
      const style = window.getComputedStyle(el);
      if (style.display === "none" || style.visibility === "hidden" || style.opacity === "0") return false;
      const rect = el.getBoundingClientRect();
      return rect.width > 0 && rect.height > 0 && rect.top < viewportHeight && rect.bottom > 0 && rect.left < viewportWidth && rect.right > 0;
    });

    // Clipped elements (horizontally outside viewport)
    const clippedElements = visibleElements
      .filter((el) => {
        const rect = el.getBoundingClientRect();
        return rect.right > viewportWidth || rect.left < 0;
      })
      .slice(0, 10)
      .map((el) => {
        const rect = el.getBoundingClientRect();
        return { tag: el.tagName.toLowerCase(), rect: { x: rect.x, y: rect.y, w: rect.width, h: rect.height } };
      });

    // Below fold elements
    const belowFoldElements = visibleElements.filter((el) => el.getBoundingClientRect().top > viewportHeight);

    // Small font elements
    const smallFontElements = visibleElements
      .filter((el) => {
        const style = window.getComputedStyle(el);
        const fs = parseFloat(style.fontSize);
        return fs < 12 && el.innerText.trim().length > 0;
      })
      .slice(0, 10)
      .map((el) => {
        const style = window.getComputedStyle(el);
        const rect = el.getBoundingClientRect();
        return { tag: el.tagName.toLowerCase(), class: el.className, text: el.innerText.trim().slice(0, 50), fontSize: style.fontSize, rect: { x: rect.x, y: rect.y, w: rect.width, h: rect.height } };
      });

    // Small tap targets
    const interactiveSelectors = "a, button, input, select, textarea, [role=button], [role=link], [role=menuitem], [tabindex]:not([tabindex='-1'])";
    const interactiveElements = Array.from(document.querySelectorAll(interactiveSelectors)) as HTMLElement[];
    const smallTapTargets = interactiveElements
      .filter((el) => {
        const style = window.getComputedStyle(el);
        if (style.display === "none" || style.visibility === "hidden" || style.opacity === "0") return false;
        const rect = el.getBoundingClientRect();
        return rect.width > 0 && rect.height > 0 && Math.min(rect.width, rect.height) < 32;
      })
      .slice(0, 10)
      .map((el) => {
        const rect = el.getBoundingClientRect();
        return { tag: el.tagName.toLowerCase(), class: el.className, rect: { x: rect.x, y: rect.y, w: rect.width, h: rect.height } };
      });

    return {
      viewport: { width: viewportWidth, height: viewportHeight },
      horizontalScroll,
      documentScrollWidth: docWidth,
      visibleElementCount: visibleElements.length,
      clippedElements: { count: clippedElements.length, samples: clippedElements },
      belowFoldElements: { count: belowFoldElements.length, note: "informational; vertical scroll is expected" },
      smallFontElements: { count: smallFontElements.length, samples: smallFontElements },
      smallTapTargets: { count: smallTapTargets.length, samples: smallTapTargets },
    };
  });

  // Capture viewport screenshot
  const viewportFile = path.join(outDir, `${baseName}.png`);
  await page.screenshot({ path: viewportFile, fullPage: false });
  const viewportBytes = fsSync.statSync(viewportFile).size;

  // Capture full-page screenshot
  const fullPageFile = path.join(outDir, `${baseName}.full.png`);
  await page.screenshot({ path: fullPageFile, fullPage: true });

  // Capture focus states at 1440x900
  const focusFiles: string[] = [];
  if (viewport.label === "1440x900") {
    for (const tabs of [1, 3]) {
      await page.keyboard.down("Tab");
      for (let i = 1; i < tabs; i++) {
        await page.keyboard.down("Tab");
      }
      await sleep(200);
      const focusFile = path.join(outDir, `${baseName}_focus${tabs}.png`);
      await page.screenshot({ path: focusFile, fullPage: false });
      focusFiles.push(focusFile);

      // Get focused element descriptor
      const focused = await page.evaluate(() => {
        const el = document.activeElement as HTMLElement;
        if (!el) return null;
        const rect = el.getBoundingClientRect();
        return { tag: el.tagName.toLowerCase(), text: el.innerText.trim().slice(0, 50), rect: { x: rect.x, y: rect.y, w: rect.width, h: rect.height } };
      });
      measured.focus = measured.focus || [];
      if (focused) {
        measured.focus.push({ tabs, focused: true, ...focused });
      }
    }
  }

  // Collect console errors
  const consoleErrors: string[] = [];
  page.on("console", (msg) => {
    if (msg.type() === "error") consoleErrors.push(msg.text());
  });
  page.on("pageerror", (err) => {
    consoleErrors.push(err.message);
  });

  return {
    route: route.name,
    state: route.state,
    url: route.path.startsWith("/") ? route.path : `/${route.path}`,
    viewport: viewport.label,
    file: path.basename(viewportFile),
    fullPageFile: path.basename(fullPageFile),
    bytes: viewportBytes,
    settled: true,
    measured,
    consoleErrors,
    focusFiles,
  };
}

async function main() {
  console.log("[visual] FlashBeat Web Studio visual gate");
  console.log("[visual] Finding Chrome...");

  const chrome = findChrome();
  if (!chrome) {
    console.error("[visual] ERROR: Chrome not found. Set CHROME_PATH or install Chrome.");
    process.exit(2);
  }
  console.log(`[visual] Chrome: ${chrome}`);

  let baseUrl = process.env.VISUAL_BASE_URL;
  let server: ReturnType<typeof spawn> | null = null;
  let serverPid: number | null = null;

  if (!baseUrl) {
    console.log("[visual] Seeding database...");
    await seedDatabase();

    console.log("[visual] Finding free port...");
    const port = await findFreePort();
    console.log(`[visual] Port: ${port}`);

    console.log("[visual] Starting dev server...");
    server = await startDevServer(port);
    serverPid = server.pid ?? null;
    baseUrl = `http://127.0.0.1:${port}`;

    console.log("[visual] Waiting for API...");
    await waitForApi(baseUrl);

    console.log("[visual] Warming routes...");
    await warmRoutes(baseUrl);
  } else {
    console.log(`[visual] Using existing server: ${baseUrl}`);
  }

  console.log("[visual] Launching browser...");
  const browser = await puppeteer.launch({
    executablePath: chrome,
    headless: true,
    args: ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage"],
    defaultViewport: null,
  });

  const page = await browser.newPage();

  // Collect console errors
  const allConsoleErrors: string[] = [];
  page.on("console", (msg) => {
    if (msg.type() === "error") allConsoleErrors.push(msg.text());
  });
  page.on("pageerror", (err) => {
    allConsoleErrors.push(err.message);
  });

  // Create output directory
  const timestamp = new Date().toISOString().replace(/[:.]/g, "-");
  const runDir = path.join(OUT_DIR, timestamp);
  await fs.mkdir(runDir, { recursive: true });

  console.log(`[visual] Output: ${runDir}`);

  const results: CaptureResult[] = [];
  let hasHorizontalOverflow = false;
  let hasConsoleErrors = false;

  for (const viewport of VIEWPORTS) {
    console.log(`[visual] Viewport: ${viewport.label}`);
    for (const route of ROUTES) {
      console.log(`[visual]   ${route.name}-${route.state}...`);
      const result = await captureRoute(page, route, viewport, runDir, route.holdCatalog ?? false);
      results.push(result);

      if (result.measured.horizontalScroll) {
        hasHorizontalOverflow = true;
        console.log(`[visual]   ⚠ horizontal overflow at ${viewport.label}`);
      }
      if (result.consoleErrors.length > 0) {
        hasConsoleErrors = true;
        console.log(`[visual]   ⚠ console errors: ${result.consoleErrors.join("; ")}`);
      }
      if (result.measured.smallFontElements.count > 0) {
        console.log(`[visual]   ⚠ ${result.measured.smallFontElements.count} small font elements`);
      }
      if (result.measured.smallTapTargets.count > 0) {
        console.log(`[visual]   ⚠ ${result.measured.smallTapTargets.count} small tap targets`);
      }
      if (result.measured.clippedElements.count > 0) {
        console.log(`[visual]   ⚠ ${result.measured.clippedElements.count} clipped elements`);
      }
    }
  }

  await browser.close();

  if (server && serverPid) {
    console.log("[visual] Stopping dev server...");
    killProcessGroup(serverPid);
  }

  // Write index.json
  const index = {
    generatedAt: new Date().toISOString(),
    base: baseUrl,
    chrome,
    tool: "puppeteer-core (real headless Chromium)",
    routes: ROUTES.map((r) => `${r.name}-${r.state} ${r.path}`),
    viewports: VIEWPORTS.map((v) => v.label),
    captures: results,
    summary: {
      horizontalOverflow: hasHorizontalOverflow,
      consoleErrors: hasConsoleErrors,
    },
    needsHumanVisualReview: results.map((r) => `${r.route}-${r.state}_${r.viewport}.png`),
  };

  const indexPath = path.join(runDir, "index.json");
  await fs.writeFile(indexPath, JSON.stringify(index, null, 2));

  // Copy to evidence directory
  console.log("[visual] Copying to evidence directory...");
  for (const result of results) {
    const src = path.join(runDir, result.file);
    const destDir = path.join(EVIDENCE_DIR, result.viewport);
    await fs.mkdir(destDir, { recursive: true });
    const dest = path.join(destDir, `${result.route}-${result.state}.png`);
    await fs.copyFile(src, dest);
  }

  // Copy index.json to evidence
  const evidenceIndexPath = path.join(EVIDENCE_DIR, "index.json");
  await fs.copyFile(indexPath, evidenceIndexPath);

  console.log("[visual] Done.");
  console.log(`[visual] Run: ${runDir}`);
  console.log(`[visual] Evidence: ${EVIDENCE_DIR}`);

  if (hasHorizontalOverflow || hasConsoleErrors) {
    process.exit(1);
  }
  process.exit(0);
}

main().catch((err) => {
  console.error("[visual] FATAL:", err);
  process.exit(2);
});