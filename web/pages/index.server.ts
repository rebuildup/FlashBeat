import { defineHandler, type InferProps } from "void";
import type { CatalogMode } from "@/app/catalog";

export type Props = InferProps<typeof loader>;

const CATALOG_MODES: CatalogMode[] = ["loading", "ready", "empty", "error"];

/** Resolve explicit preview states; normal navigation always loads the catalog. */
export const loader = defineHandler(async (c) => {
  const requested = new URL(c.req.url).searchParams.get("state");
  const mode = CATALOG_MODES.find((candidate) => candidate === requested) ?? "ready";

  return { mode };
});
