// @vitest-environment jsdom
import { act, useState } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Topbar, type LevelFilter } from "./topbar";
import type { SortKey } from "./catalog-view";

/**
 * Issue #163 — Catalog 検索とフィルタの実操作。
 *
 * 検索は typing / paste / Ctrl+A → Delete で往復し、Sort / Level の Select
 * は Control だけでクリック・キーボード (Enter / Escape / 矢印) で開閉・選択
 * できる。選択した実値がフィルタへ反映される。
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
  onQueryChange?: (value: string) => void;
  onLevelChange?: (value: LevelFilter) => void;
  onSortChange?: (value: SortKey) => void;
}

function TopbarHarness({ handlers }: { handlers: Handlers }) {
  const [query, setQuery] = useState("");
  const [level, setLevel] = useState<LevelFilter>("all");
  const [sort, setSort] = useState<SortKey>("catalog");
  return (
    <Topbar
      title="Catalog"
      subtitle="Published songs"
      query={query}
      onQueryChange={(value) => {
        handlers.onQueryChange?.(value);
        setQuery(value);
      }}
      level={level}
      onLevelChange={(value) => {
        handlers.onLevelChange?.(value);
        setLevel(value);
      }}
      sort={sort}
      onSortChange={(value) => {
        handlers.onSortChange?.(value);
        setSort(value);
      }}
    />
  );
}

function renderTopbar(handlers: Handlers = {}): HTMLElement {
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  act(() => {
    root!.render(<TopbarHarness handlers={handlers} />);
  });
  return container;
}

function searchInput(): HTMLInputElement {
  const input = document.body.querySelector<HTMLInputElement>("#catalog-search");
  if (input === null) {
    throw new Error("the search input is missing");
  }
  return input;
}

function triggerByLabel(labelText: string): HTMLButtonElement {
  const triggers = document.body.querySelectorAll<HTMLButtonElement>('button[role="combobox"]');
  const trigger = Array.from(triggers).find((element) => {
    const labelId = element.getAttribute("aria-labelledby");
    const labelElement = labelId !== null ? document.getElementById(labelId) : null;
    return labelElement?.textContent === labelText;
  });
  if (trigger === undefined) {
    throw new Error(`combobox for ${labelText} not found`);
  }
  return trigger;
}

/** React の value tracker を通す typing の再現 (paste / IME 確定と同じ経路)。 */
function type(input: HTMLInputElement, value: string): void {
  const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!;
  setter.call(input, value);
  act(() => {
    input.dispatchEvent(new Event("input", { bubbles: true }));
  });
}

async function clickTrigger(trigger: HTMLButtonElement): Promise<void> {
  await act(async () => {
    trigger.click();
    await Promise.resolve();
    await new Promise((resolve) => requestAnimationFrame(resolve));
  });
}

async function pressKey(key: string, target: Element): Promise<void> {
  await act(async () => {
    const active = document.activeElement ?? target;
    active.dispatchEvent(new KeyboardEvent("keydown", { key, bubbles: true, cancelable: true }));
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

describe("Topbar search (issue #163)", () => {
  it("round-trips typed and pasted queries and select-all delete", () => {
    const onQueryChange = vi.fn();
    renderTopbar({ onQueryChange });
    const input = searchInput();

    type(input, "星降る");
    expect(onQueryChange).toHaveBeenLastCalledWith("星降る");
    expect(input.value).toBe("星降る");

    // Ctrl/⌘+A → Delete。
    type(input, "");
    expect(onQueryChange).toHaveBeenLastCalledWith("");
    expect(input.value).toBe("");
  });

  it("keeps the / shortcut id on the search input", () => {
    renderTopbar();
    expect(searchInput().id).toBe("catalog-search");
  });
});

describe("Topbar selects (issue #163)", () => {
  it("opens the sort select by click and commits the chosen key", async () => {
    const onSortChange = vi.fn();
    renderTopbar({ onSortChange });
    const trigger = triggerByLabel("Sort");

    await clickTrigger(trigger);
    expect(trigger.getAttribute("aria-expanded")).toBe("true");

    const contentId = trigger.getAttribute("aria-controls") ?? "";
    const options = document
      .getElementById(contentId)!
      .querySelectorAll<HTMLElement>('[role="option"]');
    expect(options.length).toBe(6);
    const bpm = Array.from(options).find((element) => element.textContent === "BPM");
    await act(async () => {
      bpm!.click();
      await Promise.resolve();
      await new Promise((resolve) => requestAnimationFrame(resolve));
    });

    expect(onSortChange).toHaveBeenLastCalledWith("bpm");
    expect(trigger.getAttribute("aria-expanded")).toBe("false");
    expect(trigger.textContent).toContain("BPM");
  });

  it("selects a level filter with the keyboard (ArrowDown + Enter) and closes on Escape", async () => {
    const onLevelChange = vi.fn();
    renderTopbar({ onLevelChange });
    const trigger = triggerByLabel("Level");

    await act(async () => {
      trigger.focus();
      await Promise.resolve();
    });
    await pressKey("ArrowDown", trigger);
    expect(trigger.getAttribute("aria-expanded")).toBe("true");

    // "Level 10 – 19" へハイライトを進めて Enter。
    await pressKey("ArrowDown", trigger);
    await pressKey("ArrowDown", trigger);
    await pressKey("Enter", trigger);
    expect(onLevelChange).toHaveBeenLastCalledWith("mid");

    await clickTrigger(trigger);
    await pressKey("Escape", trigger);
    expect(trigger.getAttribute("aria-expanded")).toBe("false");
  });
});
