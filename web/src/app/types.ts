/**
 * Shape of a catalog entry.
 *
 * Mirrors `SongSummary` in `contracts/openapi.v1.yaml` so the read catalog
 * endpoint (#15) can be dropped in without reshaping the UI.
 */
export interface SongSummary {
  id: string;
  title: string;
  artist: string;
  youtubeId: string;
  level: number;
  bpm: number;
  durationSec: number;
  chartVersion: number;
  noteCount: number;
}
