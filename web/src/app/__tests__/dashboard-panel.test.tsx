// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { DashboardPanel } from "../dashboard-panel";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

/**
 * Issue #640 — `refresh()` の in-flight 対策。
 *
 * `app-shell.tsx` のカタログ読み込みと同じく、再読み込み・アンマウントで
 * 前の要求を abort し、中断をエラー表示にしないことを固定する。
 * ネットワークには出ない: `fetch` は応答を自分で決められる fake。
 */

const RANKINGS = {
  catalogVersion: 2,
  songs: [
    {
      songId: "who",
      title: "Who",
      artist: "Azari",
      level: 7,
      chartVersion: 1,
      plays: 3,
      entries: [{ nickname: "player1", score: 12345 }],
    },
  ],
};

const STATS = {
  generatedAt: "2025-01-01T00:00:00.000Z",
  totals: {
    songs: 1,
    liveSongs: 1,
    archivedSongs: 0,
    charts: 1,
    scores: 3,
    players: 1,
    playsLast24h: 0,
  },
  perSong: [],
  levelDistribution: [{ level: 7, songs: 1 }],
  topPlayers: [],
};

/** `/api/v1/rankings` と `/api/v1/stats` に対応する応答本文。 */
function bodyFor(url: string): unknown {
  return url.includes("/api/v1/rankings") ? RANKINGS : STATS;
}

interface PendingRequest {
  url: string;
  resolve: (body: unknown) => void;
  reject: (error: unknown) => void;
  signal: AbortSignal | undefined;
}

/** 応答を保留する `fetch` の fake。1 回の refresh で 2 件たまる。 */
function stubDeferredFetch() {
  const pending: PendingRequest[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn(
      (url: string, init?: RequestInit) =>
        new Promise((resolve, reject) => {
          pending.push({
            url,
            resolve: (body: unknown) => resolve({ ok: true, status: 200, json: async () => body }),
            reject,
            signal: init?.signal ?? undefined,
          });
        }),
    ),
  );
  return pending;
}

let root: Root | null = null;
let container: HTMLElement | null = null;

function renderPanel(): HTMLElement {
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  act(() => {
    root!.render(<DashboardPanel />);
  });
  return container;
}

function text(element: HTMLElement): string {
  return element.textContent ?? "";
}

function buttonByText(label: string): HTMLButtonElement {
  const button = Array.from(container!.querySelectorAll("button")).find((node) =>
    node.textContent?.includes(label),
  );
  if (!button) throw new Error(`button not found: ${label}`);
  return button as HTMLButtonElement;
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

describe("DashboardPanel の読み込み (issue #640)", () => {
  it("アンマウントで未完了の要求を中断する", () => {
    const pending = stubDeferredFetch();
    renderPanel();
    expect(pending).toHaveLength(2);
    expect(pending.every((request) => !request.signal?.aborted)).toBe(true);

    act(() => {
      root!.unmount();
    });
    root = null;
    container?.remove();
    container = null;

    expect(pending.every((request) => request.signal?.aborted === true)).toBe(true);
  });

  it("再試行で前の要求を中断し、中断はエラー表示にしない", async () => {
    const pending = stubDeferredFetch();
    const view = renderPanel();
    expect(pending).toHaveLength(2);

    // 1 回目の読み込みを失敗させてエラー表示にする。
    await act(async () => {
      pending[0]?.reject(new Error("ランキングAPIが500を返しました"));
    });
    expect(text(view)).toContain("ダッシュボードを読み込めませんでした");

    // 再試行: 新しい 2 件が始まり、残っていた前の要求は中断される。
    await act(async () => {
      buttonByText("再試行").click();
    });
    expect(pending).toHaveLength(4);
    expect(pending[0]?.signal?.aborted).toBe(true);
    expect(pending[1]?.signal?.aborted).toBe(true);
    expect(pending[2]?.signal?.aborted).toBe(false);
    expect(pending[3]?.signal?.aborted).toBe(false);

    // 新しい要求が返れば結果だけが残る。
    await act(async () => {
      pending[2]?.resolve(bodyFor(pending[2]!.url));
      pending[3]?.resolve(bodyFor(pending[3]!.url));
    });
    expect(text(view)).toContain("Who");
    expect(text(view)).not.toContain("ダッシュボードを読み込めませんでした");

    // 途中で残っていた前の stats 要求が遅れて返っても表示は変わらない
    // (Promise.all は既に reject 済みで、requestId も古い)。
    await act(async () => {
      pending[1]?.resolve(bodyFor(pending[1]!.url));
    });
    expect(text(view)).toContain("Who");
  });

  it("中断された要求はエラー表示にしない", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => {
        throw new DOMException("The operation was aborted.", "AbortError");
      }),
    );
    const view = renderPanel();

    await act(async () => {
      await Promise.resolve();
    });

    expect(text(view)).not.toContain("ダッシュボードを読み込めませんでした");
    expect(text(view)).toContain("ダッシュボードを読み込み中");
  });
});
