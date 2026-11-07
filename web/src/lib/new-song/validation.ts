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
 * Field rules of the new-song flow, in Japanese and English.
 *
 * The rules mirror `contracts/openapi.v1.yaml` (`SongPublishRequest`) and
 * `src/lib/publish/validation.ts`, which the publish request builder reuses.
 * The flow shows both languages because the booth operator and the visitor
 * do not necessarily read the same one.
 */

export type NewSongField = "youtube" | "title" | "artist" | "songId" | "level" | "durationSec";

export interface Bilingual {
  ja: string;
  en: string;
}

export interface NewSongFieldIssue {
  field: NewSongField;
  message: Bilingual;
}

/** The id is already published — the field must change. */
export const SONG_ID_TAKEN: Bilingual = {
  ja: "このIDは使用済みです。別のIDを選んでください。",
  en: "This ID is already in use. Choose another one.",
};

export function validateYoutubeField(value: string): Bilingual | undefined {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return { ja: "YouTubeのURLを貼り付けてください。", en: "Paste the YouTube URL." };
  }
  if (parseYouTubeId(trimmed) === null) {
    return {
      ja: "YouTubeのURL、または11文字の動画IDを入力してください。",
      en: "Enter a YouTube URL or an 11 character video id.",
    };
  }
  return undefined;
}

export function validateTitleField(value: string): Bilingual | undefined {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return { ja: "曲名を入力してください。", en: "Enter the title." };
  }
  if (trimmed.length > MAX_TITLE_LENGTH) {
    return {
      ja: `曲名は${MAX_TITLE_LENGTH}文字以内にしてください。`,
      en: `The title must be ${MAX_TITLE_LENGTH} characters or fewer.`,
    };
  }
  return undefined;
}

export function validateArtistField(value: string): Bilingual | undefined {
  if (value.trim().length > MAX_ARTIST_LENGTH) {
    return {
      ja: `アーティスト名は${MAX_ARTIST_LENGTH}文字以内にしてください。`,
      en: `The artist must be ${MAX_ARTIST_LENGTH} characters or fewer.`,
    };
  }
  return undefined;
}

export function validateSongIdField(value: string): Bilingual | undefined {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return { ja: "曲IDを入力してください。", en: "Enter the song id." };
  }
  if (!SONG_ID_PATTERN.test(trimmed)) {
    return {
      ja: "英小文字・数字・ハイフンで3〜64文字にしてください。",
      en: "Use 3 to 64 lowercase letters, digits or dashes.",
    };
  }
  return undefined;
}

export function validateLevelField(value: number): Bilingual | undefined {
  if (!Number.isInteger(value)) {
    return { ja: "レベルは整数で入力してください。", en: "The level must be a whole number." };
  }
  if (value < MIN_LEVEL || value > MAX_LEVEL) {
    return {
      ja: `レベルは${MIN_LEVEL}〜${MAX_LEVEL}にしてください。`,
      en: `The level must be between ${MIN_LEVEL} and ${MAX_LEVEL}.`,
    };
  }
  return undefined;
}

export function validateDurationField(value: number): Bilingual | undefined {
  if (!Number.isFinite(value)) {
    return {
      ja: "再生時間（秒）を入力してください。",
      en: "Enter the duration in seconds.",
    };
  }
  if (value <= 0) {
    return {
      ja: "再生時間は0より長くしてください。",
      en: "The duration must be longer than zero.",
    };
  }
  if (value > MAX_DURATION_SEC) {
    return {
      ja: `再生時間は${MAX_DURATION_SEC}秒以内にしてください。`,
      en: `The duration must be ${MAX_DURATION_SEC} seconds or fewer.`,
    };
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
  const push = (field: NewSongField, message: Bilingual | undefined) => {
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
  message: Bilingual;
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
      message: {
        ja: "ノートを1つ以上置いてください。",
        en: "Add at least one note.",
      },
    };
  }
  if (count < 20) {
    return {
      level: "warn",
      message: {
        ja: `ノートが${count}個です。20未満は間延びしやすいので確認してください。`,
        en: `Only ${count} notes. Fewer than 20 plays sparse; double-check before publishing.`,
      },
    };
  }
  return null;
}
