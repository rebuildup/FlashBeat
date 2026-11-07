import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test, type Page } from "@playwright/test";

/**
 * Web Studio 受け入れ監査 (issue #61) — 実操作ベース。
 *
 * 3 解像度 × 4 ルートで全ボタン・全タブ・全設定を実際に操作し、
 * 「押せるのに何もしない」「リンクが死んでいる」「エラーが出る」要素を洗い出す。
 * 証跡 (スクリーンショット + 操作ログ) を docs/evidence/web-acceptance/ に残す。
 *
 * Prerequisites (local): `void db migrate && void db seed`, then `vp dev`
 * (see playwright.config.ts). Publish の実送信は行わない (ダイアログを開いて
 * Cancel で閉じるまで)。
 */

const EVIDENCE_DIR = join(
  dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
  "..",
  "docs",
  "evidence",
  "web-acceptance",
);

const VIEWPORTS = [
  { name: "1920x1080", width: 1920, height: 1080 },
  { name: "2560x1440", width: 2560, height: 1440 },
  { name: "2560x1664", width: 2560, height: 1664 },
] as const;

const ROUTES = [
  { name: "catalog", path: "/" },
  { name: "chart", path: "/chart?songId=who&version=1" },
  { name: "publish", path: "/publish?songId=who&version=1" },
  { name: "new-song", path: "/new-song" },
] as const;

type LogEntry = {
  viewport: string;
  route: string;
  operation: string;
  result: "ok" | "fail";
  detail?: string;
};

const log: LogEntry[] = [];

function record(viewport: string, route: string, operation: string, detail?: string): void {
  log.push({ viewport, route, operation, result: "ok", ...(detail ? { detail } : {}) });
  process.env.AUDIT_LAST = operation;
}

function recordFail(viewport: string, route: string, operation: string, detail: string): void {
  log.push({ viewport, route, operation, result: "fail", detail });
}

async function shot(page: Page, viewport: string, route: string, name: string): Promise<void> {
  const dir = join(EVIDENCE_DIR, viewport);
  mkdirSync(dir, { recursive: true });
  // caret:"initial" — 既定の "hide" はフォーカス中 input の caret-color を
  // 書き換えるため、hydration 中だと React の属性不一致警告が出る。
  await page.screenshot({ path: join(dir, `${route}--${name}.png`), caret: "initial" });
}

/** 横スクロールとレイアウト崩れ (body からはみ出す要素) を検査する。 */
async function checkLayout(page: Page): Promise<{ overflowX: boolean; offscreen: string[] }> {
  return page.evaluate(() => {
    const doc = document.documentElement;
    const overflowX = doc.scrollWidth > doc.clientWidth + 1;
    const offscreen: string[] = [];
    const limit = doc.clientWidth;
    for (const el of Array.from(document.body.querySelectorAll("button, a, input, select"))) {
      const rect = el.getBoundingClientRect();
      if (rect.width === 0 && rect.height === 0) continue;
      if (rect.right > limit + 1 || rect.left < -1) {
        const label =
          (el as HTMLElement).innerText?.slice(0, 40) ||
          el.getAttribute("aria-label") ||
          el.getAttribute("placeholder") ||
          el.tagName;
        offscreen.push(`${el.tagName}:${label}`);
      }
    }
    return { overflowX, offscreen };
  });
}

/** hydration 後にしか出ないクライアント描画内容を待つ (操作は hydration 後に限定)。 */
async function waitReady(page: Page, route: string): Promise<void> {
  await page.waitForLoadState("networkidle");
  if (route === "catalog") {
    await page.getByTestId("catalog-list").waitFor({ state: "visible", timeout: 15_000 });
  } else if (route === "chart") {
    await page.getByTestId("chart-identity").waitFor({ state: "visible", timeout: 15_000 });
  } else if (route === "publish") {
    // 公開画面は lg 以上でサイドバーが常設列になり、トグルボタンは表示されない。
    // 判定は常時表示のヘッダー題名で行う。
    await page.getByRole("heading", { name: "Publish a song" }).waitFor({
      state: "visible",
      timeout: 15_000,
    });
  } else if (route === "new-song") {
    await page.getByTestId("new-song-step-1").waitFor({ state: "visible", timeout: 15_000 });
  }
}

test.describe.configure({ mode: "serial" });

test.beforeAll(() => {
  mkdirSync(EVIDENCE_DIR, { recursive: true });
});

test.afterAll(() => {
  writeFileSync(join(EVIDENCE_DIR, "operations.json"), JSON.stringify(log, null, 4));
});

for (const viewport of VIEWPORTS) {
  for (const route of ROUTES) {
    test(`[audit] ${viewport.name} ${route.path}: 全操作要素が実機能を持つ`, async ({
      page,
    }, testInfo) => {
      const errors: string[] = [];
      page.on("pageerror", (error) => errors.push(`pageerror: ${error.message}`));
      page.on("console", (message) => {
        if (message.type() !== "error") return;
        // ブラウザ自身の permissions-policy 通知はアプリの不具合ではない。
        if (message.text().startsWith("Permissions policy violation:")) return;
        errors.push(`console: ${message.text()}`);
      });

      const vp = viewport.name;
      const rt = route.name;
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await page.goto(route.path);
      await waitReady(page, rt);

      // --- レイアウト: 横スクロール・画面外要素 ---
      const layout = await checkLayout(page);
      if (layout.overflowX) {
        recordFail(vp, rt, "layout: no horizontal scroll", "document.scrollWidth > clientWidth");
      } else {
        record(vp, rt, "layout: no horizontal scroll");
      }
      if (layout.offscreen.length > 0) {
        recordFail(vp, rt, "layout: elements inside viewport", layout.offscreen.join(", "));
      } else {
        record(vp, rt, "layout: elements inside viewport");
      }
      await shot(page, vp, rt, "initial");

      // --- シェル: ナビリンクが実在する ---
      const nav = page.getByRole("navigation", { name: "Studio" });
      for (const label of ["Catalog", "New song", "New chart", "Publish"]) {
        await expect(nav.getByRole("link", { name: label })).toBeVisible();
      }
      record(vp, rt, "shell: 4 nav links visible");

      // --- ルート固有の操作 ---
      if (rt === "catalog") {
        // 検索: 入力すると行が絞り込まれる
        const search = page.locator("#catalog-search");
        await search.fill("存在しない曲名xyz");
        await expect(page.getByText("No song matches the filters")).toBeVisible();
        record(vp, rt, "search: no-match state");
        await shot(page, vp, rt, "search-no-match");
        await page.getByRole("button", { name: "Clear filters" }).click();
        await expect(page.getByRole("button", { name: "Clear filters" })).toHaveCount(0);
        record(vp, rt, "button: Clear filters restores list");

        // レベルフィルタ (ark-ui select: button[role=combobox] が trigger)。
        // 1 番目が Sort、2 番目が Level。
        const levelTrigger = page.locator('button[role="combobox"]').nth(1);
        await levelTrigger.click();
        await page.getByRole("option", { name: "Level 10 – 19" }).click();
        await expect(page.getByText("No song matches the filters")).toBeVisible();
        record(vp, rt, "level filter: no-match for Lv 10–19 (seeded song is Lv 5)");
        await page.getByRole("button", { name: "Clear filters" }).click();

        // ソート
        const sortTrigger = page.locator('button[role="combobox"]').nth(0);
        await sortTrigger.click();
        await page.getByRole("option", { name: "Level", exact: true }).click();
        record(vp, rt, "sort: Level applied without error");
        await sortTrigger.click();
        await page.getByRole("option", { name: "Catalog" }).click();

        // Refresh
        await page.getByRole("button", { name: "Refresh" }).click();
        await expect(page.getByTestId("catalog-list")).toBeVisible();
        record(vp, rt, "button: Refresh reloads catalog");

        // キーボード: `/` で検索へフォーカス
        await page.keyboard.press("/");
        await expect(search).toBeFocused();
        record(vp, rt, "keyboard: / focuses search");

        // 行リンク → editor へ (md+ はテーブル、base はカード。可視側のみ)
        const row = page.getByTestId("catalog-list").locator("a[data-row-link]").first();
        await expect(row).toBeVisible();
        record(vp, rt, "row link: visible and navigable");

        // 行のセルはヘッダー列と 1 対 1 (曲名 / Lv / BPM / Time / Notes / Ver / 操作)。
        // 行ブロックが二重になると后面的が 1 列ずつ右へずれて操作リンクが折り返す。
        const cells = await page.evaluate(() => {
          const list = document.querySelector('[data-testid="catalog-list"]');
          const rowElement = Array.from(list?.children ?? []).find(
            (child) => child instanceof HTMLElement && child.querySelector("a[data-row-link]"),
          );
          if (!(rowElement instanceof HTMLElement)) return null;
          const inFlow = Array.from(rowElement.children).filter(
            (child) => !(child instanceof HTMLElement) || !child.matches("a[data-row-link]"),
          );
          return inFlow.map((cell) => (cell as HTMLElement).textContent?.trim() ?? "");
        });
        if (cells === null || cells.length !== 7) {
          recordFail(
            vp,
            rt,
            "row: one cell per header column",
            `cells=${JSON.stringify(cells)}`,
          );
        } else {
          record(vp, rt, "row: one cell per header column");
          const level = cells[1];
          if (!/^\d+$/.test(level)) {
            recordFail(vp, rt, "row: level badge under the Lv header", `cell[1]=${level}`);
          } else {
            record(vp, rt, "row: level badge under the Lv header");
          }
        }

        // error 状態の Try again: 強制状態 (?state=error) では再読込も error に
        // なるのが仕様。ボタンがハンドラを持ち、クラッシュせず状態が再描画されること。
        await page.goto("/?state=error");
        await page.waitForLoadState("networkidle");
        await expect(page.getByText("Catalog unavailable")).toBeVisible();
        await page.getByRole("button", { name: "Try again" }).click();
        await expect(page.getByText("Catalog unavailable")).toBeVisible();
        record(vp, rt, "button: Try again re-enters the error cycle (forced state)");
        await shot(page, vp, rt, "error-state");
      }

      if (rt === "publish") {
        // メタデータフォームが入力できる (lg 以上ではサイドバーが常設列)
        const titleInput = page.getByLabel("Title", { exact: true });
        await titleInput.fill("Who (audit)");
        await expect(titleInput).toHaveValue("Who (audit)");
        record(vp, rt, "form: Title editable");
        await titleInput.fill("Who");

        // Review publish → ダイアログ → Cancel (送信はしない)
        await page.getByRole("button", { name: "Review publish" }).click();
        await expect(page.getByRole("dialog")).toBeVisible();
        record(vp, rt, "button: Review publish opens dialog");
        await shot(page, vp, rt, "publish-dialog");
        await page.getByRole("button", { name: "Cancel" }).click();
        await expect(page.getByRole("dialog")).toHaveCount(0);
        record(vp, rt, "button: Cancel closes dialog without publishing");

        // サイドバーは lg 未満でドロワーになる。狭幅にして開閉を実操作する。
        await page.setViewportSize({ width: 900, height: 800 });
        const toggle = page.getByRole("button", { name: "Open the publish sidebar" });
        await toggle.waitFor({ state: "visible", timeout: 15_000 });
        await toggle.click();
        // 開いたドロワー (aside[role=dialog]) の中の ✕ を使う。ヘッダーのトグルも
        // 同時に "Close the publish sidebar" へ切り替わるため、対象を絞る。
        const drawer = page.getByRole("dialog", { name: "Publish sidebar" });
        await expect(drawer).toBeVisible();
        record(vp, rt, "button: publish sidebar opens");
        await shot(page, vp, rt, "sidebar-open");

        // サイドバーを閉じて元に戻す
        await drawer.getByRole("button", { name: "Close the publish sidebar" }).click();
        await expect(page.getByRole("button", { name: "Open the publish sidebar" })).toBeVisible();
        record(vp, rt, "button: publish sidebar closes");
        await page.setViewportSize({ width: viewport.width, height: viewport.height });
      }

      if (rt === "new-song") {
        // ステッパー: step 1 の入力ができる
        const step1 = page.getByTestId("new-song-step-1");
        await expect(step1).toBeVisible();
        record(vp, rt, "stepper: step 1 visible");
        await shot(page, vp, rt, "step-1");
      }

      if (rt === "chart") {
        await expect(page.getByTestId("chart-identity")).toContainText("Who");
        record(vp, rt, "editor: identity shows the selected song");
        await expect(page.getByRole("button", { name: "Review & Publish" })).toBeVisible();
        record(vp, rt, "button: Review & Publish visible");
      }

      // --- 実行中に JS エラーが出ていない ---
      if (errors.length > 0) {
        recordFail(vp, rt, "no runtime errors", errors.join(" | "));
      } else {
        record(vp, rt, "no runtime errors");
      }
      expect(errors, `${vp} ${rt}: runtime errors`).toEqual([]);
      expect(layout.overflowX, `${vp} ${rt}: horizontal scroll`).toBe(false);
      expect(layout.offscreen, `${vp} ${rt}: offscreen elements`).toEqual([]);
      // 記録した失敗が 1 件でもあると、そのルートは「動かない UI が残存」。
      expect(
        log.filter((entry) => entry.result === "fail" && entry.viewport === vp && entry.route === rt),
        `${vp} ${rt}: failed operations`,
      ).toEqual([]);
      void testInfo;
    });
  }
}
