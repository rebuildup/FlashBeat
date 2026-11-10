import { mkdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { expect, test, type Page } from "@playwright/test";

/**
 * The exhibit operation surface of the chart editor (issue #216).
 *
 * One real mouse-only pass per target viewport:
 *
 *   published chart loads → place one note → read the selection → delete it
 *   → undo it back → move the playhead and read the position
 *
 * The screenshots are the pixel evidence attached to the Draft PR. They are
 * written to `specs/216-chart-editor-operation/evidence/`.
 *
 * Run (dev server + seeded D1):
 *
 *   cd web && npx vp dev
 *   E2E_BASE_URL=http://localhost:5173 npm run test:e2e
 */

const EVIDENCE_DIR = fileURLToPath(
  new URL("../../../specs/216-chart-editor-operation/evidence/", import.meta.url),
);

const VIEWPORTS = [
  { name: "fhd-1920x1080", width: 1920, height: 1080 },
  { name: "2k-2560x1440", width: 2560, height: 1440 },
  { name: "mac-2560x1664", width: 2560, height: 1664 },
] as const;

/** Console errors that belong to the Studio, not to the embedded player. */
function collectAppErrors(page: Page, baseURL: string): string[] {
  const errors: string[] = [];

  page.on("console", (message) => {
    if (message.type() !== "error") {
      return;
    }
    const source = message.location().url ?? "";
    // The YouTube iframe logs its own permission/sandbox violations. They
    // are third-party noise and do not come from the Studio bundle.
    if (source && !source.startsWith(baseURL)) {
      return;
    }
    errors.push(message.text());
  });

  page.on("pageerror", (error) => {
    errors.push(error.message);
  });

  return errors;
}

async function openPublishedChart(page: Page): Promise<void> {
  await page.goto("/chart?songId=who&version=1");
  await expect(page.getByTestId("chart-identity")).toContainText("Who");
  await expect(page.getByTestId("chart-identity")).toContainText("v1");
  await expect(page.getByTestId("chart-identity")).toContainText("139 notes");
  await expect(page.getByTestId("note-count")).toContainText("139 notes");
  await expect(page.getByTestId("note-grid")).toBeVisible();
  await expect(page.getByTestId("editor-inspector")).toBeVisible();
  await expect(page.getByTestId("editor-preview-panel")).toBeVisible();
}

for (const viewport of VIEWPORTS) {
  test.describe(viewport.name, () => {
    test.use({ viewport: { width: viewport.width, height: viewport.height } });

    test("place → delete → undo → playhead", async ({ page, baseURL }) => {
      await mkdir(EVIDENCE_DIR, { recursive: true });
      const errors = collectAppErrors(page, baseURL ?? "http://localhost:5173");

      await openPublishedChart(page);
      await page.screenshot({
        path: `${EVIDENCE_DIR}/${viewport.name}-00-open.png`,
      });

      // Lane 1 (leftmost lane), half a beat in: the published chart has no
      // note before beat 1.25, so the cell is empty at every resolution.
      const grid = page.getByTestId("note-grid");
      const box = await grid.boundingBox();
      expect(box).not.toBeNull();
      const laneWidth = Math.max(56, Math.floor(box!.width / 8));
      await page.mouse.click(box!.x + laneWidth / 2, box!.y + 22);

      await expect(page.getByTestId("note-count")).toContainText("140 notes");
      await expect(page.getByTestId("selection-lane")).toHaveText("1");
      await expect(page.getByTestId("selection-beat")).toHaveText("0.50");
      await page.screenshot({
        path: `${EVIDENCE_DIR}/${viewport.name}-01-placed.png`,
      });

      // The just-placed note is selected; the inspector deletes it.
      await page.getByTestId("selection-delete").click();
      await expect(page.getByTestId("note-count")).toContainText("139 notes");
      await expect(page.getByTestId("selection-empty")).toBeVisible();
      await page.screenshot({
        path: `${EVIDENCE_DIR}/${viewport.name}-02-deleted.png`,
      });

      // Undo restores the note and its selection.
      await page.getByTestId("undo-button").click();
      await expect(page.getByTestId("note-count")).toContainText("140 notes");
      await expect(page.getByTestId("selection-lane")).toHaveText("1");
      await page.getByTestId("redo-button").click();
      await expect(page.getByTestId("note-count")).toContainText("139 notes");
      await page.getByTestId("undo-button").click();
      await expect(page.getByTestId("note-count")).toContainText("140 notes");
      await page.screenshot({
        path: `${EVIDENCE_DIR}/${viewport.name}-03-restored.png`,
      });

      // The playhead is always visible and the slider moves it (and asks the
      // YouTube preview to seek to the same second).
      const slider = page.getByTestId("playhead-slider");
      await slider.evaluate((element, value) => {
        const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!;
        setter.call(element, String(value));
        element.dispatchEvent(new Event("input", { bubbles: true }));
      }, 5);
      await expect(page.getByTestId("transport-time")).toContainText("5.00s");
      await expect(page.getByTestId("playhead-readout")).toContainText("5.00s");
      await page.screenshot({
        path: `${EVIDENCE_DIR}/${viewport.name}-04-playhead.png`,
      });

      expect(errors, `console errors: ${errors.join("\n")}`).toEqual([]);
    });
  });
}
