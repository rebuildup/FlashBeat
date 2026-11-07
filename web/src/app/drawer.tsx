import { useEffect, useRef, type RefObject } from "react";
import { css } from "styled-system/css";

/**
 * The off-canvas side panel of the chart editor and of the publish screen.
 *
 * Both screens keep a persistent inspector next to the work area while the
 * viewport is wide enough (`lg`, 1024px). Below that the inspector slides in
 * from the right over the work area. The panel keeps one single instance of
 * its content: the same element is the persistent column on wide viewports
 * and the drawer on narrow ones, so a form or a video preview is never
 * mounted twice.
 *
 * The keyboard model of the drawer:
 *
 * - opening it moves the focus to the first control inside the panel,
 * - Tab and Shift+Tab cycle inside the panel and never leave it,
 * - Escape closes the drawer,
 * - closing it gives the focus back to the element that opened it.
 */

/** Elements that can hold the keyboard focus inside the panel. */
export const FOCUSABLE_SELECTOR = [
  "a[href]",
  "button:not([disabled])",
  "input:not([disabled])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  '[tabindex]:not([tabindex="-1"])',
].join(", ");

/** Anything that can take the focus. */
export interface FocusTarget {
  focus(): void;
}

/**
 * The element the focus moves to when Tab leaves the panel.
 *
 * Pure on purpose: the trap is the part of the drawer a keyboard user feels
 * most, so it is unit tested without a browser.
 */
export function nextFocusTarget<T extends FocusTarget>(
  focusables: readonly T[],
  current: T | null,
  backwards: boolean,
): T | null {
  if (focusables.length === 0) {
    return null;
  }
  const index = current ? focusables.indexOf(current) : -1;
  if (index === -1) {
    return backwards ? focusables[focusables.length - 1] : focusables[0];
  }
  const step = backwards ? index - 1 : index + 1;
  return focusables[(step + focusables.length) % focusables.length];
}

/** A hidden element cannot take the focus, so it is no target of the trap. */
function isVisible(element: HTMLElement): boolean {
  if (typeof element.checkVisibility === "function") {
    return element.checkVisibility();
  }
  return element.getClientRects().length > 0;
}

/** The focusable elements of the panel, in tab order. */
export function focusableWithin(panel: HTMLElement): HTMLElement[] {
  const found = Array.from(panel.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR));
  return found.filter((element) => isVisible(element));
}

export interface DrawerFocusOptions {
  /** The drawer is open. */
  open: boolean;
  /** The element that holds the drawer content. */
  panelRef: RefObject<HTMLElement | null>;
  /** Called when the drawer asks to be closed. */
  onClose(): void;
}

/**
 * Keeps the keyboard inside the drawer and hands the focus back to the
 * trigger when the drawer closes.
 */
export function useDrawerFocus({ open, panelRef, onClose }: DrawerFocusOptions): void {
  const restoreRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!open) {
      return;
    }
    const previous = document.activeElement;
    restoreRef.current = previous instanceof HTMLElement ? previous : null;
    const panel = panelRef.current;
    const first = panel ? focusableWithin(panel)[0] : null;
    (first ?? panel)?.focus();
    return () => {
      const trigger = restoreRef.current;
      restoreRef.current = null;
      if (trigger && trigger.isConnected) {
        trigger.focus();
      }
    };
  }, [open, panelRef]);

  useEffect(() => {
    if (!open) {
      return;
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        event.stopPropagation();
        onClose();
        return;
      }
      if (event.key !== "Tab") {
        return;
      }
      const panel = panelRef.current;
      if (!panel) {
        return;
      }
      const focusables = focusableWithin(panel);
      if (focusables.length === 0) {
        event.preventDefault();
        panel.focus();
        return;
      }
      const target = nextFocusTarget(
        focusables,
        document.activeElement instanceof HTMLElement ? document.activeElement : null,
        event.shiftKey,
      );
      if (!target) {
        return;
      }
      event.preventDefault();
      target.focus();
    };
    window.addEventListener("keydown", onKeyDown, true);
    return () => window.removeEventListener("keydown", onKeyDown, true);
  }, [open, onClose, panelRef]);
}

/** The dimmed layer behind the drawer; clicking it closes the drawer. */
export const drawerBackdrop = css({
  bg: "black/50",
  display: { base: "block", lg: "none" },
  inset: "0",
  position: "fixed",
  zIndex: "modal",
});

export interface DrawerBackdropProps {
  onClose(): void;
}

/** The dimmed layer behind the drawer, hidden on wide viewports. */
export function DrawerBackdrop({ onClose }: DrawerBackdropProps) {
  return (
    <div
      aria-hidden="true"
      className={drawerBackdrop}
      onClick={onClose}
      data-testid="drawer-backdrop"
    />
  );
}
