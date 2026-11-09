import { useCallback, useEffect, useMemo, useState } from "react";
import { css } from "styled-system/css";
import { Button } from "@/components/ui";
import {
  Archive,
  ChartColumnBig,
  Clock,
  FileClock,
  ListMusic,
  Music,
  Trophy,
  Users,
  type LucideIcon,
} from "lucide-react";
import { loadRankings, loadStats } from "@/lib/dashboard/client";
import type { RankingSong, StatsResponse } from "@/lib/dashboard/types";
import { StateBlock, panelFrame, panelHeader, panelSubtitle, panelTitle } from "./panel";

/**
 * The festival dashboard (issue #151).
 *
 * Three things the operator wants to see at a glance:
 *
 *   - **the ranking** of every live song (the best scores of its published
 *     chart version), sorted by the best score;
 *   - **the statistics**: how many songs are live, how many plays happened,
 *     how many players there are and how good they are;
 *   - **the difficulty axis**: how the songs are distributed over the levels.
 *
 * The whole page is filled from two requests (`/api/v1/rankings` and
 * `/api/v1/stats`); a song without a score is still listed, it just has no
 * entries.
 */
export type DashboardStatus = "loading" | "ready" | "error";

/** How many entries of a song the ranking board shows. */
const ENTRIES_PER_SONG = 5;

const page = css({
  display: "flex",
  flexDirection: "column",
  gap: "5",
  minHeight: "0",
});

const tiles = css({
  display: "grid",
  gap: "3",
  gridTemplateColumns: { base: "repeat(2, minmax(0, 1fr))", md: "repeat(3, minmax(0, 1fr))", xl: "repeat(6, minmax(0, 1fr))" },
});

const tile = css({
  bg: "gray.1",
  borderColor: "border",
  borderRadius: "l2",
  borderWidth: "1px",
  display: "flex",
  flexDirection: "column",
  gap: "2",
  p: "4",
  transition: "background 120ms ease-out",
  _hover: { bg: "gray.2" },
});

/** The icon of a stat tile, on a tinted chip so it reads at a glance. */
const tileChip = css({
  alignItems: "center",
  bg: "blue.a4",
  borderRadius: "l2",
  color: "blue.11",
  display: "inline-flex",
  height: "8",
  justifyContent: "center",
  width: "8",
});

const tileValue = css({
  color: "fg.default",
  fontVariantNumeric: "tabular-nums",
  fontSize: "2xl",
  fontWeight: "semibold",
  letterSpacing: "-0.01em",
  lineHeight: "1",
});

const tileLabel = css({
  color: "fg.muted",
  fontSize: "xs",
  fontWeight: "medium",
  letterSpacing: "0.04em",
  textTransform: "uppercase",
});

/** The heading of a panel: an icon and the title on one line. */
const titleRow = css({
  alignItems: "center",
  columnGap: "2",
  display: "flex",
});

const titleIcon = css({
  color: "blue.11",
});

const columns = css({
  alignItems: "start",
  display: "grid",
  gap: "4",
  gridTemplateColumns: { base: "minmax(0, 1fr)", lg: "minmax(0, 3fr) minmax(0, 2fr)" },
});

/**
 * The body of a panel that grows with its content.
 *
 * `panelBody` is a scroll area (the catalog list scrolls inside a fixed
 * frame). The dashboard panels are short and their content must be fully
 * visible, so they use this variant instead.
 */
const panelBodyAuto = css({
  p: "4",
});

const section = css({
  display: "flex",
  flexDirection: "column",
  gap: "4",
  minWidth: "0",
});

const rows = css({
  display: "flex",
  flexDirection: "column",
});

const row = css({
  alignItems: "center",
  borderBottomColor: "border",
  borderBottomWidth: "1px",
  columnGap: "4",
  display: "grid",
  gridTemplateColumns: "minmax(0, 1fr) 4rem 6rem 7rem",
  minHeight: "2.75rem",
  px: "1",
  transition: "background 100ms ease-out",
  _last: { borderBottomWidth: "0" },
  _hover: { bg: "gray.2" },
});

const rowHeader = css({
  borderBottomColor: "border",
  borderBottomWidth: "1px",
  color: "fg.subtle",
  columnGap: "4",
  display: "grid",
  fontSize: "xs",
  fontWeight: "medium",
  gridTemplateColumns: "minmax(0, 1fr) 4rem 6rem 7rem",
  letterSpacing: "0.04em",
  paddingBottom: "2",
  paddingTop: "1",
  textTransform: "uppercase",
});

const songCell = css({
  display: "flex",
  flexDirection: "column",
  minWidth: "0",
});

const songTitle = css({
  color: "fg.default",
  fontSize: "sm",
  fontWeight: "medium",
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "nowrap",
});

const songMeta = css({
  color: "fg.muted",
  fontSize: "xs",
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "nowrap",
});

const num = css({
  color: "fg.muted",
  fontVariantNumeric: "tabular-nums",
  fontSize: "sm",
  textAlign: "right",
  whiteSpace: "nowrap",
});

/** The rank badge of a ranking entry. */
const rankBadge = css({
  alignItems: "center",
  bg: "gray.4",
  borderRadius: "l1",
  color: "fg.muted",
  display: "inline-flex",
  flexShrink: 0,
  fontVariantNumeric: "tabular-nums",
  fontSize: "xs",
  fontWeight: "semibold",
  height: "5",
  justifyContent: "center",
  width: "5",
});

/** The badge of the first place. */
const rankBadgeFirst = css({
  bg: "blue.a4",
  color: "blue.11",
});

/** One entry of a song in the ranking board. */
const entryRow = css({
  alignItems: "center",
  columnGap: "2",
  display: "flex",
  minWidth: "0",
});

/** The name of the player in a ranking entry. */
const entryName = css({
  color: "fg.default",
  fontSize: "xs",
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "nowrap",
});

/** The score of a ranking entry. */
const entryScore = css({
  color: "fg.muted",
  fontVariantNumeric: "tabular-nums",
  fontSize: "xs",
  marginLeft: "auto",
  whiteSpace: "nowrap",
});

const barTrack = css({
  bg: "gray.3",
  borderRadius: "full",
  height: "2",
  overflow: "hidden",
  width: "full",
});

const barFill = css({
  bg: "blue.9",
  borderRadius: "full",
  height: "full",
  minWidth: "2",
});

const levelRow = css({
  alignItems: "center",
  columnGap: "3",
  display: "grid",
  gridTemplateColumns: "3rem minmax(0, 1fr) 2rem",
  minHeight: "1.75rem",
});

const levelLabel = css({
  color: "fg.muted",
  fontSize: "xs",
  fontVariantNumeric: "tabular-nums",
});

const notice = css({
  color: "fg.muted",
  fontSize: "sm",
});

/** `1 play` / `3 plays`. */
function plural(count: number, word: string): string {
  return `${count} ${count === 1 ? word : `${word}s`}`;
}

/** `887769` → `887,769`. */
function formatScore(score: number): string {
  return score.toLocaleString("en-US");
}

/** `0.887769…` → `88.8%`. */
function formatAccuracy(accuracy: number | null): string {
  if (accuracy === null) return "—";
  return `${(accuracy * 100).toFixed(1)}%`;
}

export function DashboardPanel() {
  const [status, setStatus] = useState<DashboardStatus>("loading");
  const [errorMessage, setErrorMessage] = useState<string | undefined>(undefined);
  const [rankings, setRankings] = useState<RankingSong[]>([]);
  const [stats, setStats] = useState<StatsResponse | null>(null);

  const refresh = useCallback(() => {
    setStatus("loading");
    setErrorMessage(undefined);

    Promise.all([loadRankings(), loadStats()])
      .then(([board, statistics]) => {
        setRankings(board.songs);
        setStats(statistics);
        setStatus("ready");
      })
      .catch((error: unknown) => {
        setErrorMessage(error instanceof Error ? error.message : "Unknown error");
        setStatus("error");
      });
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  /** The songs of the ranking board, best score first. */
  const orderedSongs = useMemo(() => {
    const bestScoreOf = (song: RankingSong) => song.entries[0]?.score ?? -1;
    return [...rankings].sort(
      (a, b) => bestScoreOf(b) - bestScoreOf(a) || b.plays - a.plays || a.title.localeCompare(b.title),
    );
  }, [rankings]);

  const levelBars = useMemo(() => {
    const distribution = stats?.levelDistribution ?? [];
    const max = distribution.reduce((highest, entry) => Math.max(highest, entry.songs), 0);
    return { distribution, max };
  }, [stats]);

  if (status === "error") {
    return (
      <div className={panelFrame}>
        <StateBlock
          title="The dashboard could not be loaded"
          description="The API answered something the dashboard could not read."
          action={
            <Button onClick={refresh} variant="outline">
              Retry
            </Button>
          }
        />
        {errorMessage === undefined ? null : <p className={notice}>{errorMessage}</p>}
      </div>
    );
  }

  if (status === "loading" || stats === null) {
    return (
      <div className={panelFrame}>
        <StateBlock title="Loading the dashboard" description="Reading the scores and the songs." />
      </div>
    );
  }

  return (
    <div className={page}>
      <section className={tiles}>
        <Tile icon={Music} label="Live songs" value={stats.totals.liveSongs} />
        <Tile icon={Archive} label="Archived" value={stats.totals.archivedSongs} />
        <Tile icon={FileClock} label="Chart versions" value={stats.totals.charts} />
        <Tile icon={Trophy} label="Plays" value={stats.totals.scores} />
        <Tile icon={Users} label="Players" value={stats.totals.players} />
        <Tile icon={Clock} label="Plays (24h)" value={stats.totals.playsLast24h} />
      </section>

      <div className={columns}>
        <section className={section}>
          <div className={panelFrame}>
            <div className={panelHeader}>
              <Heading icon={Trophy} title="Ranking" subtitle="Best score of the published chart." />
            </div>
            <div className={panelBodyAuto}>
              {orderedSongs.length === 0 ? (
                <p className={notice}>No song is published yet.</p>
              ) : (
                <div className={rows}>
                  {orderedSongs.map((song) => (
                    <SongRanking key={song.songId} song={song} />
                  ))}
                </div>
              )}
            </div>
          </div>

          <div className={panelFrame}>
            <div className={panelHeader}>
              <Heading
                icon={ListMusic}
                title="Songs in detail"
                subtitle="Plays, players and the average accuracy."
              />
            </div>
            <div className={panelBodyAuto}>
              <div className={rowHeader}>
                <span>Song</span>
                <span className={num}>Plays</span>
                <span className={num}>Best</span>
                <span className={num}>Accuracy</span>
              </div>
              <div className={rows}>
                {stats.perSong.map((song) => (
                  <div key={song.songId} className={row}>
                    <div className={songCell}>
                      <span className={songTitle}>
                        {song.title}
                        {song.archived ? " (archived)" : ""}
                      </span>
                      <span className={songMeta}>
                        Lv.{song.level} · {song.artist} · {plural(song.plays, "play")} ·{" "}
                        {plural(song.distinctPlayers, "player")}
                      </span>
                    </div>
                    <span className={num}>{song.plays}</span>
                    <span className={num}>
                      {song.bestScore === null ? "—" : formatScore(song.bestScore)}
                    </span>
                    <span className={num}>{formatAccuracy(song.averageAccuracy)}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </section>

        <section className={section}>
          <div className={panelFrame}>
            <div className={panelHeader}>
              <Heading
                icon={Users}
                title="Best players"
                subtitle="Total score of every player."
              />
            </div>
            <div className={panelBodyAuto}>
              {stats.topPlayers.length === 0 ? (
                <p className={notice}>Nobody has played yet.</p>
              ) : (
                <>
                  <div className={rowHeader}>
                    <span>Player</span>
                    <span className={num}>Plays</span>
                    <span className={num}>Total</span>
                    <span className={num}>Best</span>
                  </div>
                  <div className={rows}>
                    {stats.topPlayers.map((player, index) => (
                      <div key={player.nickname} className={row}>
                        <div className={songCell}>
                          <span className={songTitle}>
                            <span className={index === 0 ? rankBadgeFirst : rankBadge}>{index + 1}</span>{" "}
                            {player.nickname}
                          </span>
                          <span className={songMeta}>{plural(player.plays, "play")}</span>
                        </div>
                        <span className={num}>{player.plays}</span>
                        <span className={num}>{formatScore(player.totalScore)}</span>
                        <span className={num}>{formatScore(player.bestScore)}</span>
                      </div>
                    ))}
                  </div>
                </>
              )}
            </div>
          </div>

          <div className={panelFrame}>
            <div className={panelHeader}>
              <Heading
                icon={ChartColumnBig}
                title="Difficulty"
                subtitle="Songs per level."
              />
            </div>
            <div className={panelBodyAuto}>
              {levelBars.distribution.length === 0 ? (
                <p className={notice}>No song is published yet.</p>
              ) : (
                <div className={rows}>
                  {levelBars.distribution.map((entry) => (
                    <div key={entry.level} className={levelRow}>
                      <span className={levelLabel}>Lv.{entry.level}</span>
                      <span className={barTrack}>
                        <span
                          className={barFill}
                          style={{
                            width: `${
                              levelBars.max === 0 ? 0 : (entry.songs / levelBars.max) * 100
                            }%`,
                          }}
                        />
                      </span>
                      <span className={num}>{entry.songs}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </section>
      </div>
    </div>
  );
}

/**
 * The heading of a panel.
 *
 * The icon tells the panels apart at a glance; the subtitle explains what
 * the panel shows.
 */
function Heading({
  icon: Icon,
  title,
  subtitle,
}: {
  icon: LucideIcon;
  title: string;
  subtitle: string;
}) {
  return (
    <div>
      <div className={titleRow}>
        <Icon className={titleIcon} size={16} aria-hidden="true" />
        <h2 className={panelTitle}>{title}</h2>
      </div>
      <p className={panelSubtitle}>{subtitle}</p>
    </div>
  );
}

function Tile({
  icon: Icon,
  label,
  value,
}: {
  icon: LucideIcon;
  label: string;
  value: number;
}) {
  return (
    <div className={tile}>
      <span className={tileChip}>
        <Icon size={16} aria-hidden="true" />
      </span>
      <span className={tileValue}>{formatScore(value)}</span>
      <span className={tileLabel}>{label}</span>
    </div>
  );
}

function SongRanking({ song }: { song: RankingSong }) {
  return (
    <div className={row} style={{ gridTemplateColumns: "minmax(0, 1fr) 12rem" }}>
      <div className={songCell}>
        <span className={songTitle}>{song.title}</span>
        <span className={songMeta}>
          Lv.{song.level} · {song.artist} · {plural(song.plays, "play")}
        </span>
      </div>
      <div className={songCell}>
        {song.entries.length === 0 ? (
          <span className={songMeta}>Nobody played it yet.</span>
        ) : (
          song.entries.slice(0, ENTRIES_PER_SONG).map((entry, index) => (
            <span key={`${entry.nickname}:${index}`} className={entryRow}>
              <span className={index === 0 ? rankBadgeFirst : rankBadge}>{index + 1}</span>
              <span className={entryName}>{entry.nickname}</span>
              <span className={entryScore}>{formatScore(entry.score)}</span>
            </span>
          ))
        )}
      </div>
    </div>
  );
}
