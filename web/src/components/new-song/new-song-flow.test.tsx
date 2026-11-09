// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { NewSongFlow } from "./new-song-flow";

/**
 * Issue #215 — going back in the new-song flow never loses input: the draft
 * (`@/lib/new-song/draft`) survives the step change and the return.
 */

let root: Root | null = null;
let container: HTMLElement | null = null;

/** React の value tracker を通す typing の再現 (paste / IME 確定と同じ経路)。 */
function type(input: HTMLInputElement, value: string): void {
  const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!;
  act(() => {
    setter.call(input, value);
    input.dispatchEvent(new Event("input", { bubbles: true }));
  });
}

function inputByLabel(labelText: string): HTMLInputElement {
  const label = Array.from(container!.querySelectorAll("label")).find((node) =>
    node.textContent?.includes(labelText),
  );
  const id = label?.getAttribute("for") ?? undefined;
  const input = id ? document.getElementById(id) : (label?.querySelector("input") ?? null);
  if (!(input instanceof HTMLInputElement)) throw new Error(`input not found: ${labelText}`);
  return input;
}

function buttonByText(text: string): HTMLButtonElement {
  const button = Array.from(container!.querySelectorAll("button")).find((node) =>
    node.textContent?.includes(text),
  );
  if (!button) throw new Error(`button not found: ${text}`);
  return button as HTMLButtonElement;
}

function renderFlow(): void {
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  // Offline: the oEmbed lookup must resolve as an ordinary failure, not throw.
  vi.stubGlobal(
    "fetch",
    vi.fn(() => Promise.reject(new TypeError("offline"))),
  );
  act(() => {
    root!.render(<NewSongFlow apiOrigin="fixture" />);
  });
}

afterEach(() => {
  act(() => root?.unmount());
  container?.remove();
  root = null;
  container = null;
  vi.unstubAllGlobals();
  sessionStorage.clear();
});

describe("NewSongFlow draft retention (issue #215)", () => {
  it("keeps the step-1 input after advancing to the chart step and going back", () => {
    renderFlow();

    type(inputByLabel("YouTubeのURL"), "https://www.youtube.com/watch?v=dQw4w9WgXcQ");
    type(inputByLabel("曲名"), "テスト曲");
    type(inputByLabel("曲ID"), "test-song-215");
    type(inputByLabel("レベル"), "5");
    type(inputByLabel("再生時間"), "120");

    act(() => {
      buttonByText("次へ：譜面").click();
    });
    expect(container!.querySelector('[data-testid="new-song-step-2"]')).not.toBeNull();

    // Back to step 1 via the stepper (steps up to the furthest reached are
    // clickable) — the draft must come back with the input.
    act(() => {
      buttonByText("曲の情報を入力").click();
    });
    expect(container!.querySelector('[data-testid="new-song-step-1"]')).not.toBeNull();
    expect(inputByLabel("曲名").value).toBe("テスト曲");
    expect(inputByLabel("曲ID").value).toBe("test-song-215");
    expect(inputByLabel("レベル").value).toBe("5");
    expect(inputByLabel("再生時間").value).toBe("120");
  });
});
