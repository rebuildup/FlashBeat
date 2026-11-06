import { defineHandler, type InferProps } from "void";
export type Props = InferProps<typeof loader>;
export const ssr = false;
export const loader = defineHandler(async (c) => {
  const params = new URL(c.req.url).searchParams;
  return { songId: params.get("songId"), version: params.get("version") };
});
