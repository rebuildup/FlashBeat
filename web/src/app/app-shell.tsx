import { useCallback, useEffect, useMemo, useState } from "react";
import { filterAndSort, type SortKey } from "./catalog-view";
import { CatalogPanel, type CatalogStatus } from "./catalog-panel";
import { Topbar, SEARCH_INPUT_ID, type LevelFilter } from "./topbar";
import { contentArea } from "./shell-metrics";
import { WelcomeHero } from "@/components/welcome/welcome-hero";
import { loadCatalog, type CatalogMode } from "./catalog";
import { isAbortError, useLatestRequest } from "./request-guard";
import type { SongSummary } from "./types";

/**
 * The catalog page content (issue #166). Issue #801 moved the shared shell —
 * the navigation rail and the work area — into `pages/layout.tsx`, so this
 * component renders only what is unique to `/`: the hero, the filter
 * topbar and the scrolling list. The document-scrolling frame comes from
 * `pages/layout.tsx`, which picks the variant from the route.
 */
export function AppShell({
  mode,
  query: initialQuery = "",
  level: initialLevel = "all",
  sort: initialSort = "catalog",
}: {
  mode: CatalogMode;
  /** Initial filter values from the page loader (`/?query=…&level=…&sort=…`). */
  query?: string;
  level?: LevelFilter;
  sort?: SortKey;
}) {
  const [status, setStatus] = useState<CatalogStatus>("loading");
  const [songs, setSongs] = useState<SongSummary[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | undefined>(undefined);
  const [query, setQuery] = useState(initialQuery);
  const [level, setLevel] = useState<LevelFilter>(initialLevel);
  const [sort, setSort] = useState<SortKey>(initialSort);
  const [online, setOnline] = useState(() => navigator.onLine);

  /**
   * 最新の要求だけを反映するガード（`request-guard.ts`、issue #741）。
   * 新しい読み込みが始まれば前の要求を中断し、古い応答は捨てる。
   */
  const { begin, cancel } = useLatestRequest();

  const refresh = useCallback(() => {
    const request = begin();

    setStatus("loading");
    setErrorMessage(undefined);

    loadCatalog(mode, { signal: request.signal })
      .then((loaded) => {
        if (!request.isCurrent()) return;
        setSongs(loaded);
        setStatus(loaded.length === 0 ? "empty" : "ready");
      })
      .catch((error: unknown) => {
        // 中断は失敗ではない。次に始まる読み込みの結果を待つだけ。
        if (isAbortError(error)) return;
        if (!request.isCurrent()) return;
        setErrorMessage(error instanceof Error ? error.message : "不明なエラー");
        setStatus("error");
      });
  }, [mode, begin]);

  useEffect(() => {
    refresh();
    return () => {
      // 画面を離れるときに未完了の要求を中断する。
      cancel();
    };
  }, [refresh, cancel]);

  useEffect(() => {
    const goOnline = () => {
      setOnline(true);
      refresh();
    };
    const goOffline = () => setOnline(false);
    window.addEventListener("online", goOnline);
    window.addEventListener("offline", goOffline);
    return () => {
      window.removeEventListener("online", goOnline);
      window.removeEventListener("offline", goOffline);
    };
  }, [refresh]);

  /** `/` focuses the search field from anywhere outside a text input. */
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "/" || event.defaultPrevented) return;
      const target = event.target as HTMLElement | null;
      if (
        target &&
        (target.tagName === "INPUT" ||
          target.tagName === "TEXTAREA" ||
          target.tagName === "SELECT" ||
          target.isContentEditable)
      ) {
        return;
      }
      const input = document.getElementById(SEARCH_INPUT_ID);
      if (input === null) return;
      event.preventDefault();
      input.focus();
    };
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, []);

  const visibleSongs = useMemo(
    () => filterAndSort(songs, query, level, sort),
    [songs, query, level, sort],
  );

  const effectiveStatus: CatalogStatus = !online && status !== "loading" ? "offline" : status;

  /** Arrow keys move row focus inside the catalog list (rows are native links). */
  const onListKeyDown = useCallback((event: React.KeyboardEvent) => {
    if (event.key !== "ArrowDown" && event.key !== "ArrowUp") return;
    const rows = Array.from(
      event.currentTarget.querySelectorAll<HTMLAnchorElement>("a[data-row-link]"),
    );
    if (rows.length === 0) return;
    const current = rows.indexOf(document.activeElement as HTMLAnchorElement);
    const next =
      event.key === "ArrowDown"
        ? rows[Math.min(current + 1, rows.length - 1)]
        : rows[Math.max(current - 1, 0)];
    if (next === undefined || next === document.activeElement) return;
    event.preventDefault();
    next.focus();
    next.scrollIntoView({ block: "nearest" });
  }, []);

  // Issue #801: the shared shell (sidebar + main) lives in `pages/layout.tsx`
  // so it survives client-side navigation; this page renders only content.
  return (
    <>
      {/* Issue #215: the visitor entry sits above everything — one default
          path (曲を選ぶ → 譜面を試す) with a single primary CTA. */}
      <WelcomeHero />
      <Topbar
        title="曲を選ぶ"
        subtitle="リストから曲を選ぶと、その譜面が開きます。再生して試せます。"
        query={query}
        onQueryChange={setQuery}
        level={level}
        onLevelChange={setLevel}
        sort={sort}
        onSortChange={setSort}
      />

      <div className={contentArea}>
        <CatalogPanel
          status={effectiveStatus}
          songs={visibleSongs}
          total={songs.length}
          errorMessage={errorMessage}
          onRetry={refresh}
          hasFilters={query.trim().length > 0 || level !== "all"}
          onClearFilters={() => {
            setQuery("");
            setLevel("all");
          }}
          onListKeyDown={onListKeyDown}
        />
      </div>
    </>
  );
}
