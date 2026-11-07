import { expect, test } from "@playwright/test";

/**
 * Studio flow E2E (issue #166): the four screens share one shell and the
 * Catalog → Editor → Publish → Catalog 戻り導線 works with real clicks.
 *
 * Prerequisites (local): `void db migrate && void db seed`, then `vp dev`
 * (see playwright.config.ts next to this file). The catalog must hold the
 * seeded `who` song.
 */

test.describe.configure({ mode: "serial" });

const NAV_LINKS = ["Catalog", "New song", "New chart", "Publish"];

test("all four screens share the studio shell", async ({ page }) => {
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

test("catalog → editor → publish → back to catalog", async ({ page }) => {
  await page.goto("/");

  // The seeded song is listed; its row links into the editor.
  const row = page.locator('a[data-row-link]', { hasText: "Who" }).first();
  await expect(row).toBeVisible();
  await row.click();
  await expect(page).toHaveURL(/\/chart\?songId=who&version=1/);
  await expect(page.getByTestId("chart-identity")).toContainText("Who");
  await expect(page.getByTestId("chart-identity")).toContainText("v1");

  // The editor is ready and hands the draft to the publish screen.
  const review = page.getByRole("button", { name: "Review & Publish" });
  await expect(review).toBeVisible();
  await review.click();
  await expect(page).toHaveURL(/\/publish\?/);
  await expect(page.getByRole("heading", { name: "Publish a song" })).toBeVisible();

  // The way back reloads the catalog (AppShell refetches on mount).
  await page.getByRole("link", { name: "← Web Studio" }).click();
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByTestId("chart-identity")).toHaveCount(0);
  await expect(page.locator('a[data-row-link]', { hasText: "Who" })).toBeVisible();
});

test("forced catalog states render inside the shared shell", async ({ page }) => {
  await page.goto("/?state=empty");
  await expect(page.getByRole("navigation", { name: "Studio" })).toBeVisible();
  await expect(page.getByText("No songs published yet")).toBeVisible();

  await page.goto("/?state=error");
  await expect(page.getByRole("navigation", { name: "Studio" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Try again" })).toBeVisible();
});
