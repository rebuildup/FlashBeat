import { defineHandler, type InferProps } from "void";
export type Props = InferProps<typeof loader>;
export const ssr = false;
export const loader = defineHandler(async (c) => {
  const params = new URL(c.req.url).searchParams;
  return {
    songId: params.get("songId"),
    version: params.get("version"),
    /**
     * The local draft the publish screen handed back to the editor
     * (issue #98). The draft lives in the tab's session storage, so the
     * editor can restore it without a round trip to the API.
     */
    draftId: params.get("draft"),
  };
});
