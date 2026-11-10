import { expect, test, type APIRequestContext } from "@playwright/test";

/**
 * Publish round trip over real HTTP (issue: browser E2E as a quality gate):
 *
 *   Catalog → Editor → Publish (real `PUT /api/v1/admin/songs/{songId}` with
 *   the local admin token) → success panel → back to Catalog, which reflects
 *   the new chart version and the published title.
 *
 * The write goes to an isolated test song id (`e2e-publish-flow`), never to a
 * song of the seed; the song is archived again in `afterAll`, so the public
 * catalog is back to the seeded state and the suite is repeatable (the next
 * run unarchives and republishes on top; the local D1 is reset by
 * `void db seed` anyway).
 *
 * Prerequisites are the config's, not optional: seeded local D1 + dev server
 * (the `webServer` entry guarantees both) and `FLASHBEAT_ADMIN_TOKEN` of the
 * dev server, which is the literal `local-dev-only` locally and in CI.
 */

test.describe.configure({ mode: "serial" });

const TEST_SONG_ID = "e2e-publish-flow";
const TEST_TITLE = "Who [e2e]";
const TOKEN = process.env.FLASHBEAT_ADMIN_TOKEN ?? "local-dev-only";

interface CatalogEntry {
  catalogVersion: number;
  songs: { id: string; title: string; chartVersion: number }[];
}

async function fetchCatalog(request: APIRequestContext): Promise<CatalogEntry> {
  const response = await request.get("/api/v1/catalog");
  if (!response.ok()) {
    throw new Error(`GET /api/v1/catalog answered ${response.status()}`);
  }
  return (await response.json()) as CatalogEntry;
}

test.beforeAll(async ({ request }) => {
  // A previous run that crashed before the cleanup leaves the test song live.
  // Restore is idempotent; a fresh seed has no such song (404 is fine).
  const response = await request.post(`/api/v1/admin/songs/${TEST_SONG_ID}`, {
    headers: { Authorization: `Bearer ${TOKEN}` },
    data: { archived: false },
  });
  if (!response.ok() && response.status() !== 404) {
    throw new Error(`unarchive ${TEST_SONG_ID} answered ${response.status()}`);
  }
});

test.afterAll(async ({ request }) => {
  // Cleanup: the test song leaves the public catalog again (songs are never
  // deleted; the archive is the soft delete).
  const response = await request.post(`/api/v1/admin/songs/${TEST_SONG_ID}`, {
    headers: { Authorization: `Bearer ${TOKEN}` },
    data: { archived: true },
  });
  if (!response.ok() && response.status() !== 404) {
    throw new Error(`archive ${TEST_SONG_ID} answered ${response.status()}`);
  }
});

test("catalog → editor → publish → catalog reflects the publish", async ({ page, request }) => {
  const before = await fetchCatalog(request);
  const who = before.songs.find((song) => song.id === "who");
  if (!who) {
    throw new Error("the seeded song `who` is missing from the catalog; is the D1 seeded?");
  }

  // Catalog → editor for the seeded song.
  await page.goto("/");
  const row = page
    .getByTestId("catalog-list")
    .locator("a[data-row-link]", { hasText: "Who" })
    .first();
  await expect(row).toBeVisible();
  await row.click();
  await expect(page).toHaveURL(new RegExp(`/chart\\?songId=who&version=${who.chartVersion}`));
  await expect(page.getByTestId("chart-identity")).toContainText("Who");

  // Editor → publish screen; the draft handoff carries the chart on screen.
  await page.getByRole("button", { name: "公開に進む / Review & Publish" }).click();
  await expect(page).toHaveURL(/\/publish\?/);
  await expect(page.getByRole("heading", { name: "Publish a song" })).toBeVisible();

  // Retarget the publish to the isolated test song id and rename it.
  const songIdInput = page.getByPlaceholder("who", { exact: true });
  await expect(songIdInput).toHaveValue("who");
  await songIdInput.fill(TEST_SONG_ID);
  await page.getByPlaceholder("Who", { exact: true }).fill(TEST_TITLE);

  // Review → dialog → the staff token → the one request that publishes.
  await page.getByRole("button", { name: "Review publish" }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.getByPlaceholder("FLASHBEAT_ADMIN_TOKEN").fill(TOKEN);
  await page.getByRole("button", { name: "Publish", exact: true }).click();

  // The success panel reports the versions the API answered.
  const success = page.getByTestId("publish-success");
  await expect(success).toBeVisible();
  const detail = await success.innerText();
  const chartVersion = Number(/chart version (\d+)/.exec(detail)?.[1]);
  const catalogVersion = Number(/catalog is at version (\d+)/.exec(detail)?.[1]);
  expect(chartVersion).toBeGreaterThanOrEqual(1);
  expect(catalogVersion).toBe(before.catalogVersion + 1);
  await expect(success).toContainText(TEST_SONG_ID);

  // Back to the catalog: the test song is listed with its new version.
  await page.getByRole("link", { name: "← Web Studio" }).click();
  await expect(page).toHaveURL(/\/$/);
  // The version text (`v${n}`) sits in the row's meta line, next to the link.
  const publishedRow = page
    .getByTestId("catalog-list")
    .locator(".row", { hasText: TEST_TITLE })
    .first();
  await expect(publishedRow).toBeVisible();
  await expect(publishedRow).toContainText(`v${chartVersion}`);

  // The catalog API agrees: one more snapshot, the new song at that version.
  const after = await fetchCatalog(request);
  expect(after.catalogVersion).toBe(before.catalogVersion + 1);
  const published = after.songs.find((song) => song.id === TEST_SONG_ID);
  expect(published?.title).toBe(TEST_TITLE);
  expect(published?.chartVersion).toBe(chartVersion);
});
