import { mkdirSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test, type Page } from "@playwright/test";

/**
 * Visitor flow E2E (issue #215): 初回訪問 → 開始操作を発見 → 1操作 → 結果確認
 * → 戻る/やり直し。The default path is 曲を選ぶ → 譜面を見る/試す, driven by
 * one primary CTA. Also captures real-pixel screenshots of the main flow at
 * the three review viewports (1920×1080 / 2560×1440 / 2560×1664) into
 * tests/e2e/artifacts/ and asserts a clean console.
 *
 * Prerequisites: local `vp dev` with seeded D1 (see playwright.config.ts).
 */

const ARTIFACTS_DIR = join(
  dirname(fileURLToPath(import.meta.url)),
  "artifacts",
);

const VIEWPORTS = [
  { name: "1920x1080", width: 1920, height: 1080 },
  { name: "2560x1440", width: 2560, height: 1440 },
  { name: "2560x1664", width: 2560, height: 1664 },
] as const;

test.describe.configure({ mode: "serial" });

test.beforeAll(() => {
  mkdirSync(ARTIFACTS_DIR, { recursive: true });
});

/** Collect console errors and page errors; the flow must stay clean. */
function watchConsole(page: Page): () => string[] {
  const problems: string[] = [];
  page.on("console", (message) => {
    if (message.type() === "error") problems.push(`console.error: ${message.text()}`);
  });
  page.on("pageerror", (error) => problems.push(`pageerror: ${error.message}`));
  return () => problems;
}

async function shot(page: Page, viewport: string, name: string): Promise<void> {
  await page.screenshot({
    path: join(ARTIFACTS_DIR, `${viewport}-${name}.png`),
    fullPage: true,
  });
}

for (const viewport of VIEWPORTS) {
  test(`visitor flow at ${viewport.name} with clean console`, async ({ page }) => {
    test.setTimeout(60_000);
    const problems = watchConsole(page);
    await page.setViewportSize({ width: viewport.width, height: viewport.height });

    // 1. 初回訪問: the hero states the default path with one primary CTA.
    await page.goto("/");
    const hero = page.getByTestId("welcome-hero");
    await expect(hero).toBeVisible();
    await expect(hero).toContainText("曲を選んで");
    await expect(page.getByTestId("welcome-start")).toBeVisible();

    // The staff routes never sit next to the visitor entry as equals: they
    // live under the staff group and Publish carries the token boundary.
    await expect(page.getByTestId("nav-visitor")).toBeVisible();
    await expect(page.getByTestId("nav-publish-admin-badge")).toHaveText("要トークン");

    // 2. 開始操作を発見 → 1操作: the CTA focuses the first visible catalog row.
    // (While the catalog is still loading the CTA focuses the search field
    // instead — that fallback is covered by the unit test.)
    const firstRow = page.getByTestId("catalog-list").locator("a[data-row-link]").first();
    await expect(firstRow).toBeVisible();
    await shot(page, viewport.name, "1-home");
    await page.getByTestId("welcome-start").click();
    await expect(page.locator("a[data-row-link]:visible").first()).toBeFocused();

    // 3. 結果確認: the row opens the chart of that song.
    await firstRow.click();
    await expect(page).toHaveURL(/\/chart\?songId=.+/);
    await expect(page.getByTestId("chart-identity")).toBeVisible();
    await expect(page.getByTestId("publish-token-hint")).toBeVisible();
    await shot(page, viewport.name, "2-chart");

    // 4. 戻る: back to the song list, then やり直し: pick again.
    await page.getByRole("link", { name: "← 曲を選ぶ / Back to songs" }).click();
    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByTestId("welcome-hero")).toBeVisible();
    await shot(page, viewport.name, "3-back-home");

    await page.getByTestId("catalog-list").locator("a[data-row-link]").first().click();
    await expect(page).toHaveURL(/\/chart\?songId=.+/);
    await expect(page.getByTestId("chart-identity")).toBeVisible();

    expect(problems(), problems().join("\n")).toEqual([]);
  });
}
