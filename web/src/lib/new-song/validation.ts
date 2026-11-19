import { parseYouTubeId } from "../chart/youtube";
import {
  MAX_ARTIST_LENGTH,
  MAX_DURATION_SEC,
  MAX_LEVEL,
  MAX_TITLE_LENGTH,
  MIN_LEVEL,
  SONG_ID_PATTERN,
} from "../publish/validation";

/**
 * Field rules of the new-song flow, in Japanese.
 *
 * The rules mirror `contracts/openapi.v1.yaml` (`SongPublishRequest`) and
 * `src/lib/publish/validation.ts`, which the publish request builder reuses.
 * The studio copy was unified to Japanese in issue #611, so the messages are
 * plain strings — there is no English variant left to keep in sync.
 */

export type NewSongField = "youtube" | "title" | "artist" | "songId" | "level" | "durationSec";

export interface NewSongFieldIssue {
  field: NewSongField;
  message: string;
}

/** The id is already published — the field must change. */
export const SONG_ID_TAKEN = "このIDは使用済みです。別のIDを選んでください。";

export function validateYoutubeField(value: string): string | undefined {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return "YouTubeのURLを貼り付けてください。";
  }
  if (parseYouTubeId(trimmed) === null) {
    return "YouTubeのURL、または11文字の動画IDを入力してください。";
  }
  return undefined;
}

export function validateTitleField(value: string): string | undefined {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return "曲名を入力してください。";
  }
  if (trimmed.length > MAX_TITLE_LENGTH) {
    return `曲名は${MAX_TITLE_LENGTH}文字以内にしてください。`;
  }
  return undefined;
}

export function validateArtistField(value: string): string | undefined {
  if (value.trim().length > MAX_ARTIST_LENGTH) {
    return `アーティスト名は${MAX_ARTIST_LENGTH}文字以内にしてください。`;
  }
  return undefined;
}

export function validateSongIdField(value: string): string | undefined {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return "曲IDを入力してください。";
  }
  if (!SONG_ID_PATTERN.test(trimmed)) {
    return "英小文字・数字・ハイフンで3〜64文字にしてください。";
  }
  return undefined;
}

export function validateLevelField(value: number): string | undefined {
  if (!Number.isInteger(value)) {
    return "レベルは整数で入力してください。";
  }
  if (value < MIN_LEVEL || value > MAX_LEVEL) {
    return `レベルは${MIN_LEVEL}〜${MAX_LEVEL}にしてください。`;
  }
  return undefined;
}

export function validateDurationField(value: number): string | undefined {
  if (!Number.isFinite(value)) {
    return "再生時間（秒）を入力してください。";
  }
  if (value <= 0) {
    return "再生時間は0より長くしてください。";
  }
  if (value > MAX_DURATION_SEC) {
    return `再生時間は${MAX_DURATION_SEC}秒以内にしてください。`;
  }
  return undefined;
}

/** Every metadata problem of the step-1 form, all fields at once. */
export function validateNewSongInfo(info: {
  youtube: string;
  title: string;
  artist: string;
  songId: string;
  level: number;
  durationSec: number;
}): NewSongFieldIssue[] {
  const issues: NewSongFieldIssue[] = [];
  const push = (field: NewSongField, message: string | undefined) => {
    if (message !== undefined) {
      issues.push({ field, message });
    }
  };

  push("youtube", validateYoutubeField(info.youtube));
  push("title", validateTitleField(info.title));
  push("artist", validateArtistField(info.artist));
  push("songId", validateSongIdField(info.songId));
  push("level", validateLevelField(info.level));
  push("durationSec", validateDurationField(info.durationSec));

  return issues;
}

export interface NoteCountVerdict {
  level: "error" | "warn";
  message: string;
}

/**
 * Minimum-notes rule of the chart step.
 *
 * A publish with zero notes is blocked (the contract allows it, the game
 * would show an empty song); fewer than 20 notes only warns.
 */
export function judgeNoteCount(count: number): NoteCountVerdict | null {
  if (count <= 0) {
    return {
      level: "error",
      message: "ノートを1つ以上置いてください。",
    };
  }
  if (count < 20) {
    return {
      level: "warn",
      message: `ノートが${count}個です。20未満は間延びしやすいので確認してください。`,
    };
  }
  return null;
}
