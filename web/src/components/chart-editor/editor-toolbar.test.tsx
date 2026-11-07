// @vitest-environment jsdom
import { act, useState } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { updateChartMeta } from "@/lib/chart/notes";
import { SAMPLE_CHART } from "@/lib/chart/fixtures";
import type { Chart } from "@/lib/chart/types";
import type { SnapSetting } from "@/lib/chart/snap";
import { EditorToolbar } from "./editor-toolbar";
import { getSnapDivision, setSnapDivision } from "./snap-store";

/**
 * Issue #163 — toolbar の実操作。
 *
 * BPM は draft 入力: Ctrl/⌘+A → Delete (空文字) や無効テキストの途中でも
 * 表示が確定値に戻されず、blur / Enter で整数として確定する。
 * Grid / View の Select は Control だけで開閉・選択できる。
 *
 * 親 (chart-editor) と同じく chart を state で持ち、確定値が画面へ往復する
 * ことを確かめる。スタブした DOM API は Zag popper / ハイライト用。
 */

class ResizeObserverStub {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}
if (typeof globalThis.ResizeObserver === "undefined") {
  (globalThis as { ResizeObserver?: unknown }).ResizeObserver = ResizeObserverStub;
}
if (typeof Element.prototype.scrollTo !== "function") {
  Element.prototype.scrollTo = function scrollTo(): void {};
}
if (typeof Element.prototype.scrollIntoView !== "function") {
  Element.prototype.scrollIntoView = function scrollIntoView(): void {};
}

let root: Root | null = null;
let container: HTMLElement | null = null;

interface Handlers {
  onNameChange?: (name: string) => void;
  onBpmChange?: (bpm: number) => void;
  onResolutionChange?: (resolution: number) => void;
  onSnapDivisionChange?: (division: SnapSetting) => void;
  onZoomChange?: (zoom: number) => void;
}

/**
 * chart-editor の state 更新を模すハーネス: 確定値が表示へ往復する。
 *
 * The snap division is not a prop here: the toolbar and the grid share the
 * snap store (issue #180), because `chart-editor.tsx` belongs to another
 * issue and must not carry the state.
 */
function ToolbarHarness({ handlers }: { handlers: Handlers }) {
  const [chart, setChart] = useState<Chart | null>(SAMPLE_CHART);
  const [resolution, setResolution] = useState(8);
  const [zoom, setZoom] = useState(1);
  return (
    <EditorToolbar
      chart={chart}
      noteCount={chart?.notes.length ?? 0}
      resolution={resolution}
      zoom={zoom}
      canDelete={false}
      onNameChange={(name) => {
        handlers.onNameChange?.(name);
        setChart((current) => (current ? updateChartMeta(current, { name }) : current));
      }}
      onBpmChange={(bpm) => {
        handlers.onBpmChange?.(bpm);
        setChart((current) => (current ? updateChartMeta(current, { BPM: bpm }) : current));
      }}
      onResolutionChange={(next) => {
        handlers.onResolutionChange?.(next);
        setResolution(next);
      }}
      onSnapDivisionChange={(next) => {
        handlers.onSnapDivisionChange?.(next);
      }}
      onZoomChange={(next) => {
        handlers.onZoomChange?.(next);
        setZoom(next);
      }}
      onImportFile={() => {}}
      onExport={() => {}}
      onDeleteSelected={() => {}}
    />
  );
}

function renderToolbar(handlers: Handlers = {}): HTMLElement {
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  act(() => {
    root!.render(<ToolbarHarness handlers={handlers} />);
  });
  return container;
}

function inputByTestId(testId: string): HTMLInputElement {
  const input = document.body.querySelector<HTMLInputElement>(`[data-testid="${testId}"]`);
  if (input === null) {
    throw new Error(`input not found: ${testId}`);
  }
  return input;
}

/** React の value tracker を通す typing の再現 (paste / IME 確定と同じ経路)。 */
function type(input: HTMLInputElement, value: string): void {
  const setter = Object.getOwnPropertyDescriptor(
    HTMLInputElement.prototype,
    "value",
  )!.set!;
  setter.call(input, value);
  act(() => {
    input.dispatchEvent(new Event("input", { bubbles: true }));
  });
}

function focusAndBlur(input: HTMLInputElement): void {
  act(() => {
    input.focus();
  });
  act(() => {
    input.blur();
  });
}

async function selectOption(labelText: string, optionLabel: string): Promise<void> {
  // トリガー (role="combobox") の aria-labelledby がラベルを指す。
  const triggers = document.body.querySelectorAll<HTMLButtonElement>('button[role="combobox"]');
  const trigger = Array.from(triggers).find((element) => {
    const labelId = element.getAttribute("aria-labelledby");
    const labelElement = labelId !== null ? document.getElementById(labelId) : null;
    return labelElement?.textContent === labelText;
  });
  if (trigger === undefined) {
    throw new Error(`combobox for ${labelText} not found`);
  }
  await act(async () => {
    trigger.click();
    await Promise.resolve();
    await new Promise((resolve) => requestAnimationFrame(resolve));
  });
  // 開いたリストボックスだけを見る: 閉じたリストは DOM に残り、同じ
  // ラベル (1/8 など) を別の Select が持つので、そのままだと誤って選ぶ。
  const options = openListboxOptions();
  const option = Array.from(options).find((element) => element.textContent === optionLabel);
  if (option === undefined) {
    throw new Error(`option ${optionLabel} not found`);
  }
  await act(async () => {
    option.click();
    await Promise.resolve();
    await new Promise((resolve) => requestAnimationFrame(resolve));
  });
}

/** The options of the listbox that is currently open. */
function openListboxOptions(): NodeListOf<HTMLElement> {
  const listboxes = Array.from(document.body.querySelectorAll<HTMLElement>('[role="listbox"]'));
  const open = listboxes.find((listbox) => listbox.getAttribute("data-state") !== "closed");
  const scope = open ?? document.body;
  return scope.querySelectorAll<HTMLElement>('[role="option"]');
}

afterEach(() => {
  if (root && container) {
    act(() => {
      root!.unmount();
    });
  }
  container?.remove();
  root = null;
  container = null;
});

describe("EditorToolbar BPM draft input (issue #163)", () => {
  it("commits a valid number while typing and keeps it after blur", { timeout: 20000 }, () => {
    const onBpmChange = vi.fn();
    renderToolbar({ onBpmChange });
    const bpm = inputByTestId("bpm-input");
    expect(bpm.value).toBe(String(SAMPLE_CHART.BPM));

    type(bpm, "96");
    expect(bpm.value).toBe("96");
    expect(onBpmChange).toHaveBeenLastCalledWith(96);

    act(() => {
      bpm.blur();
    });
    expect(bpm.value).toBe("96");
  });

  it("keeps the empty draft after Ctrl/Cmd+A + Delete and commits a re-entry on blur", { timeout: 20000 }, () => {
    const onBpmChange = vi.fn();
    renderToolbar({ onBpmChange });
    const bpm = inputByTestId("bpm-input");

    act(() => {
      bpm.focus();
    });
    // Ctrl/⌘+A → Delete: 選択範囲の削除は空文字の input event として届く。
    type(bpm, "");
    expect(bpm.value).toBe("");
    // 空文字は確定できない (譜面の BPM が壊れない)。
    expect(onBpmChange).not.toHaveBeenCalled();

    // 全再入力して blur。
    type(bpm, "96");
    act(() => {
      bpm.blur();
    });
    expect(bpm.value).toBe("96");
    expect(onBpmChange).toHaveBeenLastCalledWith(96);
  });

  it("reverts an invalid draft on blur and keeps the committed value", { timeout: 20000 }, () => {
    const onBpmChange = vi.fn();
    renderToolbar({ onBpmChange });
    const bpm = inputByTestId("bpm-input");

    act(() => {
      bpm.focus();
    });
    type(bpm, "-"); // 負号の途中
    expect(bpm.value).toBe("-");
    expect(onBpmChange).not.toHaveBeenCalled();

    act(() => {
      bpm.blur();
    });
    expect(bpm.value).toBe(String(SAMPLE_CHART.BPM));
    expect(onBpmChange).not.toHaveBeenCalled();
  });

  it("keeps a leading zero while typing and normalizes it on blur", { timeout: 20000 }, () => {
    const onBpmChange = vi.fn();
    renderToolbar({ onBpmChange });
    const bpm = inputByTestId("bpm-input");

    act(() => {
      bpm.focus();
    });
    type(bpm, "012");
    expect(bpm.value).toBe("012");
    expect(onBpmChange).toHaveBeenLastCalledWith(12);

    act(() => {
      bpm.blur();
    });
    expect(bpm.value).toBe("12");
  });

  it("commits full-width digits from the IME as an integer", { timeout: 20000 }, () => {
    const onBpmChange = vi.fn();
    renderToolbar({ onBpmChange });
    const bpm = inputByTestId("bpm-input");

    act(() => {
      bpm.focus();
    });
    type(bpm, "１２８");
    expect(onBpmChange).toHaveBeenLastCalledWith(128);

    act(() => {
      bpm.blur();
    });
    expect(bpm.value).toBe("128");
  });

  it("does not commit while the IME conversion is open, commits at compositionend", { timeout: 20000 }, () => {
    const onBpmChange = vi.fn();
    renderToolbar({ onBpmChange });
    const bpm = inputByTestId("bpm-input");

    act(() => {
      bpm.focus();
      bpm.dispatchEvent(new CompositionEvent("compositionstart", { bubbles: true }));
    });
    type(bpm, "12");
    expect(onBpmChange).not.toHaveBeenCalled();

    act(() => {
      bpm.dispatchEvent(new CompositionEvent("compositionend", { bubbles: true }));
    });
    expect(onBpmChange).toHaveBeenLastCalledWith(12);
  });
});

describe("EditorToolbar rest", () => {
  it("round-trips the song name", { timeout: 20000 }, () => {
    const onNameChange = vi.fn();
    renderToolbar({ onNameChange });
    const name = inputByTestId("song-name-input");
    type(name, "新しい曲名");
    expect(onNameChange).toHaveBeenLastCalledWith("新しい曲名");
    expect(name.value).toBe("新しい曲名");
  });

  it("opens the grid select from the control and commits the resolution", { timeout: 20000 }, async () => {
    const onResolutionChange = vi.fn();
    renderToolbar({ onResolutionChange });
    await selectOption("Grid", "1/16");
    expect(onResolutionChange).toHaveBeenLastCalledWith(16);
    // 選択した実値がトリガーに表示される。
    const triggers = document.body.querySelectorAll('button[role="combobox"]');
    expect(Array.from(triggers).some((element) => element.textContent === "1/16")).toBe(true);
  });

  it("opens the view select from the control and commits the zoom", { timeout: 20000 }, async () => {
    const onZoomChange = vi.fn();
    renderToolbar({ onZoomChange });
    await selectOption("View", "2×");
    expect(onZoomChange).toHaveBeenLastCalledWith(2);
  });
});

/**
 * The grid the notes are placed on (issue #180).
 *
 * The toolbar picks the division and the grid reads it. Both talk through
 * the snap store, because `chart-editor.tsx` belongs to another issue and
 * must not carry the state.
 */
describe("EditorToolbar snap division (issue #180)", () => {
  afterEach(() => {
    setSnapDivision("off");
  });

  it("starts with the grid off, so the editor behaves as it did before", { timeout: 20000 }, () => {
    renderToolbar();
    expect(getSnapDivision()).toBe("off");
    const triggers = document.body.querySelectorAll('button[role="combobox"]');
    expect(Array.from(triggers).some((element) => element.textContent === "off")).toBe(true);
  });

  it("commits the division the operator picks", { timeout: 20000 }, async () => {
    const onSnapDivisionChange = vi.fn();
    renderToolbar({ onSnapDivisionChange });
    await selectOption("Snap", "1/8");
    expect(onSnapDivisionChange).toHaveBeenLastCalledWith(8);
    // The grid reads the same store, so the division is shared.
    expect(getSnapDivision()).toBe(8);
    const triggers = document.body.querySelectorAll('button[role="combobox"]');
    expect(Array.from(triggers).some((element) => element.textContent === "1/8")).toBe(true);
  });

  it("can turn the grid off", { timeout: 20000 }, async () => {
    const onSnapDivisionChange = vi.fn();
    // The grid is on when the operator turns it off.
    setSnapDivision(8);
    renderToolbar({ onSnapDivisionChange });
    await selectOption("Snap", "off");
    expect(onSnapDivisionChange).toHaveBeenLastCalledWith("off");
    expect(getSnapDivision()).toBe("off");
  });

  it("offers every division of the grid", { timeout: 20000 }, async () => {
    renderToolbar();
    await selectOption("Snap", "1/16");
    const triggers = document.body.querySelectorAll('button[role="combobox"]');
    const snap = Array.from(triggers).find((element): element is HTMLElement => {
      const labelId = element.getAttribute("aria-labelledby");
      const labelElement = labelId !== null ? document.getElementById(labelId) : null;
      return labelElement?.textContent === "Snap";
    });
    expect(snap).not.toBeUndefined();
    await act(async () => {
      snap!.click();
      await Promise.resolve();
      await new Promise((resolve) => requestAnimationFrame(resolve));
    });
    const labels = Array.from(openListboxOptions()).map((option) => option.textContent);
    expect(labels).toEqual(["off", "1/1", "1/2", "1/4", "1/8", "1/16"]);
  });
});
