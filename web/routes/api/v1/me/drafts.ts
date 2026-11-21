/**
 * `POST /api/v1/me/drafts` — create a song draft (issue #800).
 *
 * Studio-internal API. A draft is the working copy of one intended public
 * song: metadata plus the chart JSON being edited. It never appears in
 * the catalog, the admin song list or `chart_versions` — only
 * `POST /api/v1/me/drafts/{draftId}/publish` moves it there.
 *
 *   - 401 — no session.
 *   - 403 — the target workspace exists but the caller's role is `viewer`.
 *   - 404 — `workspaceId` names a workspace the caller does not belong to
 *     (no existence leak).
 *   - 409 — the `songId` is already taken: another draft reserved it, or a
 *     `songs` row owned by a different workspace (or staff-managed) holds
 *     it. First publisher wins.
 *   - 422 — a present field fails validation; nothing is written.
 *   - 201 — `{ draft }` with the server-assigned `draft-<uuid>` id.
 *
 * `songId` is optional: omitted, the server assigns `u-<8 hex>` so a draft
 * can start before the operator picks a public id. Rate limited per user
 * and audited (`draft.create`).
 */
import { defineHandler } from "void";
import { db, eq } from "void/db";
import { songDrafts, songs } from "@schema";
import {
  consumeRateLimit,
  findMembership,
  hasWorkspaceRole,
  requireWorkspaceContext,
  writeAuditLog,
} from "../_authz";
import { readJsonLimited, PUBLISH_BODY_LIMIT } from "../_request-body";
import { validateDraftBody, type DraftPatch } from "../_me-model";

/** Draft creates per user per minute (chart-sized bodies are the norm). */
const DRAFT_WRITE_LIMIT = 30;
const DRAFT_WRITE_WINDOW_SECONDS = 60;

/** Assign a collision-resistant default public id (`u-` + 8 hex chars). */
function defaultSongId(): string {
  return `u-${crypto.randomUUID().replaceAll("-", "").slice(0, 8)}`;
}

/**
 * Whether `songId` may be claimed by `workspaceId`:
 *   - a `songs` row owned by the same workspace is the normal update flow;
 *   - a `songs` row owned by anyone else (or staff-managed, `NULL`) is a
 *     hard conflict — user publishes never overwrite foreign ids;
 *   - a `song_drafts` row of another workspace cannot exist (the UNIQUE
 *     index on `songId` enforces it), the insert catches the race.
 */
async function songIdConflict(
  songId: string,
  workspaceId: string,
): Promise<"free" | "own" | "taken"> {
  const existing = await db
    .select({ ownerWorkspaceId: songs.ownerWorkspaceId })
    .from(songs)
    .where(eq(songs.id, songId))
    .get();
  if (!existing) {
    return "free";
  }
  return existing.ownerWorkspaceId === workspaceId ? "own" : "taken";
}

export const POST = defineHandler(async (c) => {
  const context = await requireWorkspaceContext(c);
  if (!context) {
    return c.json({ error: "authentication required" }, 401);
  }

  if (
    !(await consumeRateLimit(
      "me.draft.write",
      context.user.id,
      DRAFT_WRITE_LIMIT,
      DRAFT_WRITE_WINDOW_SECONDS,
    ))
  ) {
    return c.json({ error: "rate limit exceeded" }, 429);
  }

  const limited = await readJsonLimited(c, PUBLISH_BODY_LIMIT);
  if (!limited.ok) {
    return limited.response;
  }
  const body = limited.value;

  const validation = validateDraftBody(body);
  if (!validation.ok) {
    return c.json({ error: "invalid draft request", diagnostics: validation.diagnostics }, 422);
  }
  const patch = validation.patch;

  // Target workspace: the personal workspace by default, or any workspace
  // the caller can edit (`workspaceId` in the body, editor role required).
  let workspaceId = context.workspaceId;
  const requestedWorkspace = (body as Record<string, unknown>).workspaceId;
  if (requestedWorkspace !== undefined) {
    if (typeof requestedWorkspace !== "string" || requestedWorkspace.length === 0) {
      return c.json(
        {
          error: "invalid draft request",
          diagnostics: [{ path: "workspaceId", message: "must be a workspace id string" }],
        },
        422,
      );
    }
    const requested = requestedWorkspace;
    const membership = await findMembership(requested, context.user.id);
    if (!membership) {
      return c.json({ error: "workspace not found" }, 404);
    }
    if (!hasWorkspaceRole(membership.role, "editor")) {
      return c.json({ error: "editor role required to create drafts" }, 403);
    }
    workspaceId = requested;
  } else {
    // The default personal workspace membership is `owner`; still verify
    // the role defensively (a future role model could change it).
    if (!hasWorkspaceRole(context.role, "editor")) {
      return c.json({ error: "editor role required to create drafts" }, 403);
    }
  }

  // Resolve the public song id: explicit (validated above) or generated.
  // An explicit id that a `songs` row of another workspace holds is a 409;
  // the same check runs again inside the publish transaction.
  let songId = patch.songId ?? null;
  if (songId !== null) {
    const conflict = await songIdConflict(songId, workspaceId);
    if (conflict === "taken") {
      return c.json(
        {
          error: "song id is already in use",
          diagnostics: [
            {
              path: "songId",
              message:
                "this song id is published by another workspace or managed by staff; pick a different id",
            },
          ],
        },
        409,
      );
    }
  } else {
    // Generate until the UNIQUE index accepts one (practically never
    // loops; bounded so a pathological collision cannot spin forever).
    for (let attempt = 0; attempt < 5; attempt += 1) {
      const candidate = defaultSongId();
      if ((await songIdConflict(candidate, workspaceId)) !== "taken") {
        songId = candidate;
        break;
      }
    }
    if (songId === null) {
      return c.json({ error: "could not allocate a song id" }, 500);
    }
  }

  const draftId = `draft-${crypto.randomUUID()}`;
  const values = draftInsertValues(draftId, workspaceId, context.user.id, songId, patch);

  try {
    await db.insert(songDrafts).values(values);
  } catch {
    // UNIQUE(songId) race: another draft claimed the id between the check
    // and the insert.
    return c.json(
      {
        error: "song id is already in use",
        diagnostics: [{ path: "songId", message: "another draft already reserved this song id" }],
      },
      409,
    );
  }

  await writeAuditLog({
    actorUserId: context.user.id,
    workspaceId,
    action: "draft.create",
    targetType: "song_draft",
    targetId: draftId,
    detail: { songId },
  });

  const created = await db.select().from(songDrafts).where(eq(songDrafts.id, draftId)).get();
  return c.json({ draft: created }, 201);
});

/** Column values of a new draft row from a validated patch. */
function draftInsertValues(
  id: string,
  ownerWorkspaceId: string,
  createdByUserId: string,
  songId: string,
  patch: DraftPatch,
) {
  return {
    id,
    ownerWorkspaceId,
    createdByUserId,
    songId,
    title: patch.title ?? "",
    artist: patch.artist ?? "",
    youtubeId: patch.youtubeId ?? null,
    level: patch.level ?? null,
    durationSec: patch.durationSec ?? null,
    chartJson: patch.chart ? JSON.stringify(patch.chart) : null,
    noteCount: patch.noteCount ?? 0,
    materialSource: patch.materialSource ?? null,
    rightsStatus: patch.rightsStatus ?? null,
    license: patch.license ?? null,
  };
}
