import { defineHandler, type InferProps } from "void";
import type { CatalogMode } from "@/app/catalog";
import { isSortKey, type SortKey } from "@/app/catalog-view";
import { LEVEL_FILTERS, type LevelFilter } from "@/app/topbar";

export type Props = InferProps<typeof loader>;

const CATALOG_MODES: CatalogMode[] = ["loading", "ready", "empty", "error"];

/**
 * Resolve explicit preview states; normal navigation always loads the catalog.
 * `query`/`level`/`sort` seed the topbar filters so the filtered states of the
 * catalog panel are reviewable without typing (`/?query=…&level=…&sort=…`).
 */
export const loader = defineHandler(async (c) => {
  const params = new URL(c.req.url).searchParams;
  const requested = params.get("state");
  const mode = CATALOG_MODES.find((candidate) => candidate === requested) ?? "ready";

  const query = params.get("query") ?? "";
  const levelParam = params.get("level");
  const level: LevelFilter =
    levelParam !== null && LEVEL_FILTERS.some((filter) => filter.value === levelParam)
      ? (levelParam as LevelFilter)
      : "all";

  const sortParam = params.get("sort");
  const sort: SortKey = isSortKey(sortParam) ? sortParam : "catalog";

  return { mode, query, level, sort };
});
