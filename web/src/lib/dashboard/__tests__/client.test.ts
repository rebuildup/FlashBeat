import { describe, expect, it } from "vitest";
import { parseAdminSongs, parseArchiveResponse, parseRankings, parseStats } from "../client";

/**
 * The parsers of the dashboard API (issue #151).
 *
 * They are the contract between the API and the dashboard: a broken answer
 * has to be an error and not a half filled table.
 */

const rankingsAnswer = {
  catalogVersion: 38,
  songs: [
    {
      songId: "who",
      title: "Who",
      artist: "Azari",
      level: 5,
      chartVersion: 10,
      plays: 2,
      entries: [{ nickname: "Tester", score: 887769 }],
    },
  ],
};

const statsAnswer = {
  generatedAt: "2026-11-07T00:00:00.000Z",
  totals: {
    songs: 2,
    liveSongs: 1,
    archivedSongs: 1,
    charts: 3,
    scores: 4,
    players: 2,
    playsLast24h: 1,
  },
  perSong: [
    {
      songId: "who",
      title: "Who",
      artist: "Azari",
      level: 5,
      bpm: 128,
      durationSec: 68,
      chartVersion: 10,
      archived: false,
      plays: 4,
      distinctPlayers: 2,
      averageScore: 800000,
      bestScore: 887769,
      averageAccuracy: 0.8,
    },
  ],
  levelDistribution: [{ level: 5, songs: 1 }],
  topPlayers: [{ nickname: "Tester", plays: 2, totalScore: 1500000, bestScore: 887769 }],
};

describe("parseRankings", () => {
  it("reads the board of the API", () => {
    const board = parseRankings(rankingsAnswer);
    expect(board.catalogVersion).toBe(38);
    expect(board.songs).toHaveLength(1);
    expect(board.songs[0]?.songId).toBe("who");
    expect(board.songs[0]?.entries[0]).toEqual({ nickname: "Tester", score: 887769 });
  });

  it("rejects an answer without a song list", () => {
    expect(() => parseRankings({ catalogVersion: 1 })).toThrow(/rankings/);
    expect(() => parseRankings({ songs: [] })).toThrow(/rankings/);
    expect(() => parseRankings(null)).toThrow(/rankings/);
  });

  it("rejects a song with a broken field", () => {
    const broken = structuredClone(rankingsAnswer) as typeof rankingsAnswer;
    broken.songs[0]!.level = "five" as unknown as number;
    expect(() => parseRankings(broken)).toThrow(/rankings/);
  });
});

describe("parseStats", () => {
  it("reads the summary of the API", () => {
    const stats = parseStats(statsAnswer);
    expect(stats.totals.liveSongs).toBe(1);
    expect(stats.totals.archivedSongs).toBe(1);
    expect(stats.perSong[0]?.averageAccuracy).toBeCloseTo(0.8);
    expect(stats.topPlayers[0]?.nickname).toBe("Tester");
  });

  it("keeps the nullable fields of a song without a play", () => {
    const withoutPlays = structuredClone(statsAnswer) as {
      perSong: { averageScore: number | null; bestScore: number | null; averageAccuracy: number | null }[];
    };
    withoutPlays.perSong[0]!.averageScore = null;
    withoutPlays.perSong[0]!.bestScore = null;
    withoutPlays.perSong[0]!.averageAccuracy = null;
    const stats = parseStats(withoutPlays);
    expect(stats.perSong[0]?.bestScore).toBeNull();
    expect(stats.perSong[0]?.averageAccuracy).toBeNull();
  });

  it("rejects a broken summary", () => {
    expect(() => parseStats({ generatedAt: "now" })).toThrow(/stats/);
    expect(() => parseStats({ ...statsAnswer, totals: { songs: 1 } })).toThrow(/stats/);
  });
});

describe("parseAdminSongs", () => {
  it("reads the staff list with the archived flag", () => {
    const list = parseAdminSongs({
      catalogVersion: 3,
      songs: [
        {
          id: "who",
          title: "Who",
          artist: "Azari",
          youtubeId: "8JXiXt0D6tw",
          level: 5,
          bpm: 128,
          durationSec: 68,
          chartVersion: 10,
          archived: true,
          archivedAt: "2026-11-07T00:00:00.000Z",
          plays: 4,
          bestScore: 887769,
        },
      ],
    });
    expect(list.songs[0]?.archived).toBe(true);
    expect(list.songs[0]?.archivedAt).toBe("2026-11-07T00:00:00.000Z");
  });

  it("rejects a list without the archive flag", () => {
    expect(() =>
      parseAdminSongs({
        catalogVersion: 1,
        songs: [{ id: "who", title: "Who", archived: "yes" }],
      }),
    ).toThrow(/admin\/songs/);
  });
});

describe("parseArchiveResponse", () => {
  it("reads the answer of the archive route", () => {
    const answer = parseArchiveResponse({
      songId: "who",
      archived: true,
      archivedAt: "2026-11-07T00:00:00.000Z",
      catalogVersion: 38,
    });
    expect(answer.archived).toBe(true);
    expect(answer.catalogVersion).toBe(38);
  });

  it("rejects an answer without the flag", () => {
    expect(() => parseArchiveResponse({ songId: "who", catalogVersion: 1 })).toThrow(/archive/);
  });
});
