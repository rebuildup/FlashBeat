// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it } from "vitest";
import { WelcomeHero } from "./welcome-hero";
import { SEARCH_INPUT_ID } from "@/app/topbar";

/**
 * Issue #215 — the visitor entry states the one default path and its CTA is
 * real in every catalog state: with rows it focuses the first row link,
 * without rows it focuses the search field. It never dead-ends.
 */

if (typeof Element.prototype.scrollIntoView !== "function") {
  Element.prototype.scrollIntoView = function scrollIntoView(): void {};
}

let root: Root | null = null;
let container: HTMLElement | null = null;

function renderHero(children: React.ReactNode): HTMLElement {
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  act(() => {
    root!.render(
      <div id="studio-content">
        <WelcomeHero />
        {children}
        <input id={SEARCH_INPUT_ID} placeholder="Song title or artist" />
      </div>,
    );
  });
  return container;
}

afterEach(() => {
  act(() => root?.unmount());
  container?.remove();
  root = null;
  container = null;
});

describe("WelcomeHero (issue #215)", () => {
  it("states the default path with a single primary CTA", () => {
    const dom = renderHero(null);
    const hero = dom.querySelector('[data-testid="welcome-hero"]');
    expect(hero).not.toBeNull();
    expect(hero!.textContent).toContain("曲を選んで");
    // One primary CTA in the hero.
    expect(dom.querySelectorAll('[data-testid="welcome-start"]').length).toBe(1);
  });

  it("the CTA focuses the first catalog row when rows exist", () => {
    const dom = renderHero(
      <a href="/chart?songId=who&version=1" data-row-link>
        Who
      </a>,
    );
    // jsdom has no layout: offsetParent is always null, so mark the row
    // visible the way the component detects visibility.
    const row = dom.querySelector("a[data-row-link]")!;
    Object.defineProperty(row, "offsetParent", { value: dom });
    const start = dom.querySelector<HTMLButtonElement>('[data-testid="welcome-start"]')!;
    act(() => {
      start.click();
    });
    expect(document.activeElement).toBe(row);
  });

  it("the CTA skips hidden rows and focuses the first visible one", () => {
    const dom = renderHero(
      <>
        <a href="/chart?songId=a&version=1" data-row-link>
          A
        </a>
        <a href="/chart?songId=b&version=1" data-row-link>
          B
        </a>
      </>,
    );
    // The hidden card row (base view) sits before the visible table row at
    // md+ widths; focus() on a hidden element is a no-op in a real browser.
    const rows = dom.querySelectorAll("a[data-row-link]");
    Object.defineProperty(rows[1], "offsetParent", { value: dom });
    const start = dom.querySelector<HTMLButtonElement>('[data-testid="welcome-start"]')!;
    act(() => {
      start.click();
    });
    expect(document.activeElement).toBe(rows[1]);
  });

  it("the CTA focuses the search field when no rows exist", () => {
    const dom = renderHero(null);
    const start = dom.querySelector<HTMLButtonElement>('[data-testid="welcome-start"]')!;
    act(() => {
      start.click();
    });
    expect(document.activeElement).toBe(dom.querySelector(`#${SEARCH_INPUT_ID}`));
  });
});
