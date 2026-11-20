import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { expect, test, type Page } from "@playwright/test";

/**
 * The per-account publish journey of issue #800 over the real dev server
 * and the seeded local D1 (same contract as the other specs):
 *
 *   sign up → create draft → edit & save the chart → sign out/in (resume)
 *   → publish (rights attestation) → catalog → republish (v2) → unpublish.
 *
 * The chart edit itself goes through `PUT /api/v1/me/drafts/{id}` — the
 * same endpoint the editor's 「下書きを保存」 button calls — because the
 * Konva grid has no stable text surface for a spec to assert on; the
 * editor page is still opened afterwards so the restore path
 * (`?serverDraft=` → chart-identity → save) is exercised in the browser.
 */

test.describe.configure({ mode: "serial" });

const RUN = Date.now().toString(36);
const EMAIL = `e2e-account-${RUN}@example.com`;
const PASSWORD = "e2e-password-123";
const SONG_ID = `e2e-acct-${RUN}`;
const TITLE = "E2E Account Song";
const ARTIST = "FlashBeat E2E";
const YOUTUBE_ID = "dQw4w9WgXcQ";

const CHART = JSON.parse(
  readFileSync(
    fileURLToPath(new URL("../../src/lib/chart/fixtures/sample-chart.json", import.meta.url)),
    "utf8",
  ),
) as { notes: unknown[] };

function catalogSongIds(page: Page) {
  return page
    .context()
    .request.get("/api/v1/catalog")
    .then(async (res) => {
      expect(res.ok()).toBeTruthy();
      const body = (await res.json()) as { songs: { id: string }[] };
      return body.songs.map((song) => song.id);
    });
}

async function signUp(page: Page) {
  await page.goto("/login");
  await page.getByRole("button", { name: "新規登録" }).click();
  await page.getByLabel("表示名").fill("E2E Account");
  await page.getByLabel("メールアドレス").fill(EMAIL);
  await page.getByLabel("パスワード").fill(PASSWORD);
  await page.getByTestId("auth-submit").click();
  await expect(page).toHaveURL(/\/my-songs/, { timeout: 60_000 });
  await expect(page.getByTestId("my-songs-header")).toBeVisible();
}

async function signIn(page: Page) {
  await page.goto("/login");
  await page.getByLabel("メールアドレス").fill(EMAIL);
  await page.getByLabel("パスワード").fill(PASSWORD);
  await page.getByTestId("auth-submit").click();
  await expect(page).toHaveURL(/\/my-songs/, { timeout: 60_000 });
}

test("account journey: signup → draft → save → publish → update → unpublish", async ({ page }) => {
  // Cold on-demand compiles of the auth, draft and chart routes can stack
  // past the config-wide 120s on a fresh dev server.
  test.setTimeout(300_000);
  // The unpublish step uses window.confirm; accept it.
  page.on("dialog", (dialog) => void dialog.accept());

  // 1. Sign up through the real form.
  await signUp(page);

  // 2. Create a draft; the panel navigates to the server-draft editor. The
  //    POST route and the chart page both compile on demand, so a cold dev
  //    server can hold the button disabled and the navigation well past the
  //    default expect timeout.
  await expect(page.getByTestId("new-draft")).toBeEnabled();
  await page.getByTestId("new-draft").click();
  await expect(page).toHaveURL(/\/chart\?serverDraft=draft-/, { timeout: 90_000 });
  const draftId = new URL(page.url()).searchParams.get("serverDraft") ?? "";
  expect(draftId).toMatch(/^draft-/);

  // 3. Save once from the editor (empty chart), then write the real chart
  //    and the public song id through the draft API — the same write the
  //    editor's save button performs.
  await expect(page.getByTestId("save-server-draft")).toBeVisible();
  await page.getByTestId("save-server-draft").click();
  await expect(page.getByTestId("server-draft-saved")).toBeVisible();

  const save = await page.context().request.put(`/api/v1/me/drafts/${draftId}`, {
    data: {
      songId: SONG_ID,
      title: TITLE,
      artist: ARTIST,
      youtubeId: YOUTUBE_ID,
      level: 7,
      durationSec: 90,
      chart: CHART,
    },
  });
  expect(save.ok()).toBeTruthy();

  // 4. Resume the draft: the editor restores the stored chart. The draft
  //    fetch is client-side; a cold dev-server compile of the route can
  //    exceed the default expect timeout, so wait explicitly and surface
  //    the error banner when the fetch itself fails.
  await page.reload();
  await expect(async () => {
    if (await page.getByTestId("server-draft-error").isVisible()) {
      throw new Error("server draft failed to load");
    }
    await expect(page.getByTestId("chart-identity")).toContainText(`${CHART.notes.length}ノート`, {
      timeout: 5_000,
    });
  }).toPass({ timeout: 90_000 });
  await page.getByTestId("save-server-draft").click();
  await expect(page.getByTestId("server-draft-saved")).toBeVisible();

  // 5. Sign out and back in — the draft must still be there.
  await page.goto("/login");
  await page.getByTestId("signed-in").getByRole("button", { name: "ログアウト" }).click();
  await signIn(page);
  await expect(page.getByTestId(`draft-row-${draftId}`)).toBeVisible();

  // 6. Publish through the dialog with the rights attestation. The dialog
  //    opens before the draft detail arrives; the form re-seeds when it
  //    lands, so wait for the prefilled 曲ID before typing — otherwise the
  //    seed wipes the inputs.
  await page.getByTestId(`publish-${draftId}`).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toBeVisible();
  await expect(dialog.getByLabel("曲ID")).toHaveValue(SONG_ID, { timeout: 60_000 });
  await dialog.getByLabel("曲ID").fill(SONG_ID);
  await dialog.getByLabel("曲名").fill(TITLE);
  await dialog.getByLabel("アーティスト").fill(ARTIST);
  await dialog.getByLabel("YouTube動画ID").fill(YOUTUBE_ID);
  await dialog.getByLabel("レベル").fill("7");
  await dialog.getByLabel("再生時間").fill("90");
  await dialog.getByLabel("素材の出所").fill("E2E: repo fixture chart");
  await page.getByTestId("rights-confirmed").check();
  await page.getByTestId("publish-submit").click();
  // The dialog only closes after the publish batch commits; the song row is
  // already visible from before, so it cannot signal completion.
  await expect(dialog).toBeHidden({ timeout: 60_000 });
  await expect(page.getByTestId(`song-row-${SONG_ID}`)).toBeVisible();

  // 7. The public catalog serves the song.
  expect(await catalogSongIds(page)).toContain(SONG_ID);

  // 8. Republish = update to the next chart version.
  await page.getByTestId(`publish-${draftId}`).click();
  await expect(dialog).toBeVisible();
  await expect(dialog.getByLabel("曲ID")).toHaveValue(SONG_ID, { timeout: 60_000 });
  await page.getByTestId("rights-confirmed").check();
  await page.getByTestId("publish-submit").click();
  await expect(dialog).toBeHidden({ timeout: 60_000 });
  await expect(page.getByTestId(`song-row-${SONG_ID}`)).toBeVisible();
  const chartRes = await page.context().request.get(`/api/v1/charts/${SONG_ID}?version=2`);
  expect(chartRes.ok()).toBeTruthy();

  // 9. Unpublish: leaves the catalog, stays visible as 公開停止. The row
  //    badge flips only after the archive batch commits.
  await page.getByTestId(`unpublish-${SONG_ID}`).click();
  await expect(page.getByTestId(`song-row-${SONG_ID}`).getByText("公開停止")).toBeVisible({
    timeout: 60_000,
  });
  expect(await catalogSongIds(page)).not.toContain(SONG_ID);
});
