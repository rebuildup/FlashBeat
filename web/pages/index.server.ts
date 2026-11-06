import { defineHandler, type InferProps } from "void";
import type { CatalogMode } from "@/app/catalog";

export type Props = InferProps<typeof loader>;

const CATALOG_MODES: CatalogMode[] = ["loading", "ready", "empty", "error"];

/**
 * The shell itself has no backend yet, so the loader only resolves which
 * catalog state the page should render.
 *
 * `?state=loading|empty|error` forces a state so every branch of the list can
 * be reviewed without a deployed API. Without the parameter the catalog is
 * rendered in its normal, ready state.
 */
export const loader = defineHandler(async (c) => {
  const requested = new URL(c.req.url).searchParams.get("state");
  const mode = CATALOG_MODES.find((candidate) => candidate === requested) ?? "ready";

  return { mode };
});
