import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Button, Field, Input } from "@/components/ui";
import { DurationProbe } from "./duration-probe";
import { css } from "styled-system/css";
import { parseYouTubeId } from "@/lib/chart/youtube";
import type { NewSongInfo } from "@/lib/new-song/draft";
import { fetchOembedMetadata, type OembedFailureReason } from "@/lib/new-song/oembed";
import { suggestSongSlug } from "@/lib/new-song/slug";
import {
  SONG_ID_TAKEN,
  validateArtistField,
  validateDurationField,
  validateLevelField,
  validateNewSongInfo,
  validateSongIdField,
  validateTitleField,
  validateYoutubeField,
  type NewSongField,
} from "@/lib/new-song/validation";

/**
 * Step 1 of the new-song flow: 曲情報.
 *
 * The operator pastes a YouTube URL; the video id is validated, the title and
 * the artist come from YouTube oEmbed (manual fields stay authoritative) and
 * the song id is suggested as an ascii kebab-case slug that is checked
 * against the live catalog. Every error is shown in Japanese and English.
 */

const form = css({
  display: "grid",
  gap: "4",
});

const row = css({
  display: "grid",
  gap: "3",
  gridTemplateColumns: { base: "1fr", md: "1fr 1fr" },
});

const suggestions = css({
  display: "flex",
  flexWrap: "wrap",
  gap: "2",
});

const suggestionButton = css({
  bg: "#3a3a3a",
  borderRadius: "8px",
  color: "#ffffff",
  fontSize: "2xs",
  paddingInline: "2",
  _hover: { bg: "#454545" },
});

const note = css({
  fontSize: "2xs",
  margin: "0",
});

const noteMuted = css({
  color: "#8c8c8c",
});

const noteError = css({
  color: "#ff6b6b",
});

const noteOk = css({
  color: "#9ad3fa",
});

const footer = css({
  alignItems: "center",
  display: "flex",
  gap: "3",
  justifyContent: "space-between",
});

const catalogNote = css({
  color: "#8c8c8c",
  fontSize: "2xs",
  margin: "0",
});

const accent = css({ colorPalette: "blue" });

/** v5 Button Secondary: raised #3A3A3A with white label. */
const raisedButton = css({
  bg: "#3a3a3a",
  color: "#ffffff",
  borderRadius: "8px",
  borderColor: "transparent",
  _hover: { bg: "#454545" },
  _disabled: { opacity: 0.4 },
});

const NO_TOUCHED: Record<NewSongField, boolean> = {
  youtube: false,
  title: false,
  artist: false,
  songId: false,
  level: false,
  durationSec: false,
};

const OEMBED_FAILURE_TEXT: Record<OembedFailureReason, { ja: string; en: string }> = {
  "invalid-id": {
    ja: "動画IDを確認してください。",
    en: "Check the video id.",
  },
  offline: {
    ja: "オフラインのため曲名を自動取得できません。手動で入力してください。",
    en: "Offline — the title could not be fetched. Enter it manually.",
  },
  "not-found": {
    ja: "動画が見つかりません（非公開または削除済み）。",
    en: "The video was not found (private or deleted).",
  },
  malformed: {
    ja: "動画情報を取得できませんでした。手動で入力してください。",
    en: "The video metadata could not be read. Enter it manually.",
  },
};

export interface SongInfoStepProps {
  info: NewSongInfo;
  /** Published song ids of `GET /api/v1/catalog`; `null` when unavailable. */
  takenIds: ReadonlySet<string> | null;
  catalogError: string | null;
  onChange: (patch: Partial<NewSongInfo>) => void;
  onAdvance: () => void;
}

export function SongInfoStep({
  info,
  takenIds,
  catalogError,
  onChange,
  onAdvance,
}: SongInfoStepProps) {
  const [touched, setTouched] = useState(NO_TOUCHED);
  const [attempted, setAttempted] = useState(false);
  // Issue #215: coming back from the chart step remounts this step. A song id
  // already in the draft (typed by the user or auto-suggested earlier) must
  // not be overwritten by the title suggestion again, so a non-empty id
  // starts as "touched".
  const [songIdTouched, setSongIdTouched] = useState(() => info.songId.trim().length > 0);
  const [oembed, setOembed] = useState<{
    videoId: string;
    state: "loading" | "done" | "failed";
    reason?: OembedFailureReason;
  } | null>(null);
  const [probeDuration, setProbeDuration] = useState<number | null>(null);

  const videoId = useMemo(() => parseYouTubeId(info.youtube) ?? "", [info.youtube]);

  const issues = useMemo(() => validateNewSongInfo(info), [info]);
  const issueByField = useMemo(() => {
    const map = new Map<NewSongField, string>();
    for (const issue of issues) {
      map.set(issue.field, `${issue.message.ja} / ${issue.message.en}`);
    }
    return map;
  }, [issues]);

  const songIdTrimmed = info.songId.trim();
  const songIdTaken =
    takenIds !== null &&
    songIdTrimmed.length > 0 &&
    validateSongIdField(songIdTrimmed) === undefined &&
    takenIds.has(songIdTrimmed);

  const visibleError = useCallback(
    (field: NewSongField): string | undefined => {
      if (songIdTaken && field === "songId") {
        return `${SONG_ID_TAKEN.ja} / ${SONG_ID_TAKEN.en}`;
      }
      return touched[field] || attempted ? issueByField.get(field) : undefined;
    },
    [attempted, issueByField, songIdTaken, touched],
  );

  const handleChange = useCallback(
    (patch: Partial<NewSongInfo>) => {
      if (patch.songId !== undefined) {
        setSongIdTouched(true);
      }
      onChange(patch);
    },
    [onChange],
  );

  const handleBlur = useCallback((field: NewSongField) => {
    setTouched((current) => ({ ...current, [field]: true }));
  }, []);

  // oEmbed: fetch title/artist when a new video id appears. Manual entries
  // are never overwritten — only empty fields are filled.
  const infoRef = useRef(info);
  infoRef.current = info;
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;

  useEffect(() => {
    if (videoId.length === 0) {
      setOembed(null);
      return;
    }
    let active = true;
    setOembed({ videoId, state: "loading" });
    fetchOembedMetadata(videoId).then((result) => {
      if (!active) {
        return;
      }
      if (result.ok) {
        setOembed({ videoId, state: "done" });
        const current = infoRef.current;
        const patch: Partial<NewSongInfo> = {};
        if (current.title.trim().length === 0 && result.title.length > 0) {
          patch.title = result.title;
        }
        if (current.artist.trim().length === 0 && result.author.length > 0) {
          patch.artist = result.author;
        }
        if (Object.keys(patch).length > 0) {
          onChangeRef.current(patch);
        }
      } else {
        setOembed({ videoId, state: "failed", reason: result.reason });
      }
    });
    return () => {
      active = false;
    };
  }, [videoId]);

  // The song id follows the title until the operator writes one.
  useEffect(() => {
    if (songIdTouched) {
      return;
    }
    const suggestion = suggestSongSlug(infoRef.current.title, videoId, takenIds ?? new Set());
    if (suggestion.suggestion !== "" && suggestion.suggestion !== infoRef.current.songId) {
      onChangeRef.current({ songId: suggestion.suggestion });
    }
  }, [info.title, songIdTouched, takenIds, videoId]);

  const handleProbeDuration = useCallback((seconds: number) => {
    setProbeDuration(seconds);
    if (!Number.isFinite(infoRef.current.durationSec)) {
      onChangeRef.current({ durationSec: Math.round(seconds) });
    }
  }, []);

  const handleAdvance = useCallback(() => {
    setAttempted(true);
    if (issues.length === 0 && !songIdTaken) {
      onAdvance();
    }
  }, [issues.length, onAdvance, songIdTaken]);

  const alternatives = useMemo(
    () =>
      songIdTaken && takenIds ? suggestSongSlug(info.title, videoId, takenIds).alternatives : [],
    [info.title, songIdTaken, takenIds, videoId],
  );

  const durationValue = Number.isFinite(info.durationSec) ? String(info.durationSec) : "";
  const levelValue = Number.isFinite(info.level) ? String(info.level) : "";

  return (
    <div className={form}>
      <Field.Root invalid={visibleError("youtube") !== undefined}>
        <Field.Label>YouTubeのURL / YouTube URL</Field.Label>
        <Input
          value={info.youtube}
          placeholder="https://www.youtube.com/watch?v=dQw4w9WgXcQ"
          onChange={(event) => handleChange({ youtube: event.currentTarget.value })}
          onBlur={() => handleBlur("youtube")}
        />
        {visibleError("youtube") ? (
          <Field.ErrorText>{visibleError("youtube")}</Field.ErrorText>
        ) : (
          <Field.HelperText>
            youtu.be / watch?v= / shorts どれでもOK。 / Any YouTube URL form works.
          </Field.HelperText>
        )}
      </Field.Root>

      {videoId.length > 0 ? (
        <p className={`${note} ${noteOk}`} data-testid="video-id-readout">
          動画ID: {videoId} / Video id: {videoId}
        </p>
      ) : null}

      {oembed?.state === "loading" ? (
        <p className={`${note} ${noteMuted}`} role="status">
          曲名を取得中… / Fetching the title…
        </p>
      ) : null}
      {oembed?.state === "failed" && oembed.reason ? (
        <p className={`${note} ${noteError}`} role="alert">
          {OEMBED_FAILURE_TEXT[oembed.reason].ja} / {OEMBED_FAILURE_TEXT[oembed.reason].en}
        </p>
      ) : null}

      <Field.Root invalid={visibleError("title") !== undefined}>
        <Field.Label>曲名 / Title</Field.Label>
        <Input
          value={info.title}
          placeholder="曲名 / Song title"
          onChange={(event) => handleChange({ title: event.currentTarget.value })}
          onBlur={() => handleBlur("title")}
        />
        {visibleError("title") ? (
          <Field.ErrorText>{visibleError("title")}</Field.ErrorText>
        ) : (
          <Field.HelperText>
            YouTubeから自動取得できます。 / Filled from YouTube when possible.
          </Field.HelperText>
        )}
      </Field.Root>

      <Field.Root invalid={visibleError("artist") !== undefined}>
        <Field.Label>アーティスト / Artist</Field.Label>
        <Input
          value={info.artist}
          placeholder="アーティスト名 / Artist"
          onChange={(event) => handleChange({ artist: event.currentTarget.value })}
          onBlur={() => handleBlur("artist")}
        />
        {visibleError("artist") ? (
          <Field.ErrorText>{visibleError("artist")}</Field.ErrorText>
        ) : null}
      </Field.Root>

      <Field.Root invalid={visibleError("songId") !== undefined}>
        <Field.Label>曲ID / Song id</Field.Label>
        <Input
          value={info.songId}
          placeholder="my-song"
          onChange={(event) => handleChange({ songId: event.currentTarget.value })}
          onBlur={() => handleBlur("songId")}
        />
        {visibleError("songId") ? (
          <Field.ErrorText>{visibleError("songId")}</Field.ErrorText>
        ) : (
          <Field.HelperText>
            英小文字・数字・ハイフン。ゲーム内の曲のIDになります。 / Lowercase letters, digits,
            dashes. This is the id in the game.
          </Field.HelperText>
        )}
        {songIdTaken && alternatives.length > 0 ? (
          <div className={suggestions} data-testid="song-id-suggestions">
            {alternatives.map((candidate) => (
              <button
                key={candidate}
                type="button"
                className={suggestionButton}
                onClick={() => handleChange({ songId: candidate })}
              >
                {candidate}
              </button>
            ))}
          </div>
        ) : null}
      </Field.Root>

      <div className={row}>
        <Field.Root invalid={visibleError("level") !== undefined}>
          <Field.Label>レベル / Level（1–99）</Field.Label>
          <Input
            type="number"
            min={1}
            max={99}
            value={levelValue}
            placeholder="10"
            onChange={(event) => {
              const parsed = Number(event.currentTarget.value);
              handleChange({
                level: event.currentTarget.value.trim() === "" ? Number.NaN : parsed,
              });
            }}
            onBlur={() => handleBlur("level")}
          />
          {visibleError("level") ? (
            <Field.ErrorText>{visibleError("level")}</Field.ErrorText>
          ) : null}
        </Field.Root>

        <Field.Root invalid={visibleError("durationSec") !== undefined}>
          <Field.Label>再生時間（秒） / Duration (seconds)</Field.Label>
          <Input
            type="number"
            min={1}
            max={1800}
            value={durationValue}
            placeholder="214"
            onChange={(event) => {
              const parsed = Number(event.currentTarget.value);
              handleChange({
                durationSec: event.currentTarget.value.trim() === "" ? Number.NaN : parsed,
              });
            }}
            onBlur={() => handleBlur("durationSec")}
          />
          {visibleError("durationSec") ? (
            <Field.ErrorText>{visibleError("durationSec")}</Field.ErrorText>
          ) : null}
        </Field.Root>
      </div>

      <p className={`${note} ${noteMuted}`}>
        再生時間はoEmbedに含まれないため、埋め込みプレイヤーまたは手動入力で指定します。 / The
        duration is not in oEmbed: take it from the embedded player or type it.
      </p>

      {videoId.length > 0 ? (
        <div className={css({ display: "grid", gap: "2", justifyItems: "start" })}>
          <DurationProbe videoId={videoId} onDuration={handleProbeDuration} />
          <Button
            className={raisedButton}
            size="sm"
            disabled={probeDuration === null}
            onClick={() => {
              if (probeDuration !== null) {
                handleChange({ durationSec: Math.round(probeDuration) });
              }
            }}
          >
            プレイヤーから自動取得 / Auto from player
          </Button>
        </div>
      ) : null}

      {catalogError !== null ? (
        <p className={`${note} ${noteError}`} role="alert">
          カタログを取得できないためIDの重複確認ができません（{catalogError}）。 / The catalog is
          unavailable ({catalogError}); the ID uniqueness check is skipped.
        </p>
      ) : null}

      <div className={footer}>
        <span className={noteMuted} data-testid="song-info-progress-note">
          入力が揃ったら「次へ」で譜面に進みます。入力は保存され、戻っても消えません。
        </span>
        <Button className={accent} size="md" onClick={handleAdvance}>
          次へ：譜面 / Next: Chart
        </Button>
      </div>
    </div>
  );
}
