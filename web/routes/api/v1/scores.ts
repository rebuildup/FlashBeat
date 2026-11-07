/**
 * `POST /api/v1/scores` — public festival score submission (issue #133).
 *
 * Contract: `contracts/openapi.v1.yaml` (`submitScore`). No login; the
 * server owns the truth:
 *
 *   - The judgment counts must sum up to the note count of the submitted
 *     chart version (`chart_versions.noteCount`), and the version must be an
 *     existing snapshot of a published song (the current one or an older
 *     one — old snapshots stay submittable, unknown ones are 422).
 *   - The score is recomputed server-side with the exact Unity formula
 *     (`computeUnityScore` in `_score-model.ts`). A client-sent `score` that
 *     disagrees is a 422 with the expected and received values as
 *     diagnostics; nothing is stored.
 *   - The nickname is trimmed to 1-12 characters without control characters.
 *   - A duplicate identical submission (same songId + nickname + score)
 *     within 10 seconds is a 429 with `Retry-After` — a light abuse limit
 *     that needs no extra infrastructure. Time is only ever read from the
 *     server clock (`scores.createdAt` is written by SQLite itself), never
 *     from the client.
 *
 * A successful submission answers `201` with the stored score and its
 * leaderboard rank (1 + number of strictly better scores of the same chart
 * version; ties share the rank, earliest first).
 */
import { defineHandler } from "void";
import { and, count, db, desc, eq, gt } from "void/db";
import { chartVersions, scores } from "@schema";
import {
  computeUnityScore,
  nicknameDiagnostics,
  scoreSemanticsDiagnostics,
  validateScoreRequest,
  type ScoreDiagnostic,
} from "./_score-model";
import { SONG_ID_PATTERN } from "./_read-model";

/** Window of the duplicate-submission limit, in seconds. */
const DUPLICATE_WINDOW_SECONDS = 10;

export const POST = defineHandler(async (c) => {
  let body: unknown;
  try {
    body = await c.req.json();
  } catch {
    return c.json(
      {
        error: "request body is not valid JSON",
        diagnostics: [
          { path: "(body)", message: "must be a single JSON object" } satisfies ScoreDiagnostic,
        ],
      },
      422,
    );
  }

  const validation = validateScoreRequest(body);
  if (!validation.ok) {
    return c.json({ error: "invalid score submission", diagnostics: validation.diagnostics }, 422);
  }
  const request = validation.request;

  if (!SONG_ID_PATTERN.test(request.songId)) {
    return c.json(
      {
        error: "invalid score submission",
        diagnostics: [
          {
            path: "/songId",
            message: "no published song matches this id",
          } satisfies ScoreDiagnostic,
        ],
      },
      422,
    );
  }

  // The song must be published and the version an existing snapshot.
  // `chart_versions.songId` references `songs.id`, so a row here proves both.
  const chart = await db
    .select({ noteCount: chartVersions.noteCount })
    .from(chartVersions)
    .where(
      and(
        eq(chartVersions.songId, request.songId),
        eq(chartVersions.version, request.chartVersion),
      ),
    )
    .get();
  if (!chart) {
    return c.json(
      {
        error: "invalid score submission",
        diagnostics: [
          {
            path: "/chartVersion",
            message: `no published chart version ${request.chartVersion} of "${request.songId}"`,
          } satisfies ScoreDiagnostic,
        ],
      },
      422,
    );
  }

  const nickname = request.nickname.trim();
  const nicknameProblems = nicknameDiagnostics(request.nickname);
  const semantics = scoreSemanticsDiagnostics(request, chart.noteCount);
  if (nicknameProblems.length > 0 || semantics.length > 0) {
    return c.json(
      { error: "invalid score submission", diagnostics: [...nicknameProblems, ...semantics] },
      422,
    );
  }

  const score = computeUnityScore(request.perfect, request.great, request.bad, chart.noteCount);
  if (score === null) {
    return c.json(
      {
        error: "invalid score submission",
        diagnostics: [
          {
            path: "/chartVersion",
            message: `chart version ${request.chartVersion} of "${request.songId}" has no notes; it cannot be played or scored`,
          } satisfies ScoreDiagnostic,
        ],
      },
      422,
    );
  }
  if (request.score !== undefined && request.score !== score) {
    return c.json(
      {
        error: "score mismatch",
        diagnostics: [
          {
            path: "/score",
            message: `the judgment counts score ${score} with the Unity formula, the client sent ${request.score}`,
          } satisfies ScoreDiagnostic,
        ],
      },
      422,
    );
  }

  // Light abuse limit: the identical submission (same song, nickname and
  // score) inside the window is rejected. The timestamp comes from the
  // database clock only (`createdAt` defaults to `datetime('now')`).
  const previous = await db
    .select({ createdAt: scores.createdAt })
    .from(scores)
    .where(
      and(
        eq(scores.songId, request.songId),
        eq(scores.nickname, nickname),
        eq(scores.score, score),
      ),
    )
    .orderBy(desc(scores.id))
    .get();
  if (previous) {
    const elapsedSeconds = (Date.now() - Date.parse(`${previous.createdAt}Z`)) / 1000;
    const remaining = DUPLICATE_WINDOW_SECONDS - elapsedSeconds;
    if (remaining > 0) {
      c.header("Retry-After", String(Math.max(1, Math.ceil(remaining))));
      return c.json(
        {
          error: "duplicate submission",
          diagnostics: [
            {
              path: "(request)",
              message: `the same score for "${request.songId}" and "${nickname}" was already accepted; retry after the cooldown`,
            } satisfies ScoreDiagnostic,
          ],
        },
        429,
      );
    }
  }

  await db.insert(scores).values({
    songId: request.songId,
    version: request.chartVersion,
    nickname,
    p: request.perfect,
    g: request.great,
    b: request.bad,
    m: request.miss,
    score,
  });

  // Competition rank: one plus the number of strictly better scores of the
  // same chart version. Ties share the rank; the leaderboard breaks them by
  // earliest submission.
  const [better] = await db
    .select({ value: count() })
    .from(scores)
    .where(
      and(
        eq(scores.songId, request.songId),
        eq(scores.version, request.chartVersion),
        gt(scores.score, score),
      ),
    );

  return c.json({ score, rank: better.value + 1 }, 201);
});
