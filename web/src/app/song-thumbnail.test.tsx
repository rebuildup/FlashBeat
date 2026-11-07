// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it } from "vitest";
import { SongThumbnail, thumbnailState, youtubeThumbUrl } from "./song-thumbnail";

/**
 * Issue #156 — the catalog row thumbnail. Rendered with the real DOM
 * (jsdom) so the `error` fallback is exercised through React's event
 * system, not by poking component internals.
 */

let root: Root | null = null;
let container: HTMLElement | null = null;

function render(youtubeId: string, title = "テスト曲"): HTMLElement {
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  act(() => {
    root!.render(<SongThumbnail title={title} youtubeId={youtubeId} />);
  });
  return container;
}

function firstImage(scope: HTMLElement): HTMLImageElement | null {
  return scope.querySelector("img");
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

describe("youtubeThumbUrl", () => {
  it("builds the hqdefault URL from the youtube id", () => {
    expect(youtubeThumbUrl("dQw4w9WgXcQ")).toBe(
      "https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg",
    );
  });

  it("escapes the id into the URL path", () => {
    expect(youtubeThumbUrl("a/b c")).toBe("https://i.ytimg.com/vi/a%2Fb%20c/hqdefault.jpg");
  });
});

describe("thumbnailState", () => {
  it("hides when the id is empty, regardless of load state", () => {
    expect(thumbnailState("", false)).toBe("hidden");
    expect(thumbnailState("", true)).toBe("hidden");
  });

  it("shows the image until a load failure", () => {
    expect(thumbnailState("dQw4w9WgXcQ", false)).toBe("image");
    expect(thumbnailState("dQw4w9WgXcQ", true)).toBe("hidden");
  });
});

describe("SongThumbnail", () => {
  it("renders an img with the thumbnail URL, lazy loading and a titled alt", () => {
    const img = firstImage(render("dQw4w9WgXcQ"));
    expect(img).not.toBeNull();
    expect(img!.getAttribute("src")).toBe("https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg");
    expect(img!.getAttribute("alt")).toBe("テスト曲 thumbnail");
    expect(img!.getAttribute("loading")).toBe("lazy");
    expect(img!.getAttribute("decoding")).toBe("async");
  });

  it("renders no img at all when the youtube id is empty", () => {
    const mounted = render("");
    expect(firstImage(mounted)).toBeNull();
    expect(mounted.textContent).toBe("");
  });

  it("hides the image after a load error and keeps the row usable", () => {
    const mounted = render("dQw4w9WgXcQ");
    const img = firstImage(mounted);
    expect(img).not.toBeNull();

    act(() => {
      img!.dispatchEvent(new Event("error"));
    });

    expect(firstImage(mounted!)).toBeNull();
  });
});
