import { expect, test } from "@playwright/test";

/**
 * Studio main screen smoke check: the catalog shell renders against the dev
 * server the Playwright config guarantees (`webServer` seeds the local D1 and
 * boots `vp dev` when `E2E_BASE_URL` answers nothing; a server that never
 * comes up fails the run).
 *
 * There is deliberately no `test.skip` here: a missing browser or an
 * unreachable server is a hard failure (see reporters/gate-reporter.ts — a
 * run where everything is skipped exits non-zero).
 */

test("studio main screen renders", async ({ page }) => {
  await page.goto("/");
  await expect(page.locator("#studio-content")).toBeVisible();
});
