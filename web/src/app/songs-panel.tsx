import { useCallback, useState } from "react";
import { css } from "styled-system/css";
import { Button, Field, Input } from "@/components/ui";
import { Archive, ArchiveRestore, Info, RefreshCw } from "lucide-react";
import { loadAdminSongs, setSongArchived } from "@/lib/dashboard/client";
import type { AdminSong } from "@/lib/dashboard/types";
import { StateBlock, panelBody, panelFrame, panelHeader, panelSubtitle, panelTitle } from "./panel";

/**
 * Song management of the studio (issue #151).
 *
 * The festival never deletes a song. The only operation here is *archiving*:
 * an archived song leaves the public catalog (the Unity select screen does
 * not show it any more) but keeps its chart snapshots, its scores and its
 * ranking. "Restore" brings it back with its history.
 *
 * The staff token is typed into the form for every session; it is never
 * stored in the browser and never sent anywhere but the archive route.
 */
export type SongsStatus = "idle" | "loading" | "ready" | "error";

const page = css({
  display: "flex",
  flexDirection: "column",
  gap: "4",
  minHeight: "0",
});

/** The info callout above the song list. */
const notice = css({
  alignItems: "flex-start",
  bg: "blue.a2",
  borderColor: "blue.a6",
  borderRadius: "l2",
  borderWidth: "1px",
  color: "fg.muted",
  columnGap: "2",
  display: "flex",
  fontSize: "sm",
  p: "3",
});

const noticeIcon = css({
  color: "blue.11",
  flexShrink: 0,
  marginTop: "1",
});

const tokenRow = css({
  alignItems: "flex-end",
  display: "flex",
  flexWrap: "wrap",
  gap: "3",
});

const tokenField = css({
  width: "22rem",
});

const message = css({
  color: "fg.muted",
  fontSize: "xs",
});

const errorMessage = css({
  color: "red.11",
  fontSize: "xs",
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
  gridTemplateColumns: { base: "minmax(0, 1fr) auto", md: "minmax(0, 1fr) 4rem 5rem 6rem 8rem" },
  minHeight: "3rem",
  _last: { borderBottomWidth: "0" },
});

const songCell = css({
  display: "flex",
  flexDirection: "column",
  minWidth: "0",
  py: "2",
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
  display: { base: "none", md: "block" },
  fontVariantNumeric: "tabular-nums",
  fontSize: "sm",
  textAlign: "right",
  whiteSpace: "nowrap",
});

const actions = css({
  alignItems: "center",
  display: "flex",
  gap: "2",
  justifyContent: "flex-end",
  py: "2",
});

/** The icon inside a button. */
const buttonIcon = css({
  color: "currentColor",
  marginRight: "1",
});

const archivedTag = css({
  bg: "gray.8",
  borderRadius: "l1",
  color: "fg.muted",
  fontSize: "xs",
  paddingX: "2",
  paddingY: "0.5",
});

export function SongsPanel() {
  const [token, setToken] = useState("");
  const [status, setStatus] = useState<SongsStatus>("idle");
  const [errorMessage, setErrorMessage] = useState<string | undefined>(undefined);
  const [noticeText, setNoticeText] = useState<string | undefined>(undefined);
  const [songs, setSongs] = useState<AdminSong[]>([]);
  const [busySongId, setBusySongId] = useState<string | null>(null);

  const load = useCallback(async (staffToken: string) => {
    setStatus("loading");
    setErrorMessage(undefined);
    try {
      const answer = await loadAdminSongs(staffToken);
      setSongs(answer.songs);
      setStatus("ready");
    } catch (error: unknown) {
      setErrorMessage(error instanceof Error ? error.message : "Unknown error");
      setStatus("error");
    }
  }, []);

  const archive = useCallback(
    async (song: AdminSong, archived: boolean) => {
      setBusySongId(song.id);
      setErrorMessage(undefined);
      setNoticeText(undefined);
      try {
        await setSongArchived(token, song.id, archived);
        setNoticeText(
          archived ? `${song.title} archived. Its scores are kept.` : `${song.title} restored.`,
        );
        await load(token);
      } catch (error: unknown) {
        setErrorMessage(error instanceof Error ? error.message : "Unknown error");
      } finally {
        setBusySongId(null);
      }
    },
    [load, token],
  );

  return (
    <div className={page}>
      <p className={notice}>
        <Info className={noticeIcon} size={16} aria-hidden="true" />
        <span>
          Archiving hides a song from the catalog and from the game. Its chart, scores and ranking
          stay. Restore puts it back.
        </span>
      </p>

      <div className={panelFrame}>
        <div className={panelHeader}>
          <div>
            <h2 className={panelTitle}>Song list</h2>
            <p className={panelSubtitle}>Published and archived songs.</p>
          </div>
        </div>
        <div className={panelBody}>
          <div className={tokenRow}>
            <Field.Root className={tokenField}>
              <Field.Label>Staff token</Field.Label>
              <Input
                type="password"
                value={token}
                placeholder="The token of the event operator"
                onChange={(event: React.ChangeEvent<HTMLInputElement>) =>
                  setToken(event.target.value)
                }
              />
            </Field.Root>
            <Button
              onClick={() => {
                void load(token);
              }}
              loading={status === "loading"}
              disabled={token.trim().length === 0}
            >
              <RefreshCw className={buttonIcon} size={14} aria-hidden="true" />
              List the songs
            </Button>
          </div>

          {errorMessage === undefined ? null : (
            <p className={errorMessage} role="alert">
              {errorMessage}
            </p>
          )}
          {noticeText === undefined ? null : <p className={message}>{noticeText}</p>}

          {status === "ready" ? (
            <div className={rows}>
              {songs.map((song) => (
                <div key={song.id} className={row}>
                  <div className={songCell}>
                    <span className={songTitle}>{song.title}</span>
                    <span className={songMeta}>
                      {song.artist} · Lv.{song.level} · {song.id}
                      {song.archived && song.archivedAt !== null
                        ? ` · archived ${new Date(song.archivedAt).toLocaleString("ja-JP")}`
                        : ""}
                    </span>
                  </div>
                  <span className={num}>{song.plays}</span>
                  <span className={num}>
                    {song.bestScore === null ? "—" : song.bestScore.toLocaleString("en-US")}
                  </span>
                  <span className={num}>v{song.chartVersion}</span>
                  <div className={actions}>
                    {song.archived ? (
                      <>
                        <span className={archivedTag}>archived</span>
                        <Button
                          size="sm"
                          variant="outline"
                          loading={busySongId === song.id}
                          onClick={() => {
                            void archive(song, false);
                          }}
                        >
                          <ArchiveRestore className={buttonIcon} size={14} aria-hidden="true" />
                          Restore
                        </Button>
                      </>
                    ) : (
                      <Button
                        size="sm"
                        variant="outline"
                        loading={busySongId === song.id}
                        onClick={() => {
                          void archive(song, true);
                        }}
                      >
                        <Archive className={buttonIcon} size={14} aria-hidden="true" />
                        Archive
                      </Button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          ) : status === "error" ? null : status === "loading" ? (
            <StateBlock title="Loading the songs" description="Reading the database." />
          ) : (
            <StateBlock
              title="No songs loaded"
              description="Enter the staff token to list the songs."
            />
          )}
        </div>
      </div>
    </div>
  );
}
