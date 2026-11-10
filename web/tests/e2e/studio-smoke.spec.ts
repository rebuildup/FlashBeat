import { spawn, type ChildProcess } from "node:child_process";
import { existsSync } from "node:fs";
import path from "node:path";
import { createServer, type AddressInfo } from "node:net";
import { chromium, expect, test } from "@playwright/test";

/**
 * Studio main screen smoke check.
 *
 * `npm run test:studio` の他 spec は既に起動済みの dev server を前提とするが、
 * この spec は `vp dev` を自分で起動して Studio の main 画面がレンダリング
 * されることだけを確認する (CI やローカルの単独実行用)。
 *
 * ポートはその時点で空いているものを動的に選ぶ (`--strictPort` で固定)。
 * 同一マシンで別 worktree の dev server が定番ポートを握っていても影響しない。
 *
 * ブラウザがインストールされていない / dev server が起動しない環境では
 * fail せず skip する。
 */

const WEB_ROOT = path.resolve(import.meta.dirname, "../..");

/** 空いているポートを 1 つ確保して返す (競合はあるが実用上十分)。 */
async function findFreePort(): Promise<number> {
  const server = createServer();
  const listening = Promise.withResolvers<void>();
  server.listen(0, "127.0.0.1", listening.resolve);
  await listening.promise;
  const port = (server.address() as AddressInfo).port;
  const closed = Promise.withResolvers<void>();
  server.close(() => closed.resolve());
  await closed.promise;
  return port;
}

function browserAvailable(): boolean {
  try {
    // `chromium.executablePath()` はブラウザ未インストールでもパスを返すので、
    // 実在確認まで行う。
    existsSync(chromium.executablePath());
    return true;
  } catch {
    return false;
  }
}

/** 起動した dev server。afterAll で落とす。 */
let devServer: ChildProcess | null = null;
let base = "";

test.beforeAll(async () => {
  test.skip(!browserAvailable(), "Playwright browser is not installed");

  const port = await findFreePort();
  base = `http://127.0.0.1:${port}`;
  devServer = spawn(
    "npx",
    ["vp", "dev", "--port", String(port), "--host", "127.0.0.1", "--strictPort"],
    {
      cwd: WEB_ROOT,
      // `void` の Worker は token 系 env が無いと起動時に警告するため、
      // ローカル用のダミーを渡す (実通信は発生しない)。
      env: {
        ...process.env,
        FLASHBEAT_ADMIN_TOKEN: process.env.FLASHBEAT_ADMIN_TOKEN ?? "local-dev-only",
      },
      stdio: "ignore",
    },
  );

  // サーバが応答するまで待つ (起動しなければテスト側で skip)。
  const deadline = Date.now() + 60_000;
  while (devServer.exitCode === null && Date.now() < deadline) {
    try {
      await fetch(`${base}/api/v1/catalog`);
      return;
    } catch {
      await new Promise((r) => setTimeout(r, 500));
    }
  }
});

test.afterAll(async () => {
  devServer?.kill("SIGTERM");
});

test("studio main screen renders", async ({ page }) => {
  test.skip(devServer === null || devServer.exitCode !== null, "dev server did not start");
  await page.goto(base);
  await expect(page.locator("#studio-content")).toBeVisible();
});
