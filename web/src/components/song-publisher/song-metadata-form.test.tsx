// @vitest-environment jsdom
import { act, useState } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  validateSongMetadata,
  type SongMetadataDraft,
  type SongMetadataIssue,
} from "@/lib/publish/validation";
import { SAMPLE_CHART } from "@/lib/chart/fixtures";
import { SongMetadataForm } from "./song-metadata-form";

/**
 * Issue #163 — Publish メタデータフォームの実操作。
 *
 * Duration は draft 入力 (空文字は 0 ではなく未入力 NaN)、Level の Select は
 * Control だけで開閉・選択できる。テキスト (Song ID / Title / Artist /
 * YouTube) は typing / paste / IME 確定の input event で往復する。
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

const BASE_DRAFT: SongMetadataDraft = {
  songId: "who",
  title: "Who",
  artist: "Azari",
  youtube: "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
  level: 10,
  durationSec: 214,
};

/** song-publisher と同じく draft を state で持ち、確定値が表示へ往復する。 */
function FormHarness({
  handlers,
}: {
  handlers?: {
    onChange?: (patch: Partial<SongMetadataDraft>) => void;
    onFieldBlur?: (field: keyof SongMetadataDraft) => void;
  };
}) {
  const [draft, setDraft] = useState<SongMetadataDraft>(BASE_DRAFT);
  const [issues, setIssues] = useState<SongMetadataIssue[]>([]);
  return (
    <SongMetadataForm
      draft={draft}
      onChange={(patch) => {
        handlers?.onChange?.(patch);
        setDraft((current) => ({ ...current, ...patch }));
      }}
      onFieldBlur={(field) => {
        handlers?.onFieldBlur?.(field);
        setIssues(validateSongMetadata({ ...draft, [field]: touched(draft, field) }));
      }}
      issues={issues}
    />
  );
}

function touched(draft: SongMetadataDraft, field: keyof SongMetadataDraft): unknown {
  // blur 時にそのフィールドだけ「触った」ことにする簡易模擬。
  return field === "durationSec" ? draft.durationSec : draft[field];
}

function renderForm(
  handlers?: {
    onChange?: (patch: Partial<SongMetadataDraft>) => void;
    onFieldBlur?: (field: keyof SongMetadataDraft) => void;
  },
): HTMLElement {
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  act(() => {
    root!.render(<FormHarness handlers={handlers} />);
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

function inputByLabel(labelText: string): HTMLInputElement {
  const label = Array.from(document.body.querySelectorAll("label")).find(
    (element) => element.textContent === labelText,
  );
  const control = label?.control as HTMLInputElement | undefined;
  if (control === undefined) {
    throw new Error(`input for label ${labelText} not found`);
  }
  return control;
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
  const options = document.body.querySelectorAll<HTMLElement>('[role="option"]');
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

describe("SongMetadataForm text fields (issue #163)", () => {
  it("round-trips the song id, title, artist and youtube fields", { timeout: 20000 }, () => {
    const onChange = vi.fn();
    renderForm({ onChange });

    const songId = inputByLabel("Song id");
    type(songId, "my-song");
    expect(onChange).toHaveBeenLastCalledWith({ songId: "my-song" });
    expect(songId.value).toBe("my-song");

    // Ctrl/⌘+A → Delete は空文字の input event として届き、そのまま確定する。
    type(songId, "");
    expect(onChange).toHaveBeenLastCalledWith({ songId: "" });
    expect(songId.value).toBe("");

    const title = inputByLabel("Title");
    type(title, "星降る夜に");
    expect(onChange).toHaveBeenLastCalledWith({ title: "星降る夜に" });

    const artist = inputByLabel("Artist");
    type(artist, "");
    expect(onChange).toHaveBeenLastCalledWith({ artist: "" });

    const youtube = inputByLabel("YouTube video");
    type(youtube, "https://youtu.be/abcdefghijk");
    expect(onChange).toHaveBeenLastCalledWith({ youtube: "https://youtu.be/abcdefghijk" });
  });

  it("keeps Japanese text typed through the IME path (composition events)", { timeout: 20000 }, () => {
    const onChange = vi.fn();
    renderForm({ onChange });
    const title = inputByLabel("Title");

    act(() => {
      title.focus();
      title.dispatchEvent(new CompositionEvent("compositionstart", { bubbles: true }));
    });
    type(title, "ほし");
    // 変換確定前の値も表示から消えない。
    expect(title.value).toBe("ほし");

    act(() => {
      title.dispatchEvent(new CompositionEvent("compositionend", { bubbles: true }));
    });
    expect(title.value).toBe("ほし");
  });
});

describe("SongMetadataForm duration draft (issue #163)", () => {
  it("commits typed seconds and normalizes on blur", { timeout: 20000 }, () => {
    const onChange = vi.fn();
    renderForm({ onChange });
    const duration = inputByTestId("duration-input");
    expect(duration.value).toBe("214");

    act(() => {
      duration.focus();
    });
    type(duration, "180");
    expect(onChange).toHaveBeenLastCalledWith({ durationSec: 180 });
    act(() => {
      duration.blur();
    });
    expect(duration.value).toBe("180");
  });

  it("treats an emptied field as not-filled-in (NaN), never as 0", { timeout: 20000 }, () => {
    const onChange = vi.fn();
    renderForm({ onChange });
    const duration = inputByTestId("duration-input");

    act(() => {
      duration.focus();
    });
    // Ctrl/⌘+A → Delete。
    type(duration, "");
    expect(onChange).toHaveBeenLastCalledWith({ durationSec: Number.NaN });
    // 表示は空のまま (0 に戻されない)。
    expect(duration.value).toBe("");

    act(() => {
      duration.blur();
    });
    expect(duration.value).toBe("");
  });

  it("reverts an invalid draft on blur and keeps the committed value", { timeout: 20000 }, () => {
    const onChange = vi.fn();
    renderForm({ onChange });
    const duration = inputByTestId("duration-input");

    act(() => {
      duration.focus();
    });
    type(duration, "12.");
    // 途中の小数も表示は保持される。
    expect(duration.value).toBe("12.");

    type(duration, "12.5");
    expect(onChange).toHaveBeenLastCalledWith({ durationSec: 12.5 });

    type(duration, "-");
    act(() => {
      duration.blur();
    });
    // 最後に確定した値 (12.5) の表示へ戻る。
    expect(duration.value).toBe("12.5");
  });

  it("marks the duration issue after a blur on an emptied field", { timeout: 20000 }, () => {
    const onFieldBlur = vi.fn();
    renderForm({ onFieldBlur });
    const duration = inputByTestId("duration-input");

    act(() => {
      duration.focus();
    });
    type(duration, "");
    focusAndBlur(duration);

    expect(onFieldBlur).toHaveBeenCalledWith("durationSec");
    expect(document.body.textContent).toContain("Enter the duration in seconds.");
  });
});

describe("SongMetadataForm level select (issue #163)", () => {
  it("opens from the control, selects a level and reflects the value", { timeout: 20000 }, async () => {
    const onChange = vi.fn();
    renderForm({ onChange });

    await selectOption("Level", "Level 12");
    expect(onChange).toHaveBeenLastCalledWith({ level: 12 });

    const triggers = document.body.querySelectorAll('button[role="combobox"]');
    expect(Array.from(triggers).some((element) => element.textContent === "Level 12")).toBe(true);
  });

  it("keeps the list scrollable per group and closes on Escape", { timeout: 20000 }, async () => {
    renderForm();
    const triggers = document.body.querySelectorAll<HTMLButtonElement>('button[role="combobox"]');
    expect(triggers.length).toBe(1);

    await act(async () => {
      triggers[0]!.click();
      await Promise.resolve();
      await new Promise((resolve) => requestAnimationFrame(resolve));
    });
    expect(triggers[0]!.getAttribute("aria-expanded")).toBe("true");
    // 1–99 の全項目がグループ付きで並ぶ。
    const contentId = triggers[0]!.getAttribute("aria-controls") ?? "";
    const options = document.getElementById(contentId)!.querySelectorAll('[role="option"]');
    expect(options.length).toBe(99);

    await act(async () => {
      triggers[0]!.dispatchEvent(
        new KeyboardEvent("keydown", { key: "Escape", bubbles: true, cancelable: true }),
      );
      await Promise.resolve();
      await new Promise((resolve) => requestAnimationFrame(resolve));
    });
    expect(triggers[0]!.getAttribute("aria-expanded")).toBe("false");
  });
});

describe("SongMetadataForm baseline", () => {
  it("renders the sample chart metadata without issues", () => {
    renderForm();
    expect(document.body.textContent).toContain("Song id");
    expect(SAMPLE_CHART.BPM).toBeGreaterThan(0);
  });
});
