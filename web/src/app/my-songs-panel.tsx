import { useCallback, useEffect, useState } from "react";
import { Link } from "@void/react";
import { auth } from "void/client/react";
import { css } from "styled-system/css";
import { Archive, CloudUpload, PencilRuler, Plus, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui";
import {
  createDraft,
  fetchDraft,
  fetchMySongs,
  MeApiError,
  publishDraft as publishDraftApi,
  saveDraft,
  unpublishSong,
  type MeDraft,
  type MeDraftDetail,
  type MeSong,
  type PublishDraftBody,
} from "@/lib/me/client";
import { PublishDraftDialog } from "@/components/my-songs/publish-draft-dialog";
import { useClientVisit } from "./client-visit";
import { isAbortError, useLatestRequest } from "./request-guard";
import {
  pageActions,
  pageHeader,
  pageHeaderIdentity,
  pageSubtitle,
  pageTitle,
  panelBodyFlush,
  panelFrame,
  panelHeader,
  panelSubtitle,
  panelTitle,
  primaryCta,
  StateBlock,
} from "./panel";
import { Badge, metaText, num } from "./list";

/**
 * マイソング (issue #800): the per-account production screen.
 *
 * One list of the caller's songs and drafts across every workspace they
 * belong to, with publish state, chart version and owner visible on each
 * row. From here the owner resumes editing (`/chart?serverDraft=`),
 * publishes or updates through the rights-attesting dialog, and takes a
 * song back out of the catalog (公開停止 = the same soft-delete flag the
 * staff archive uses).
 *
 * Everything is session-authenticated: no staff token appears anywhere.
 */

const page = css({
  display: "flex",
  flexDirection: "column",
  gap: "4",
  minHeight: "0",
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
  gridTemplateColumns: { base: "minmax(0, 1fr) auto", md: "minmax(0, 1fr) 7rem 5rem 8rem" },
  minHeight: "3.5rem",
  _last: { borderBottomWidth: "0" },
  _hover: { bg: "gray.a3" },
});

const cell = css({
  display: "flex",
  flexDirection: "column",
  minWidth: "0",
  py: "2",
});

const songTitle = css({
  color: "fg.default",
  textStyle: "rowTitle",
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "nowrap",
});

const actions = css({
  alignItems: "center",
  display: "flex",
  flexWrap: "wrap",
  gap: "2",
  justifyContent: "flex-end",
  minWidth: "0",
  py: "2",
});

const buttonIcon = css({
  color: "currentColor",
  marginRight: "1",
});

const errorNote = css({
  color: "red.11",
  fontSize: "xs",
});

const notice = css({
  color: "fg.muted",
  fontSize: "xs",
});

const emptyText = css({
  color: "fg.subtle",
  fontSize: "sm",
  padding: "4 0",
});

function draftEditHref(draft: MeDraft): string {
  return `/chart?serverDraft=${encodeURIComponent(draft.id)}`;
}

function draftStateBadge(draft: MeDraft): { label: string; tone: "neutral" | "muted" | "outline" } {
  if (draft.publishedVersion !== null) {
    return { label: `公開中 v${draft.publishedVersion}`, tone: "neutral" };
  }
  return { label: "未公開", tone: "muted" };
}

export function MySongsPanel() {
  const visit = useClientVisit();
  const session = auth.useSession();
  const user = session.data?.user ?? null;

  const [status, setStatus] = useState<"loading" | "ready" | "error">("loading");
  const [anonymous, setAnonymous] = useState(false);
  const [songs, setSongs] = useState<MeSong[]>([]);
  const [drafts, setDrafts] = useState<MeDraft[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | undefined>(undefined);
  const [noticeText, setNoticeText] = useState<string | undefined>(undefined);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  const [publishDraft, setPublishDraft] = useState<MeDraftDetail | null>(null);
  const [publishOpen, setPublishOpen] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [publishError, setPublishError] = useState("");

  const { begin, cancel } = useLatestRequest();

  const load = useCallback(async () => {
    const request = begin();
    setStatus("loading");
    setErrorMessage(undefined);
    try {
      const list = await fetchMySongs();
      if (!request.isCurrent()) return;
      setSongs(list.songs);
      setDrafts(list.drafts);
      setAnonymous(false);
      setStatus("ready");
    } catch (error: unknown) {
      if (isAbortError(error)) return;
      if (!request.isCurrent()) return;
      if (error instanceof MeApiError && error.status === 401) {
        setAnonymous(true);
        setStatus("ready");
        return;
      }
      setErrorMessage(error instanceof Error ? error.message : "不明なエラー");
      setStatus("error");
    }
  }, [begin]);

  useEffect(() => {
    if (session.isPending) return;
    void load();
    return () => cancel();
  }, [session.isPending, load, cancel]);

  async function onCreate() {
    setCreating(true);
    setErrorMessage(undefined);
    try {
      const { draft } = await createDraft({ title: "新しい曲" });
      visit(draftEditHref(draft));
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "下書きを作成できませんでした。");
      setCreating(false);
    }
  }

  async function onOpenPublish(draft: MeDraft) {
    setPublishError("");
    setPublishOpen(true);
    setPublishDraft(null);
    try {
      const { draft: detail } = await fetchDraft(draft.id);
      setPublishDraft(detail);
    } catch (error) {
      setPublishError(error instanceof Error ? error.message : "下書きを読み込めませんでした。");
      setPublishOpen(false);
    }
  }

  async function onPublish(body: PublishDraftBody) {
    if (!publishDraft) return;
    setPublishing(true);
    setPublishError("");
    try {
      // Keep the stored draft in sync with what is being published, so a
      // resumed edit starts from the published state.
      await saveDraft(publishDraft.id, {
        title: body.title,
        artist: body.artist,
        youtubeId: body.youtubeId,
        level: body.level,
        durationSec: body.durationSec,
        materialSource: body.materialSource,
        rightsStatus: body.rightsStatus,
        license: body.license ?? null,
      });
      const result = await publishDraftApi(publishDraft.id, body);
      setNoticeText(`「${body.title}」を公開しました（v${result.chartVersion}）。`);
      setPublishOpen(false);
      setPublishDraft(null);
      await load();
    } catch (error) {
      const diagnostics =
        error instanceof MeApiError && error.diagnostics.length > 0
          ? error.diagnostics.map((d) => `${d.path}: ${d.message}`).join(" / ")
          : "";
      setPublishError(
        `${error instanceof Error ? error.message : "公開できませんでした。"}${diagnostics ? `（${diagnostics}）` : ""}`,
      );
    } finally {
      setPublishing(false);
    }
  }

  async function onUnpublish(song: MeSong) {
    if (
      !window.confirm(
        `「${song.title}」を公開停止しますか？カタログから非表示になり、スコアは保持されます。`,
      )
    ) {
      return;
    }
    setBusyId(song.id);
    setErrorMessage(undefined);
    setNoticeText(undefined);
    try {
      await unpublishSong(song.id);
      setNoticeText(`「${song.title}」を公開停止しました。`);
      await load();
    } catch (error) {
      setErrorMessage(
        error instanceof MeApiError && error.status === 404
          ? "この曲は公開停止できません（権限がないか、すでに対象外です）。"
          : error instanceof Error
            ? error.message
            : "公開停止できませんでした。",
      );
    } finally {
      setBusyId(null);
    }
  }

  /** The draft that edits `song` (same public songId), if one exists. */
  function draftFor(song: MeSong): MeDraft | undefined {
    return drafts.find((d) => d.songId === song.id);
  }

  return (
    <>
      <header className={pageHeader} data-testid="my-songs-header">
        <div className={pageHeaderIdentity}>
          <h1 className={pageTitle}>マイソング</h1>
          <p className={pageSubtitle} data-testid="my-songs-subtitle">
            {user ? `${user.name} の曲と下書き` : "ログインすると自分の曲を管理できます"}
          </p>
        </div>
        <div className={pageActions}>
          {anonymous ? (
            <Link className={primaryCta} href="/login" data-testid="go-login">
              ログイン
            </Link>
          ) : (
            <>
              <Button
                variant="outline"
                size="sm"
                onClick={() => void load()}
                loading={status === "loading"}
              >
                <RefreshCw className={buttonIcon} size={14} aria-hidden="true" />
                更新
              </Button>
              <Button
                className={primaryCta}
                size="sm"
                onClick={() => void onCreate()}
                loading={creating}
                disabled={anonymous}
                data-testid="new-draft"
              >
                <Plus className={buttonIcon} size={14} aria-hidden="true" />
                新規作成
              </Button>
            </>
          )}
        </div>
      </header>

      <div className={page}>
        {errorMessage ? (
          <p className={errorNote} role="alert">
            {errorMessage}
          </p>
        ) : null}
        {noticeText ? <p className={notice}>{noticeText}</p> : null}

        {anonymous ? (
          <div className={panelFrame}>
            <StateBlock
              title="ログインが必要です"
              description="マイソングの閲覧・編集・公開にはアカウントでのログインが必要です。"
              action={
                <Link className={primaryCta} href="/login">
                  ログイン・新規登録
                </Link>
              }
            />
          </div>
        ) : status === "loading" ? (
          <div className={panelFrame}>
            <StateBlock title="読み込み中" description="自分の曲と下書きを読み込んでいます。" />
          </div>
        ) : status === "error" ? (
          <div className={panelFrame}>
            <StateBlock
              title="読み込めませんでした"
              description={errorMessage ?? "時間をおいて再試行してください。"}
              action={
                <Button variant="outline" size="sm" onClick={() => void load()}>
                  再試行
                </Button>
              }
            />
          </div>
        ) : (
          <>
            <div className={panelFrame} data-testid="drafts-panel">
              <div className={panelHeader}>
                <div>
                  <h2 className={panelTitle}>下書き</h2>
                  <p className={panelSubtitle}>編集途中の曲。公開するまでカタログには出ません。</p>
                </div>
              </div>
              <div className={panelBodyFlush}>
                <div className={rows}>
                  {drafts.length === 0 ? (
                    <p className={emptyText}>下書きはありません。「新規作成」から始められます。</p>
                  ) : (
                    drafts.map((draft) => {
                      const badge = draftStateBadge(draft);
                      return (
                        <div key={draft.id} className={row} data-testid={`draft-row-${draft.id}`}>
                          <div className={cell}>
                            <span className={songTitle}>
                              {draft.title || draft.songId || "無題"}
                            </span>
                            <span className={metaText}>
                              {draft.artist || "—"} · {draft.songId ?? "ID未設定"} ·{" "}
                              {draft.noteCount}ノート · {draft.ownerWorkspaceName}
                            </span>
                          </div>
                          <Badge tone={badge.tone}>{badge.label}</Badge>
                          <span className={num}>
                            {draft.publishedVersion ? `v${draft.publishedVersion}` : "—"}
                          </span>
                          <div className={actions}>
                            {draft.canEdit ? (
                              <>
                                <Button
                                  variant="outline"
                                  size="sm"
                                  onClick={() => visit(draftEditHref(draft))}
                                  data-testid={`edit-${draft.id}`}
                                >
                                  <PencilRuler
                                    className={buttonIcon}
                                    size={14}
                                    aria-hidden="true"
                                  />
                                  編集
                                </Button>
                                <Button
                                  size="sm"
                                  onClick={() => void onOpenPublish(draft)}
                                  data-testid={`publish-${draft.id}`}
                                >
                                  <CloudUpload
                                    className={buttonIcon}
                                    size={14}
                                    aria-hidden="true"
                                  />
                                  公開
                                </Button>
                              </>
                            ) : (
                              <Badge tone="outline">閲覧のみ</Badge>
                            )}
                          </div>
                        </div>
                      );
                    })
                  )}
                </div>
              </div>
            </div>

            <div className={panelFrame} data-testid="songs-panel">
              <div className={panelHeader}>
                <div>
                  <h2 className={panelTitle}>公開した曲</h2>
                  <p className={panelSubtitle}>
                    カタログに出ている自分の曲。公開停止はいつでも戻せます。
                  </p>
                </div>
              </div>
              <div className={panelBodyFlush}>
                <div className={rows}>
                  {songs.length === 0 ? (
                    <p className={emptyText}>公開した曲はまだありません。</p>
                  ) : (
                    songs.map((song) => {
                      const badge =
                        song.state === "published"
                          ? { label: "公開中", tone: "neutral" as const }
                          : { label: "公開停止", tone: "muted" as const };
                      const editDraft = draftFor(song);
                      return (
                        <div key={song.id} className={row} data-testid={`song-row-${song.id}`}>
                          <div className={cell}>
                            <span className={songTitle}>{song.title}</span>
                            <span className={metaText}>
                              {song.artist || "—"} · Lv.{song.level} · {song.id} ·{" "}
                              {song.ownerWorkspaceName}
                              {song.rightsStatus ? ` · 権利:${song.rightsStatus}` : ""}
                            </span>
                          </div>
                          <Badge tone={badge.tone}>{badge.label}</Badge>
                          <span className={num}>v{song.chartVersion}</span>
                          <div className={actions}>
                            {song.canEdit && editDraft ? (
                              <Button
                                variant="outline"
                                size="sm"
                                onClick={() => visit(draftEditHref(editDraft))}
                              >
                                <PencilRuler className={buttonIcon} size={14} aria-hidden="true" />
                                編集
                              </Button>
                            ) : null}
                            {song.canEdit && song.state === "published" ? (
                              <Button
                                variant="outline"
                                size="sm"
                                loading={busyId === song.id}
                                onClick={() => void onUnpublish(song)}
                                data-testid={`unpublish-${song.id}`}
                              >
                                <Archive className={buttonIcon} size={14} aria-hidden="true" />
                                公開停止
                              </Button>
                            ) : null}
                          </div>
                        </div>
                      );
                    })
                  )}
                </div>
              </div>
            </div>
          </>
        )}
      </div>

      <PublishDraftDialog
        open={publishOpen}
        onOpenChange={(open) => {
          setPublishOpen(open);
          if (!open) setPublishDraft(null);
        }}
        draft={publishDraft}
        publishing={publishing}
        error={publishError}
        onPublish={(body) => void onPublish(body)}
      />
    </>
  );
}
