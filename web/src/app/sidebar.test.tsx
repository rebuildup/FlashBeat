// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it } from "vitest";
import { Sidebar, NAV_STAFF_ITEMS } from "./sidebar";

/**
 * Issue #74 — the staff disclosure of the narrow rail.
 *
 * On a narrow viewport the five staff entries live behind the
 * `nav-staff-toggle` button, so the button has to flip `aria-expanded` and
 * the group that holds the entries has to become visible. The state is
 * carried by `data-open` (the same attribute the CSS keys off), which makes
 * the disclosure assertable in jsdom: the contract of the stylesheet is
 * injected below and the computed `display` of the group is read back.
 */

let root: Root | null = null;
let container: HTMLElement | null = null;
let style: HTMLStyleElement | null = null;

/**
 * The stylesheet contract of the disclosure, mirrored from `sidebar.tsx`:
 * `data-open="false"` hides the group, `data-open="true"` shows it.
 */
const CONTRACT_CSS = [
  '[data-staff-nav][data-open="false"] { display: none; }',
  '[data-staff-nav][data-open="true"] { display: grid; }',
].join("\n");

function renderSidebar(): HTMLElement {
  style = document.createElement("style");
  style.textContent = CONTRACT_CSS;
  document.head.appendChild(style);

  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  act(() => {
    root!.render(<Sidebar />);
  });
  return container;
}

function query<T extends Element>(selector: string): T {
  const element = document.body.querySelector<T>(selector);
  if (element === null) {
    throw new Error(`the element ${selector} is missing`);
  }
  return element;
}

function toggle(): HTMLButtonElement {
  return query<HTMLButtonElement>('[data-testid="nav-staff-toggle"]');
}

function staffGroup(): HTMLElement {
  return query<HTMLElement>("#studio-staff-nav");
}

afterEach(() => {
  if (root && container) {
    act(() => {
      root!.unmount();
    });
  }
  container?.remove();
  style?.remove();
  root = null;
  container = null;
  style = null;
});

describe("Sidebar staff disclosure (issue #74)", () => {
  it("starts closed with the staff entries hidden", () => {
    renderSidebar();

    expect(toggle().getAttribute("aria-expanded")).toBe("false");
    expect(staffGroup().getAttribute("data-open")).toBe("false");
    expect(getComputedStyle(staffGroup()).display).toBe("none");
  });

  it("flips aria-expanded and reveals the five staff entries", () => {
    renderSidebar();

    act(() => {
      toggle().click();
    });

    expect(toggle().getAttribute("aria-expanded")).toBe("true");
    expect(staffGroup().getAttribute("data-open")).toBe("true");
    expect(getComputedStyle(staffGroup()).display).toBe("grid");

    // The five staff entries are inside the revealed group and visible.
    const values = NAV_STAFF_ITEMS.map((item) => item.value);
    expect(values).toEqual(["dashboard", "songs", "new-song", "editor", "publish"]);
    for (const value of values) {
      const entry = query<HTMLElement>(`[data-testid="nav-${value}"]`);
      expect(staffGroup().contains(entry)).toBe(true);
      expect(getComputedStyle(entry).display).not.toBe("none");
    }
  });

  it("closes again on the second press and points at the group it controls", () => {
    renderSidebar();

    const button = toggle();
    expect(button.getAttribute("aria-controls")).toBe("studio-staff-nav");

    act(() => {
      button.click();
    });
    act(() => {
      button.click();
    });

    expect(button.getAttribute("aria-expanded")).toBe("false");
    expect(staffGroup().getAttribute("data-open")).toBe("false");
    expect(getComputedStyle(staffGroup()).display).toBe("none");
  });

  it("keeps the visitor entry reachable next to the toggle", () => {
    renderSidebar();

    const visitor = query<HTMLElement>('[data-testid="nav-visitor"]');
    expect(visitor.getAttribute("href")).toBe("/");
    expect(toggle().getAttribute("aria-expanded")).toBe("false");
  });
});
