/**
 * `songId` slug helpers for the new-song flow.
 *
 * The contract pattern is `^[a-z0-9-]{3,64}$` (`contracts/openapi.v1.yaml`,
 * parameter `SongId`). A slug is suggested from the song title; Japanese
 * titles have no ascii letters, so the fallback derives from the video id.
 */

export const SONG_ID_MIN_LENGTH = 3;
export const SONG_ID_MAX_LENGTH = 64;

/**
 * Reduce a title to an ascii kebab-case slug.
 *
 * Everything outside `a-z0-9` (spaces, punctuation, Japanese kana/kanji)
 * collapses into one dash; leading and trailing dashes are removed. Returns
 * an empty string when nothing ascii survives.
 */
export function slugifyTitle(title: string): string {
  return title
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, SONG_ID_MAX_LENGTH)
    .replace(/-+$/g, "");
}

/** Slug used when the title yields no ascii (`song-<videoId>`). */
export function fallbackSlug(videoId: string): string {
  // A video id is `[A-Za-z0-9_-]{11}`; lowercased it satisfies the pattern.
  return `song-${videoId.toLowerCase()}`;
}

export interface SlugSuggestion {
  /** The slug the field should start with. */
  suggestion: string;
  /** `true` when the suggestion itself is already published. */
  taken: boolean;
  /** Free ids derived from the suggestion, first ones first. */
  alternatives: string[];
}

/**
 * Suggest a unique `songId` from the title against the published catalog.
 *
 * `takenIds` holds the ids of `GET /api/v1/catalog`. When the suggestion is
 * free the alternatives are empty; when it is taken the caller shows
 * 「このIDは使用済み」 and offers the alternatives.
 */
export function suggestSongSlug(
  title: string,
  videoId: string,
  takenIds: ReadonlySet<string>,
): SlugSuggestion {
  const fromTitle = slugifyTitle(title);
  if (fromTitle.length >= SONG_ID_MIN_LENGTH) {
    if (!takenIds.has(fromTitle)) {
      return { suggestion: fromTitle, taken: false, alternatives: [] };
    }
    return {
      suggestion: fromTitle,
      taken: true,
      alternatives: freeAlternatives(fromTitle, takenIds, 3),
    };
  }

  // Without a video id there is nothing to fall back to; the field stays
  // empty until the operator or the oEmbed lookup fills the title.
  if (videoId.length === 0) {
    return { suggestion: "", taken: false, alternatives: [] };
  }

  const base = fallbackSlug(videoId);
  if (!takenIds.has(base)) {
    return { suggestion: base, taken: false, alternatives: [] };
  }
  return { suggestion: base, taken: true, alternatives: freeAlternatives(base, takenIds, 3) };
}

/**
 * The first `count` free ids of the form `base-2`, `base-3`, …
 *
 * The suffix is included in the 64 character budget, so a maximum length
 * base is truncated before the suffix is appended.
 */
export function freeAlternatives(
  base: string,
  takenIds: ReadonlySet<string>,
  count: number,
): string[] {
  const alternatives: string[] = [];
  for (let n = 2; alternatives.length < count; n += 1) {
    const suffix = `-${n}`;
    const candidate = base.slice(0, SONG_ID_MAX_LENGTH - suffix.length) + suffix;
    if (!takenIds.has(candidate)) {
      alternatives.push(candidate);
    }
  }
  return alternatives;
}
