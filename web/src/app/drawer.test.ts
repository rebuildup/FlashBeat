import { describe, expect, it } from "vitest";
import { nextFocusTarget } from "./drawer";

/** A stand-in for a DOM element: the trap only ever calls `focus()`. */
function element(name: string) {
  return {
    name,
    focused: false,
    focus() {
      this.focused = true;
    },
  };
}

describe("nextFocusTarget", () => {
  it("moves to the next control and wraps around at the end", () => {
    const first = element("first");
    const second = element("second");
    const third = element("third");
    const focusables = [first, second, third];

    expect(nextFocusTarget(focusables, first, false)).toBe(second);
    expect(nextFocusTarget(focusables, second, false)).toBe(third);
    expect(nextFocusTarget(focusables, third, false)).toBe(first);
  });

  it("moves to the previous control and wraps around at the start", () => {
    const first = element("first");
    const second = element("second");
    const third = element("third");
    const focusables = [first, second, third];

    expect(nextFocusTarget(focusables, second, true)).toBe(first);
    expect(nextFocusTarget(focusables, first, true)).toBe(third);
  });

  it("takes the first control when the focus is not inside the panel yet", () => {
    const first = element("first");
    const outside = element("outside");
    const focusables = [first, element("second")];

    expect(nextFocusTarget(focusables, outside, false)).toBe(first);
    expect(nextFocusTarget(focusables, null, false)).toBe(first);
  });

  it("has no target when the panel holds no control", () => {
    expect(nextFocusTarget([], null, false)).toBeNull();
  });
});
