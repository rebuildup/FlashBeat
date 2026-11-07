// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createListCollection } from "@ark-ui/react/collection";
import { Portal } from "@ark-ui/react/portal";
import * as Select from "./select";

/**
 * Issue #163 — Ark Select が実操作で開閉・選択できること。
 *
 * Ark UI はクリック/キーボードを `Select.Trigger` (role="combobox" の
 * button) にだけ実装している。`Control` が Trigger を自動で内包するように
 * なったので、`<Select.Control>` に `ValueText` と `Indicator` を置くだけの
 * 従来の呼出しのまま操作できることを実 DOM (jsdom) で確かめる。
 */

// Zag popper (floating-ui) が位置追跡に使う。jsdom には無いので最小スタブ。
class ResizeObserverStub {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}
if (typeof globalThis.ResizeObserver === "undefined") {
  (globalThis as { ResizeObserver?: unknown }).ResizeObserver = ResizeObserverStub;
}
// Zag がリストのハイライト位置合わせに使う。jsdom の Element には無い。
if (typeof Element.prototype.scrollTo !== "function") {
  Element.prototype.scrollTo = function scrollTo(): void {};
}
if (typeof Element.prototype.scrollIntoView !== "function") {
  Element.prototype.scrollIntoView = function scrollIntoView(): void {};
}

let root: Root | null = null;
let container: HTMLElement | null = null;

type Item = { label: string; value: string };

const collection = createListCollection<Item>({
  items: [
    { label: "Alpha", value: "a" },
    { label: "Bravo", value: "b" },
    { label: "Charlie", value: "c" },
  ],
});

function renderSelect(options: {
  value?: string[];
  onValueChange?: (details: { value: string[] }) => void;
  disabled?: boolean;
}): HTMLElement {
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  const { value = ["a"], onValueChange, disabled } = options;
  act(() => {
    root!.render(
      <Select.Root
        collection={collection}
        value={value}
        disabled={disabled}
        onValueChange={onValueChange}
      >
        <Select.Control>
          <Select.ValueText placeholder="Pick one" />
          <Select.Indicator />
        </Select.Control>
        <Portal>
          <Select.Positioner>
            <Select.Content>
              <Select.List>
                {collection.items.map((item) => (
                  <Select.Item key={item.value} item={item}>
                    <Select.ItemText>{item.label}</Select.ItemText>
                    <Select.ItemIndicator />
                  </Select.Item>
                ))}
              </Select.List>
            </Select.Content>
          </Select.Positioner>
        </Portal>
      </Select.Root>,
    );
  });
  return container;
}

function trigger(): HTMLButtonElement {
  const button = document.body.querySelector<HTMLButtonElement>('button[role="combobox"]');
  if (button === null) {
    throw new Error("the select control did not render a combobox trigger");
  }
  return button;
}

function listbox(): HTMLElement | null {
  return document.body.querySelector<HTMLElement>('[role="listbox"]');
}

function press(key: string, fallbackTarget: Element): Promise<void> {
  return act(async () => {
    // 開いた後は focus が listbox (content) に移る。開閉に関わらず
    // 「今フォーカスのある要素」にキーを届かせるのが実操作と同じ経路。
    const target = document.activeElement ?? fallbackTarget;
    target.dispatchEvent(
      new KeyboardEvent("keydown", { key, bubbles: true, cancelable: true }),
    );
    // Zag は状態通知を microtask / rAF の後ろに流すので、act 内で回す。
    await Promise.resolve();
    await new Promise((resolve) => requestAnimationFrame(resolve));
  });
}

async function clickTrigger(button: HTMLButtonElement): Promise<void> {
  await act(async () => {
    button.click();
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

describe("Select.Control (issue #163)", () => {
  it("renders a combobox trigger inside the control", { timeout: 20000 }, () => {
    renderSelect({});
    const button = trigger();
    expect(button.getAttribute("aria-expanded")).toBe("false");
    expect(button.textContent).toContain("Alpha");
  });

  it("opens the listbox on click and selects with the mouse", { timeout: 20000 }, async () => {
    const onValueChange = vi.fn();
    renderSelect({ onValueChange });

    await clickTrigger(trigger());
    expect(trigger().getAttribute("aria-expanded")).toBe("true");
    expect(listbox()).not.toBeNull();

    const items = document.body.querySelectorAll<HTMLElement>('[role="option"]');
    expect(items.length).toBe(3);
    await act(async () => {
      items[1]!.click();
      await Promise.resolve();
      await new Promise((resolve) => requestAnimationFrame(resolve));
    });
    expect(onValueChange).toHaveBeenCalledWith(expect.objectContaining({ value: ["b"] }));
    expect(trigger().getAttribute("aria-expanded")).toBe("false");
  });

  it("selects with the keyboard: ArrowDown + Enter, closes with Escape", { timeout: 20000 }, async () => {
    const onValueChange = vi.fn();
    renderSelect({ onValueChange });
    const button = trigger();

    await act(async () => {
      button.focus();
      await Promise.resolve();
    });
    await press("ArrowDown", button);
    expect(button.getAttribute("aria-expanded")).toBe("true");

    await press("ArrowDown", button);
    await press("Enter", button);
    expect(onValueChange).toHaveBeenCalledWith(expect.objectContaining({ value: ["b"] }));

    await clickTrigger(button);
    expect(button.getAttribute("aria-expanded")).toBe("true");
    await press("Escape", button);
    expect(button.getAttribute("aria-expanded")).toBe("false");
  });

  it("keeps a disabled select closed", { timeout: 20000 }, async () => {
    renderSelect({ disabled: true });
    const button = trigger();
    expect(button.hasAttribute("disabled")).toBe(true);
    await clickTrigger(button);
    expect(button.getAttribute("aria-expanded")).toBe("false");
    expect(listbox()?.hasAttribute("hidden") ?? false).toBe(true);
  });
});
