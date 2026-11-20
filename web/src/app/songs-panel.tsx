import { useCallback, useEffect, useState } from "react";
import { css } from "styled-system/css";
import { Button, Field, Input } from "@/components/ui";
import { Archive, ArchiveRestore, Info, RefreshCw } from "lucide-react";
import { loadAdminSongs, setSongArchived } from "@/lib/dashboard/client";
import type { AdminSong } from "@/lib/dashboard/types";
import { StateBlock, panelBody, panelFrame, panelHeader, panelSubtitle, panelTitle } from "./panel";
import { Badge, metaText, num } from "./list";
import { isAbortError, useLatestRequest } from "./request-guard";

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

const errorNote = css({
  color: "red.11",
  fontSize: "xs",
});

const rows = css({
  display: "flex",
  flexDirection: "column",
});

/** Column header of the song table — same tracks as `row` (issue #802). */
const header = css({
  borderBottomColor: "border",
  borderBottomWidth: "1px",
  color: "fg.subtle",
  columnGap: "4",
  display: { base: "none", md: "grid" },
  fontSize: "xs",
  gridTemplateColumns: { md: "minmax(0, 1fr) 4rem 5rem 6rem 8rem" },
  paddingBottom: "2",
  paddingTop: "1",
});

const headerNum = css({
  textAlign: "right",
});

const row = css({
  alignItems: "center",
  borderBottomColor: "border",
  borderBottomWidth: "1px",
  columnGap: "4",
  display: "grid",
  /** Issue #802: title + plays + best score + version + actions. */
  gridTemplateColumns: { base: "minmax(0, 1fr) auto", md: "minmax(0, 1fr) 4rem 5rem 6rem 8rem" },
  minHeight: "3.5rem",
  _last: { borderBottomWidth: "0" },
  _hover: { bg: "gray.a3" },
});

const songCell = css({
  display: "flex",
  flexDirection: "column",
  minWidth: "0",
  py: "2",
});

const songTitle = css({
  color: "fg.default",
  /** Issue #802: row titles use the shared `rowTitle` step (16px medium). */
  textStyle: "rowTitle",
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "nowrap",
});

/** Play count folded into the meta line where the numeric column hides. */
const playsInline = css({
  display: { base: "inline", md: "none" },
});

/**
 * Action cluster of a management row. Unlike the catalog's `rowActions`
 * this row has no stretched link overlay, so the cluster keeps normal
 * pointer events for the Park UI buttons.
 */
const actions = css({
  alignItems: "center",
  display: "flex",
  gap: "2",
  justifyContent: "flex-end",
  minWidth: "0",
  py: "2",
});

/** The icon inside a button. */
const buttonIcon = css({
  color: "currentColor",
  marginRight: "1",
});

export function SongsPanel() {
  const [token, setToken] = useState("");
  const [status, setStatus] = useState<SongsStatus>("idle");
  const [errorMessage, setErrorMessage] = useState<string | undefined>(undefined);
  const [noticeText, setNoticeText] = useState<string | undefined>(undefined);
  const [songs, setSongs] = useState<AdminSong[]>([]);
  const [busySongId, setBusySongId] = useState<string | null>(null);

  // Issue #659: 連打で複数の load が並走すると古い応答が後勝ちしうる。
  // 最新の要求だけが state を更新する。issue #741 で他画面と同じ
  // `useLatestRequest` ガードへ揃え、未完了の要求はアンマウント時にも abort する。
  const { begin, cancel } = useLatestRequest();
  const load = useCallback(
    async (staffToken: string) => {
      const request = begin();
      setStatus("loading");
      setErrorMessage(undefined);
      try {
        const answer = await loadAdminSongs(staffToken, { signal: request.signal });
        if (!request.isCurrent()) return;
        setSongs(answer.songs);
        setStatus("ready");
      } catch (error: unknown) {
        // 中断は失敗ではない。次に始まる読み込みの結果を待つだけ。
        if (isAbortError(error)) return;
        if (!request.isCurrent()) return;
        setErrorMessage(error instanceof Error ? error.message : "不明なエラー");
        setStatus("error");
      }
    },
    [begin],
  );

  useEffect(() => cancel, [cancel]);

  const archive = useCallback(
    async (song: AdminSong, archived: boolean) => {
      setBusySongId(song.id);
      setErrorMessage(undefined);
      setNoticeText(undefined);
      try {
        await setSongArchived(token, song.id, archived);
        setNoticeText(
          archived
            ? `${song.title} をアーカイブしました。スコアは保持されています。`
            : `${song.title} を復元しました。`,
        );
        await load(token);
      } catch (error: unknown) {
        setErrorMessage(error instanceof Error ? error.message : "不明なエラー");
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
          曲をアーカイブするとカタログとゲームから非表示になります。譜面、スコア、ランキングは保持されます。復元すると元に戻ります。
        </span>
      </p>

      <div className={panelFrame}>
        <div className={panelHeader}>
          <div>
            <h2 className={panelTitle}>曲一覧</h2>
            <p className={panelSubtitle}>公開中・アーカイブ済みの曲。</p>
          </div>
        </div>
        <div className={panelBody}>
          <div className={tokenRow}>
            <Field.Root className={tokenField}>
              <Field.Label>スタッフトークン</Field.Label>
              <Input
                type="password"
                value={token}
                placeholder="イベント運営のトークン"
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
              曲を一覧する
            </Button>
          </div>

          {errorMessage === undefined ? null : (
            <p className={errorNote} role="alert">
              {errorMessage}
            </p>
          )}
          {noticeText === undefined ? null : <p className={message}>{noticeText}</p>}

          {status === "ready" ? (
            <div className={rows}>
              {/* Issue #802: the columns were unlabeled — name them once. */}
              <div className={header} aria-hidden="true">
                <span>曲</span>
                <span className={headerNum}>プレイ</span>
                <span className={headerNum}>最高スコア</span>
                <span className={headerNum}>Ver</span>
                <span />
              </div>
              {songs.map((song) => (
                <div key={song.id} className={row}>
                  <div className={songCell}>
                    <span className={songTitle}>{song.title}</span>
                    <span className={metaText}>
                      {song.artist} · Lv.{song.level} · {song.id}
                      {song.archived && song.archivedAt !== null
                        ? ` · アーカイブ ${new Date(song.archivedAt).toLocaleString("ja-JP")}`
                        : ""}
                      <span className={playsInline}>{` · プレイ ${song.plays}`}</span>
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
                        <Badge tone="muted">アーカイブ</Badge>
                        <Button
                          size="sm"
                          variant="outline"
                          loading={busySongId === song.id}
                          onClick={() => {
                            void archive(song, false);
                          }}
                        >
                          <ArchiveRestore className={buttonIcon} size={14} aria-hidden="true" />
                          復元
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
                        アーカイブ
                      </Button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          ) : status === "error" ? null : status === "loading" ? (
            <StateBlock title="曲を読み込み中" description="データベースを読み込んでいます。" />
          ) : (
            <StateBlock
              title="曲が読み込まれていません"
              description="スタッフトークンを入力して曲を一覧してください。"
            />
          )}
        </div>
      </div>
    </div>
  );
}
