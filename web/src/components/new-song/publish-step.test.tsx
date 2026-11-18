// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SAMPLE_CHART } from "@/lib/chart/fixtures";
import type { ValidationIssue } from "@/lib/chart/types";
import type { NewSongInfo } from "@/lib/new-song/draft";
import type { PublishTransport } from "@/lib/publish/transport";
import { PublishStep } from "./publish-step";
import { publishAndVerify } from "@/lib/publish/client";

/**
 * Issue #639 — the publish execution of step 3 is defense in depth: it
 * refuses a chart that still carries contract issues, and a publish call
 * that throws outside its contract must not leave the button stuck.
 */

vi.mock("@/lib/publish/client", () => ({
  publishAndVerify: vi.fn(),
}));

const publishAndVerifyMock = vi.mocked(publishAndVerify);

const INFO: NewSongInfo = {
  youtube: "dQw4w9WgXcQ",
  title: "テスト曲",
  artist: "テスト",
  songId: "test-song-639",
  level: 5,
  durationSec: 120,
};

const TRANSPORT: PublishTransport = {
  mode: "live",
  baseUrl: "",
  fetch: vi.fn(),
};

const ISSUE: ValidationIssue = { path: "/notes/0", message: "broken", keyword: "type" };

let root: Root | null = null;
let container: HTMLElement | null = null;

async function renderStep(chartIssues: ValidationIssue[]): Promise<void> {
  (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  await act(async () => {
    root!.render(
      <PublishStep
        info={INFO}
        chart={SAMPLE_CHART}
        chartIssues={chartIssues}
        transport={TRANSPORT}
        token="fixture-token"
        onTokenChange={() => {}}
        onBack={() => {}}
        onPublished={() => {}}
      />,
    );
  });
}

async function pressPublish(): Promise<void> {
  const button = Array.from(container!.querySelectorAll("button")).find((node) =>
    node.textContent?.includes("公開する"),
  );
  if (!button) throw new Error("publish button not found");
  await act(async () => {
    button.dispatchEvent(new MouseEvent("click", { bubbles: true }));
  });
}

afterEach(() => {
  act(() => root?.unmount());
  container?.remove();
  root = null;
  container = null;
  publishAndVerifyMock.mockReset();
});

describe("PublishStep publish guard (issue #639)", () => {
  it("refuses to publish while the chart still has contract issues", async () => {
    await renderStep([ISSUE]);

    await pressPublish();

    expect(publishAndVerifyMock).not.toHaveBeenCalled();
    expect(container!.querySelector('[data-testid="publish-error"]')).not.toBeNull();
  });

  it("recovers when the publish call throws outside its contract", async () => {
    publishAndVerifyMock.mockRejectedValue(new Error("contract violation"));
    await renderStep([]);

    await pressPublish();
    await pressPublish();

    // The first throw is reported and the in-flight guard is released, so a
    // second attempt reaches the transport again instead of sticking.
    expect(publishAndVerifyMock).toHaveBeenCalledTimes(2);
    const panel = container!.querySelector('[data-testid="publish-error"]');
    expect(panel?.textContent).toContain("予期せず失敗");
  });
});
