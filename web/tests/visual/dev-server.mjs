/**
 * Shared helpers for the real-browser visual scripts (Issue #75 / #74):
 * Chrome discovery and a managed local dev server (seeded D1, port 5199).
 */
import { spawn, execFileSync } from "node:child_process";
import fsSync from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
export const WEB_ROOT = path.resolve(HERE, "../..");
export const MANAGED_PORT = 5199;

export function findChrome() {
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

export function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

export async function waitForServer(base, timeoutMs) {
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

export async function startDevServer() {
  const env = {
    ...process.env,
    FLASHBEAT_ADMIN_TOKEN: process.env.FLASHBEAT_ADMIN_TOKEN ?? "local-dev-only",
  };
  console.log("[visual] seeding local D1 (void db seed)…");
  execFileSync("npx", ["void", "db", "seed"], {
    cwd: WEB_ROOT,
    env,
    stdio: "inherit",
    shell: process.platform === "win32",
  });
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

/** Resolves `VISUAL_BASE_URL` or boots the managed dev server. */
export async function resolveBaseUrl() {
  const base = process.env.VISUAL_BASE_URL?.replace(/\/$/, "");
  if (base) {
    console.log(`[visual] using provided base URL ${base}`);
    if (!(await waitForServer(base, 10_000))) {
      throw new Error(`${base} is not responding`);
    }
    return { base, stop: () => {} };
  }
  return startDevServer();
}
