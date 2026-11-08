import { expect, test } from "@playwright/test";

/**
 * Studio flow E2E (issue #166, labels updated by #215): the screens share one
 * shell and the 曲を選ぶ → 譜面 → 公開 → 戻り導線 works with real clicks.
 *
 * Prerequisites (local): `void db migrate && void db seed`, then `vp dev`
 * (see playwright.config.ts next to this file). The catalog must hold the
 * seeded `who` song.
 */

test.describe.configure({ mode: "serial" });

const NAV_LINKS = [
  "曲を選ぶ Pick a song",
  "新曲を登録 New song",
  "譜面を編集 Edit chart",
  "公開する Publish 要トークン",
];

test("all screens share the studio shell", async ({ page }) => {
  for (const path of ["/", "/chart", "/publish", "/new-song"]) {
    await page.goto(path);
    const nav = page.getByRole("navigation", { name: "Studio" });
    await expect(nav).toBeVisible();
    for (const label of NAV_LINKS) {
      await expect(nav.getByRole("link", { name: label })).toBeVisible();
    }
    await expect(page.locator("#studio-content")).toBeVisible();
  }
});

test("publish shows the admin-token boundary before pressing it", async ({ page }) => {
  await page.goto("/");
  // The sidebar marks Publish as admin-only before any click.
  await expect(page.getByTestId("nav-publish-admin-badge")).toHaveText("要トークン");
  // The catalog row states the requirement as a non-interactive badge; the
  // row never starts a publish operation (issue #215 review).
  const row = page.getByTestId("catalog-list").locator("a[data-row-link]", { hasText: "Who" }).first();
  await expect(row).toBeVisible();
  await expect(page.getByTestId("catalog-list").getByTestId("publish-requirement")).toHaveText(
    "公開はスタッフ（要トークン）",
  );
  await expect(
    page.getByTestId("catalog-list").locator("a[href^='/publish']"),
  ).toHaveCount(0);
});

test("catalog → editor → publish → back to catalog", async ({ page }) => {
  await page.goto("/");

  // The seeded song is listed; its row links into the editor.
  // (md+ ではテーブル、base ではカード。可視側の行リンクだけを見る。)
  const row = page.getByTestId("catalog-list").locator("a[data-row-link]", { hasText: "Who" }).first();
  await expect(row).toBeVisible();
  await row.click();
  await expect(page).toHaveURL(/\/chart\?songId=who&version=1/);
  await expect(page.getByTestId("chart-identity")).toContainText("Who");
  await expect(page.getByTestId("chart-identity")).toContainText("v1");

  // The editor is ready; the visitor's primary CTA focuses the preview's
  // Play control and the staff publish path is demoted (issue #215 review).
  await expect(page.getByTestId("publish-token-hint")).toContainText("adminトークン");
  await expect(page.getByTestId("try-song-button")).toBeVisible();
  const review = page.getByRole("button", { name: "公開に進む / Review & Publish" });
  await expect(review).toBeVisible();
  await review.click();
  await expect(page).toHaveURL(/\/publish\?/);
  await expect(page.getByRole("heading", { name: "Publish a song" })).toBeVisible();

  // The way back (the publish screen's own link, song-publisher) reloads the
  // catalog (AppShell refetches on mount).
  await page.getByRole("link", { name: "← Web Studio" }).click();
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByTestId("chart-identity")).toHaveCount(0);
  await expect(page.getByTestId("catalog-list").locator('a[data-row-link]', { hasText: "Who" })).toBeVisible();
});

test("forced catalog states render inside the shared shell", async ({ page }) => {
  await page.goto("/?state=empty");
  await expect(page.getByRole("navigation", { name: "Studio" })).toBeVisible();
  await expect(page.getByText("No songs published yet")).toBeVisible();

  await page.goto("/?state=error");
  await expect(page.getByRole("navigation", { name: "Studio" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Try again" })).toBeVisible();
});
