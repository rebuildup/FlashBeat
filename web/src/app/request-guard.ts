import { useCallback, useRef } from "react";

/**
 * 「最新の要求だけを反映する」ための共有ガード（issue #741）。
 *
 * カタログ（`app-shell.tsx`）、ダッシュボード（`dashboard-panel.tsx`）、
 * 楽曲一覧（`songs-panel.tsx`）がそれぞれ持っていた requestId + AbortController
 * の組をここへ集約する。ルールは全画面で同じ:
 *
 * - `begin()` は前の要求を abort し、新しい `AbortController` と通し番号を
 *   発行する。返ってきた `isCurrent()` が false の応答は捨てる
 *   （古い応答が新しい応答を上書きしないため）。
 * - `cancel()` は未完了の要求を中断する。画面を離れるときに呼ぶ。
 * - abort は失敗ではない。`isAbortError` で見分けてエラー表示にしない。
 */
export function useLatestRequest() {
  const requestIdRef = useRef(0);
  const abortRef = useRef<AbortController | null>(null);

  const begin = useCallback(() => {
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    return {
      signal: controller.signal,
      isCurrent: () => requestIdRef.current === requestId,
    };
  }, []);

  const cancel = useCallback(() => {
    abortRef.current?.abort();
  }, []);

  return { begin, cancel };
}

/**
 * 失敗が「中断」かどうか。
 *
 * abort は失敗ではないので、UI は「取得できませんでした」とは表示しない。
 * `fetch` は中断時に `AbortError` の `DOMException` を投げるが、
 * 実装によって別の形になることもあるので `name` だけを見る。
 */
export function isAbortError(error: unknown): boolean {
  return (
    typeof error === "object" &&
    error !== null &&
    (error as { name?: unknown }).name === "AbortError"
  );
}
