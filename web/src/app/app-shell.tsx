import { useCallback, useEffect, useMemo, useState } from "react";
import { css } from "styled-system/css";
import { CatalogPanel, type CatalogStatus } from "./catalog-panel";
import { Sidebar } from "./sidebar";
import { LEVEL_FILTERS, Topbar, type LevelFilter } from "./topbar";
import { loadCatalog, type CatalogMode } from "./catalog";
import type { SongSummary } from "./types";

const shell = css({
  display: "grid",
  gridTemplateColumns: "13rem minmax(0, 1fr)",
  height: "100dvh",
  overflow: "hidden",
});

const main = css({
  display: "flex",
  flexDirection: "column",
  minHeight: "0",
  minWidth: "0",
});

const content = css({
  display: "flex",
  flex: "1",
  flexDirection: "column",
  gap: "3",
  minHeight: "0",
  overflowY: "auto",
  p: "4",
});

function matchesLevel(level: number, filter: LevelFilter) {
  if (filter === "all") return true;
  if (filter === "low") return level < 10;
  if (filter === "mid") return level >= 10 && level < 20;
  return level >= 20;
}

export function AppShell({ mode }: { mode: CatalogMode }) {
  const [status, setStatus] = useState<CatalogStatus>("loading");
  const [songs, setSongs] = useState<SongSummary[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | undefined>(undefined);
  const [query, setQuery] = useState("");
  const [level, setLevel] = useState<LevelFilter>("all");

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

  const visibleSongs = useMemo(
    () =>
      songs.filter((song) => {
        const needle = query.trim().toLowerCase();
        const matchesQuery =
          needle.length === 0 ||
          song.title.toLowerCase().includes(needle) ||
          song.artist.toLowerCase().includes(needle);
        return matchesQuery && matchesLevel(song.level, level);
      }),
    [songs, query, level],
  );

  return (
    <div className={shell}>
      <Sidebar />

      <main id="studio-content" className={main}>
        <Topbar
          title="Catalog"
          subtitle="Find a published song to work on its chart."
          query={query}
          onQueryChange={setQuery}
          level={level}
          onLevelChange={setLevel}
        />

        <div className={content}>
          <CatalogPanel
            status={status}
            songs={visibleSongs}
            errorMessage={errorMessage}
            onRetry={refresh}
          />
        </div>
      </main>
    </div>
  );
}

export { LEVEL_FILTERS };
