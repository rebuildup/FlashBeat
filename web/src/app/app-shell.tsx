import { Tabs, Toaster, toaster } from "@/components/ui";
import { useCallback, useEffect, useMemo, useState } from "react";
import { css } from "styled-system/css";
import { CatalogPanel, type CatalogStatus } from "./catalog-panel";
import { EditorPanel } from "./editor-panel";
import { PublishPanel } from "./publish-panel";
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

const tabContent = css({
  display: "flex",
  flex: "1",
  flexDirection: "column",
  minHeight: "0",
});

const TAB_TITLES = {
  catalog: {
    title: "Catalog",
    subtitle: "Songs published for the festival build",
  },
  editor: {
    title: "Chart editor",
    subtitle: "Eight lanes, tap notes, one chart version at a time",
  },
  publish: {
    title: "Publish",
    subtitle: "Metadata and chart snapshot in a single request",
  },
} as const;

type TabValue = keyof typeof TAB_TITLES;

function matchesLevel(level: number, filter: LevelFilter) {
  if (filter === "all") return true;
  if (filter === "low") return level < 10;
  if (filter === "mid") return level >= 10 && level < 20;
  return level >= 20;
}

export function AppShell({ mode }: { mode: CatalogMode }) {
  const [tab, setTab] = useState<TabValue>("catalog");
  const [status, setStatus] = useState<CatalogStatus>("loading");
  const [songs, setSongs] = useState<SongSummary[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | undefined>(undefined);
  const [query, setQuery] = useState("");
  const [level, setLevel] = useState<LevelFilter>("all");

  // The published songs come from the API of the contract. An empty origin
  // means the same origin as the studio, which is how the worker is deployed.
  const apiOrigin = import.meta.env.VITE_FLASHBEAT_API_ORIGIN ?? "";

  const refresh = useCallback(() => {
    setStatus("loading");
    setErrorMessage(undefined);

    loadCatalog(mode, apiOrigin)
      .then((loaded) => {
        setSongs(loaded);
        setStatus(loaded.length === 0 ? "empty" : "ready");
      })
      .catch((error: unknown) => {
        setErrorMessage(error instanceof Error ? error.message : "Unknown error");
        setStatus("error");
      });
  }, [apiOrigin, mode]);

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

  const heading = TAB_TITLES[tab];

  return (
    <Tabs.Root
      className={shell}
      orientation="vertical"
      value={tab}
      onValueChange={(details) => setTab(details.value as TabValue)}
    >
      <Sidebar />

      <div className={main}>
        <Topbar
          title={heading.title}
          subtitle={heading.subtitle}
          query={query}
          onQueryChange={setQuery}
          level={level}
          onLevelChange={setLevel}
          refreshing={status === "loading"}
          onRefresh={() => {
            refresh();
            toaster.create({
              title: "Catalog refreshed",
              description: "Reloaded the published song list.",
              type: "success",
            });
          }}
          onPublish={() => setTab("publish")}
        />

        <div className={content}>
          <Tabs.Content value="catalog" className={tabContent}>
            <CatalogPanel
              status={status}
              songs={visibleSongs}
              errorMessage={errorMessage}
              onRetry={refresh}
            />
          </Tabs.Content>

          <Tabs.Content value="editor" className={tabContent}>
            <EditorPanel />
          </Tabs.Content>

          <Tabs.Content value="publish" className={tabContent}>
            <PublishPanel />
          </Tabs.Content>
        </div>
      </div>

      <Toaster />
    </Tabs.Root>
  );
}

export { LEVEL_FILTERS };
