/**
 * `catalogVersion` source of truth (issue #656).
 *
 * The contract counter is no longer `COUNT(chart_versions)`: archiving or
 * unarchiving a song changes the public catalog without adding a chart
 * version, so the count stayed still while the catalog changed. Instead a
 * single-row `catalog_meta` epoch is bumped inside the same transaction as
 * every catalog-visible mutation (publish, archive, unarchive), which makes
 * the value a monotonic "the catalog changed" token for polling clients.
 */
import { eq, sql } from "void/db";
import { db } from "void/db";
import { catalogMeta } from "@schema";

const CATALOG_META_ID = 1;

/** Current catalog epoch. */
export async function readCatalogVersion(): Promise<number> {
  const row = await db
    .select({ epoch: catalogMeta.epoch })
    .from(catalogMeta)
    .where(eq(catalogMeta.id, CATALOG_META_ID))
    .get();
  return row?.epoch ?? 0;
}

/**
 * Query that bumps the epoch by one. Include it in the same `db.batch` as
 * the catalog mutation so the counter and the change commit together.
 */
export function bumpCatalogVersion() {
  return db
    .update(catalogMeta)
    .set({ epoch: sql`${catalogMeta.epoch} + 1` })
    .where(eq(catalogMeta.id, CATALOG_META_ID));
}
