import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { cx, css } from "styled-system/css";
import { filterAndSort, type SortKey } from "./catalog-view";
import { CatalogPanel, type CatalogStatus } from "./catalog-panel";
import { Topbar, SEARCH_INPUT_ID, type LevelFilter } from "./topbar";
import { Sidebar } from "./sidebar";
import { WelcomeHero } from "@/components/welcome/welcome-hero";
import { loadCatalog, type CatalogMode } from "./catalog";
import type { SongSummary } from "./types";

/**
 * The shell every Studio screen shares (issue #166): the navigation rail on
 * the start side and the work area next to it. Narrow viewports: the shell is
 * a plain block so the navigation can stick to the top of the document while
 * the page scrolls. Wide viewports: the grid of the rail and the work area.
 *
 * `scroll` selects how the work area behaves:
 * - `true` (default): the shell scrolls the page content (catalog, forms).
 * - `false`: the shell is locked to the viewport and the page manages its own
 *   scrolling (the chart editor draws into a canvas and must not scroll).
 */
const shell = css({
  display: { base: "block", md: "grid" },
  gridTemplateColumns: { base: "1fr", md: "13rem minmax(0, 1fr)" },
  gridTemplateRows: { base: "auto 1fr", md: "1fr" },
  minHeight: "100dvh",
});

const shellFixed = css({
  height: "100dvh",
  overflow: "hidden",
});

const main = css({
  display: "flex",
  flexDirection: "column",
  minHeight: "0",
  minWidth: "0",
});

const mainFixed = css({
  height: "full",
});

/** Work area of scrolling pages: padded, one inner scroll area. */
const content = css({
  display: "flex",
  flex: "1",
  flexDirection: "column",
  gap: "3",
  minHeight: "0",
  overflowY: "auto",
  p: "4",
});

export function StudioShell({
  children,
  scroll = true,
}: {
  children: ReactNode;
  /** `false` locks the shell to the viewport (chart editor, canvas pages). */
  scroll?: boolean;
}) {
  return (
    <div className={cx(shell, !scroll && shellFixed)}>
      <Sidebar />
      <main id="studio-content" className={cx(main, !scroll && mainFixed)}>
        {children}
      </main>
    </div>
  );
}

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

  const refresh = useCallback(() => {
    setStatus("loading");
    setErrorMessage(undefined);

    loadCatalog(mode)
      .then((loaded) => {
        setSongs(loaded);
        setStatus(loaded.length === 0 ? "empty" : "ready");
      })
      .catch((error: unknown) => {
        setErrorMessage(error instanceof Error ? error.message : "Unknown error");
        setStatus("error");
      });
  }, [mode]);

  useEffect(() => {
    refresh();
  }, [refresh]);

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

  return (
    <StudioShell>
      {/* Issue #215: the visitor entry sits above everything — one default
          path (曲を選ぶ → 譜面を試す) with a single primary CTA. */}
      <WelcomeHero />
      <Topbar
        title="曲を選ぶ / Pick a song"
        subtitle="リストから曲を選ぶと、その譜面が開きます。再生して試せます。"
        query={query}
        onQueryChange={setQuery}
        level={level}
        onLevelChange={setLevel}
        sort={sort}
        onSortChange={setSort}
      />

      <div className={content}>
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
    </StudioShell>
  );
}
