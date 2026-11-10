import { execSync } from "node:child_process";
import { mkdirSync, writeFileSync } from "node:fs";
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

const ARTIFACTS_DIR = join(dirname(fileURLToPath(import.meta.url)), "artifacts");

const VIEWPORTS = [
  { name: "1920x1080", width: 1920, height: 1080 },
  { name: "2560x1440", width: 2560, height: 1440 },
  { name: "2560x1664", width: 2560, height: 1664 },
] as const;

test.describe.configure({ mode: "serial" });

test.beforeAll(() => {
  mkdirSync(ARTIFACTS_DIR, { recursive: true });
});

/**
 * WCAG contrast of the v5 token pairs this flow relies on, dark mode (the
 * studio is dark-first). Recorded into artifacts/metadata.json so the
 * screenshot evidence carries the check (issue #215 review).
 */
function contrast(fgHex: string, bgHex: string): number {
  const lum = (hex: string): number => {
    const [r, g, b] = [0, 2, 4].map((i) => parseInt(hex.slice(1 + i, 3 + i), 16) / 255);
    const lin = [r, g, b].map((c) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4));
    return 0.2126 * lin[0] + 0.7152 * lin[1] + 0.0722 * lin[2];
  };
  const [l1, l2] = [lum(fgHex), lum(bgHex)].sort((a, b) => b - a);
  return (l1 + 0.05) / (l2 + 0.05);
}

const CANVAS = "#1B1B1B";
const CONTRAST_PAIRS = [
  { name: "fg.default on canvas (body text)", fg: "#FFFFFF", bg: CANVAS, threshold: 4.5 },
  { name: "fg.muted on canvas (secondary text)", fg: "#B0B0B0", bg: CANVAS, threshold: 4.5 },
  { name: "fg.subtle on canvas (hints)", fg: "#8C8C8C", bg: CANVAS, threshold: 4.5 },
  {
    name: "primary CTA fg on solid.bg (再生して試す)",
    fg: "#131313",
    bg: "#3FA9F5",
    threshold: 4.5,
  },
  { name: "visitor nav fg on solid.bg (曲を選ぶ)", fg: "#131313", bg: "#3FA9F5", threshold: 4.5 },
  {
    name: "selected fg on accent tint (aria-current)",
    fg: "#9AD3FA",
    bg: "#2B4457",
    threshold: 4.5,
  },
];

function writeMetadata(): void {
  const sha = execSync("git rev-parse HEAD", { encoding: "utf8", cwd: process.cwd() }).trim();
  const pairs = CONTRAST_PAIRS.map((pair) => ({
    ...pair,
    ratio: Number(contrast(pair.fg, pair.bg).toFixed(2)),
    pass: contrast(pair.fg, pair.bg) >= pair.threshold,
  }));
  writeFileSync(
    join(ARTIFACTS_DIR, "metadata.json"),
    JSON.stringify(
      {
        commit: sha,
        date: new Date().toISOString(),
        runCommand:
          "cd web && npx void db migrate && npx void db seed && npx vp dev; " +
          "cd web && E2E_BASE_URL=http://localhost:<port> npx playwright test -c tests/e2e/playwright.config.ts --workers=1",
        viewports: VIEWPORTS.map((viewport) => `${viewport.name} (CSS viewport)`),
        contrastChecks: pairs,
        allContrastPass: pairs.every((pair) => pair.pass),
      },
      null,
      4,
    ),
  );
}

test.afterAll(() => {
  writeMetadata();
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
    // Default `caret: "hide"` sets `caret-color: transparent !important` on
    // every input for the duration of the shot; if React hydrates inside that
    // window it reports a hydration mismatch and the clean-console assertion
    // below flakes. The inputs are not focused in these shots, so nothing
    // hides.
    caret: "initial",
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

    // 3. 結果確認: the row opens the chart of that song; the primary CTA
    // stays inside the Studio — it focuses the preview's Play control.
    await firstRow.click();
    await expect(page).toHaveURL(/\/chart\?songId=.+/);
    await expect(page.getByTestId("chart-identity")).toBeVisible();
    await expect(page.getByTestId("publish-token-hint")).toBeVisible();
    const tryIt = page.getByTestId("try-song-button");
    await expect(tryIt).toBeVisible();
    // The source video is a demoted secondary link now.
    await expect(page.getByTestId("open-youtube-link")).toBeVisible();
    await tryIt.click();
    // The primary CTA starts the preview through the editor's imperative
    // handle and focuses the preview panel. The panel is asserted rather than
    // the iframe or the playing state: the YouTube API replaces the iframe
    // when playback starts and can pause again (autoplay policy), neither of
    // which is a Studio failure.
    await expect(page.getByTestId("editor-preview-panel")).toBeFocused();
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
