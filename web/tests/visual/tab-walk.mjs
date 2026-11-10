#!/usr/bin/env node
/**
 * FlashBeat Web Studio — real-browser keyboard walk (Issue #74, evidence for #75).
 *
 * Drives a REAL headless Chromium (system Chrome via puppeteer-core) and:
 *   1. walks `Tab` / `Shift+Tab` over the Studio routes, recording the focused
 *      element sequence (tag / role / aria-label / text head / visibility /
 *      focus ring),
 *   2. exercises the publish drawer focus trap: open → Tab stays inside →
 *      Escape closes → the focus returns to the trigger.
 *
 * Usage:
 *   node tests/visual/tab-walk.mjs
 *   VISUAL_BASE_URL=http://127.0.0.1:5199 node tests/visual/tab-walk.mjs
 *
 * Output: tab-walk.json (+ focus screenshots) under out/tab-walk-<timestamp>/.
 * Exit codes: 0 = walk completed (findings are data, not failures),
 * 2 = setup failure.
 */
import puppeteer from "puppeteer-core";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { findChrome, resolveBaseUrl, sleep } from "./dev-server.mjs";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const OUT_DIR = path.join(HERE, "out");

const WALK_ROUTES = [
  { name: "home", path: "/", tabs: 30 },
  { name: "chart", path: "/chart?songId=who&version=1", tabs: 40, waitFor: "canvas" },
  { name: "publish", path: "/publish", tabs: 40 },
];

const WALK_VIEWPORTS = [
  { label: "1440x900", width: 1440, height: 900, deviceScaleFactor: 1 },
  {
    label: "390x844",
    width: 390,
    height: 844,
    deviceScaleFactor: 2,
    isMobile: true,
    hasTouch: true,
  },
];

const MAX_TAB_STEPS = 60;

/** In-page description of the currently focused element. Runs in the browser. */
function describeActiveElement() {
  const el = document.activeElement;
  if (!el || el === document.body) {
    return { focused: false, reason: "focus is on <body> (left the page widgets)" };
  }
  const r = el.getBoundingClientRect();
  const s = getComputedStyle(el);
  const inViewport =
    r.width > 0 &&
    r.height > 0 &&
    r.bottom > 0 &&
    r.top < window.innerHeight &&
    r.right > 0 &&
    r.left < window.innerWidth;
  return {
    focused: true,
    tag: el.tagName.toLowerCase(),
    role: el.getAttribute("role") || undefined,
    ariaLabel: el.getAttribute("aria-label") || undefined,
    ariaExpanded: el.getAttribute("aria-expanded") || undefined,
    type: el.getAttribute("type") || undefined,
    text: (el.textContent ?? "").trim().replace(/\s+/g, " ").slice(0, 50) || undefined,
    value: el instanceof HTMLInputElement ? el.value.slice(0, 40) || undefined : undefined,
    rect: { x: Math.round(r.x), y: Math.round(r.y), w: Math.round(r.width), h: Math.round(r.height) },
    inViewport,
    focusVisible: el.matches(":focus-visible"),
    focusRing: s.outlineStyle !== "none" && s.outlineWidth !== "0px" ? s.outline : undefined,
  };
}

/** Whether the element sits inside the publish drawer panel. Runs in the browser. */
function insideDrawerPanel() {
  const el = document.activeElement;
  const panel = document.querySelector('aside[aria-label="Publish sidebar"]');
  return panel !== null && el !== null && panel.contains(el);
}

async function walkRoute(page, route, vp) {
  const url = `${process.env.VISUAL_BASE_URL?.replace(/\/$/, "") ?? "http://127.0.0.1:5199"}${route.path}`;
  await page.setViewport(vp);
  await page.goto(url, { waitUntil: "networkidle2", timeout: 60_000 });
  if (route.waitFor) {
    await page.waitForSelector(route.waitFor, { timeout: 15_000 }).catch(() => {});
  }
  await sleep(1_200);

  // Walk forward with Tab.
  const steps = [];
  let escapedToBody = 0;
  for (let i = 1; i <= Math.min(route.tabs, MAX_TAB_STEPS); i++) {
    await page.keyboard.press("Tab");
    await sleep(120);
    const d = await page.evaluate(describeActiveElement);
    if (!d.focused) escapedToBody++;
    steps.push({ tab: i, ...d });
  }
  // Walk back with Shift+Tab to prove the reverse order exists too.
  const backSteps = [];
  for (let i = 1; i <= 5; i++) {
    await page.keyboard.down("Shift");
    await page.keyboard.press("Tab");
    await page.keyboard.up("Shift");
    await sleep(120);
    backSteps.push({ shiftTab: i, ...(await page.evaluate(describeActiveElement)) });
  }

  const shotFile = `tabwalk_${route.name}_${vp.label}.png`;
  await page.screenshot({ path: shotFile }); // cwd is the run directory (set in main)

  const bodyEscapes = steps.filter((s) => !s.focused).length;
  const invisibleFocus = steps.filter((s) => s.focused && !s.inViewport).length;
  const noRing = steps.filter((s) => s.focused && !s.focusVisible).length;

  return {
    route: route.name,
    url,
    viewport: vp.label,
    tabsPressed: steps.length,
    file: shotFile,
    findings: {
      focusEscapedToBody: { count: bodyEscapes, tabs: steps.filter((s) => !s.focused).map((s) => s.tab) },
      focusOutsideViewport: { count: invisibleFocus, tabs: steps.filter((s) => s.focused && !s.inViewport).map((s) => s.tab) },
      focusVisibleNotMatched: { count: noRing, note: ":focus-visible did not match — check the global ring" },
    },
    steps,
    shiftTabSteps: backSteps,
  };
}

/**
 * Drawer focus-trap check on the publish screen: the toggle opens the drawer,
 * Tab cycles inside it, Escape closes it, the focus returns to the trigger.
 */
async function drawerTrapCheck(page) {
  const url = `${process.env.VISUAL_BASE_URL?.replace(/\/$/, "") ?? "http://127.0.0.1:5199"}/publish`;
  await page.setViewport({ width: 390, height: 844, deviceScaleFactor: 2, isMobile: true, hasTouch: true });
  await page.goto(url, { waitUntil: "networkidle2", timeout: 60_000 });
  await sleep(1_200);

  const result = { route: "publish", viewport: "390x844", scenario: "drawer focus trap" };

  // Focus the toggle via Tab walk (it is the header control) — click would not
  // prove keyboard reachability.
  const toggleSelector = 'button[aria-label="Open the publish sidebar"]';
  await page.evaluate((sel) => {
    const btn = document.querySelector(sel);
    if (!btn) throw new Error(`toggle not found: ${sel}`);
    btn.focus();
  }, toggleSelector);
  const reached = await page.evaluate(describeActiveElement);
  result.toggleReachedByFocus = reached;

  await page.keyboard.press("Enter");
  await sleep(400);
  result.afterOpen = {
    activeElement: await page.evaluate(describeActiveElement),
    insideDrawer: await page.evaluate(insideDrawerPanel),
  };

  const trap = [];
  for (let i = 1; i <= 12; i++) {
    await page.keyboard.press("Tab");
    await sleep(120);
    trap.push({ tab: i, ...(await page.evaluate(describeActiveElement)), insideDrawer: await page.evaluate(insideDrawerPanel) });
  }
  result.trapTabs = trap;
  result.trapHeld = trap.every((t) => t.insideDrawer);

  await page.screenshot({ path: "tabwalk_publish_390x844_drawer.png" });

  await page.keyboard.press("Escape");
  await sleep(400);
  result.afterEscape = {
    activeElement: await page.evaluate(describeActiveElement),
    drawerStillInDom: await page.evaluate(() => document.querySelector('aside[aria-label="Publish sidebar"]') !== null),
    drawerClosed: await page.evaluate(
      () => document.querySelector('aside[aria-label="Publish sidebar"][role="dialog"]') === null,
    ),
  };
  result.focusRestoredToTrigger =
    result.afterEscape.activeElement.focused &&
    result.afterEscape.activeElement.ariaLabel === "Open the publish sidebar";

  await page.screenshot({ path: "tabwalk_publish_390x844_after_escape.png" });
  return result;
}

async function main() {
  const chrome = findChrome();
  if (!chrome) {
    console.error("[tab-walk] no Chrome/Chromium found; set CHROME_PATH");
    process.exit(2);
  }
  let base;
  let stopServer = () => {};
  try {
    ({ base, stop: stopServer } = await resolveBaseUrl());
  } catch (err) {
    console.error(`[tab-walk] ${err.message}`);
    process.exit(2);
  }
  process.env.VISUAL_BASE_URL = base;
  console.log(`[tab-walk] chrome: ${chrome}`);

  const stamp = new Date().toISOString().replace(/[:.]/g, "-");
  const dir = path.join(OUT_DIR, `tab-walk-${stamp}`);
  await fs.mkdir(dir, { recursive: true });
  process.chdir(dir); // screenshots of the walk land next to the report

  const browser = await puppeteer.launch({
    executablePath: chrome,
    headless: true,
    args: ["--disable-gpu", "--no-first-run", "--disable-extensions"],
  });

  const walks = [];
  let drawer = null;
  try {
    const page = await browser.newPage();
    for (const route of WALK_ROUTES) {
      for (const vp of WALK_VIEWPORTS) {
        const walk = await walkRoute(page, route, vp);
        walks.push(walk);
        console.log(
          `[tab-walk] ${route.name} @ ${vp.label}: tabs=${walk.steps.length} ` +
            `bodyEscapes=${walk.findings.focusEscapedToBody.count} ` +
            `outsideViewport=${walk.findings.focusOutsideViewport.count} ` +
            `noFocusRing=${walk.findings.focusVisibleNotMatched.count}`,
        );
      }
    }
    drawer = await drawerTrapCheck(page);
    console.log(
      `[tab-walk] drawer trap: held=${drawer.trapHeld} ` +
        `focusRestored=${drawer.focusRestoredToTrigger}`,
    );
  } finally {
    await browser.close();
    stopServer();
  }

  const report = {
    generatedAt: new Date().toISOString(),
    base,
    chrome: path.basename(chrome),
    tool: "puppeteer-core (real headless Chromium)",
    method:
      "Tab / Shift+Tab pressed on the real page; document.activeElement recorded per step. " +
      "inViewport = the focused element has a non-zero rect intersecting the viewport. " +
      "focusVisible = the element matches :focus-visible (the global ring from app.css).",
    walks,
    drawerTrap: drawer,
  };
  await fs.writeFile(path.join(dir, "tab-walk.json"), JSON.stringify(report, null, 2) + "\n");
  console.log(`[tab-walk] report: ${path.join(dir, "tab-walk.json")}`);
}

main().catch((err) => {
  console.error("[tab-walk] setup failure:", err);
  process.exit(2);
});
