// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AppShell } from "../app-shell";
import type { SongSummary } from "../types";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

/**
 * Issue #68 — カタログ読み込みの競合対策 (abort + 古い応答の棄却)。
 *
 * 読み込みを 2 回重ねたとき、古い応答が新しい応答を上書きしないこと、
 * そして中断 (abort) が「カタログを取得できません」という失敗表示に
 * 変わらないことを固定する。ネットワークには出ない: `fetch` は fake。
 */

const songA: SongSummary = {
  id: "older",
  title: "古い曲",
  artist: "Artist A",
  youtubeId: "AAAAAAAAAAA",
  level: 3,
  bpm: 120,
  durationSec: 60,
  chartVersion: 1,
  noteCount: 100,
};

const songB: SongSummary = {
  id: "newer",
  title: "新しい曲",
  artist: "Artist B",
  youtubeId: "BBBBBBBBBBB",
  level: 7,
  bpm: 180,
  durationSec: 120,
  chartVersion: 4,
  noteCount: 300,
};

let root: Root | null = null;
let container: HTMLElement | null = null;

/** 保留中の要求 1 件分。 */
interface PendingRequest {
  resolve: (body: unknown) => void;
  reject: (error: unknown) => void;
  signal: AbortSignal | undefined;
}

/**
 * 応答を自分で決められる `fetch` の fake。
 *
 * `honorAbort` が真なら、本物の `fetch` と同じく中断された要求は
 * `AbortError` で失敗する。偽の要求は中断を無視する (サーバーが
 * 応答を返してくる場合) ので、古い応答の棄却を確かめられる。
 */
function stubDeferredFetch(options: { honorAbort?: boolean } = {}) {
  const pending: PendingRequest[] = [];
  const fetchMock = vi.fn(
    (_url: string, init?: RequestInit) =>
      new Promise((resolve, reject) => {
        const request: PendingRequest = {
          resolve: (body: unknown) =>
            resolve({
              ok: true,
              status: 200,
              json: async () => body,
            }),
          reject,
          signal: init?.signal ?? undefined,
        };
        pending.push(request);

        if (options.honorAbort === true) {
          init?.signal?.addEventListener("abort", () => {
            reject(new DOMException("The operation was aborted.", "AbortError"));
          });
        }
      }),
  );
  vi.stubGlobal("fetch", fetchMock);
  return pending;
}

/** `AppShell` を描画して、DOM を返す。 */
function renderShell(): HTMLElement {
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  act(() => {
    root!.render(<AppShell mode="loading" />);
  });
  return container;
}

/** 画面上のテキスト。 */
function text(element: HTMLElement): string {
  return element.textContent ?? "";
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
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("AppShell のカタログ読み込み (issue #68)", () => {
  it("古い応答が新しい応答を上書きしない", async () => {
    const pending = stubDeferredFetch();
    const view = renderShell();

    // 1 回目の読み込みが走っている。
    expect(pending).toHaveLength(1);

    // 2 回目の読み込みを始める (オンラインに戻ったときと同じ道)。
    await act(async () => {
      window.dispatchEvent(new Event("online"));
    });
    expect(pending).toHaveLength(2);

    // 古い要求は中断されている。
    expect(pending[0]?.signal?.aborted).toBe(true);
    expect(pending[1]?.signal?.aborted).toBe(false);

    // 新しい要求が先に返る。
    await act(async () => {
      pending[1]?.resolve({ catalogVersion: 2, songs: [songB] });
    });
    expect(text(view)).toContain(songB.title);

    // 古い要求が後から返っても、表示は新しい方のまま。
    await act(async () => {
      pending[0]?.resolve({ catalogVersion: 1, songs: [songA] });
    });
    expect(text(view)).toContain(songB.title);
    expect(text(view)).not.toContain(songA.title);
  });

  it("中断された要求はエラー表示にしない", async () => {
    // 最新の要求がそのまま中断されたケース (他の要求に置き換わっていない)。
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => {
        throw new DOMException("The operation was aborted.", "AbortError");
      }),
    );
    const view = renderShell();

    await act(async () => {
      await Promise.resolve();
    });

    expect(text(view)).not.toContain("Catalog unavailable");
    expect(text(view)).toContain("Loading catalog");
  });

  it("次の読み込みで中断された要求はエラー表示にしない", async () => {
    const pending = stubDeferredFetch({ honorAbort: true });
    const view = renderShell();

    expect(pending).toHaveLength(1);

    // 2 回目の読み込みで 1 回目が中断される。
    await act(async () => {
      window.dispatchEvent(new Event("online"));
    });

    expect(text(view)).not.toContain("Catalog unavailable");
    expect(text(view)).toContain("Loading catalog");

    // 新しい要求が成功すれば、その結果だけが残る。
    await act(async () => {
      pending[1]?.resolve({ catalogVersion: 2, songs: [songB] });
    });
    expect(text(view)).toContain(songB.title);
    expect(text(view)).not.toContain("Catalog unavailable");
  });

  it("本当に失敗した要求はエラー表示のまま", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => ({
        ok: false,
        status: 500,
        json: async () => ({ error: "boom" }),
      })),
    );
    const view = renderShell();

    await act(async () => {
      await Promise.resolve();
    });

    expect(text(view)).toContain("Catalog unavailable");
  });
});
