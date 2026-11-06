import { defineHandler, type InferProps } from "void";
import type { CatalogMode } from "@/app/catalog";
import { LEVEL_FILTERS, type LevelFilter } from "@/app/topbar";

export type Props = InferProps<typeof loader>;

const CATALOG_MODES: CatalogMode[] = ["loading", "ready", "empty", "error"];

/**
 * Resolve explicit preview states; normal navigation always loads the catalog.
 * `query`/`level` seed the topbar filters so the filter-no-match state of the
 * catalog panel is reviewable without typing (`/?query=…&level=…`).
 */
export const loader = defineHandler(async (c) => {
  const params = new URL(c.req.url).searchParams;
  const requested = params.get("state");
  const mode = CATALOG_MODES.find((candidate) => candidate === requested) ?? "ready";

  const query = params.get("query") ?? "";
  const levelParam = params.get("level");
  const level: LevelFilter =
    LEVEL_FILTERS.some((filter) => filter.value === levelParam) && levelParam !== null
      ? (levelParam as LevelFilter)
      : "all";

  return { mode, query, level };
});
