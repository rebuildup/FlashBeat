import { defineHandler, type InferProps } from "void";

export type Props = InferProps<typeof loader>;

/** The my-songs screen is client only: it reads the session APIs. */
export const ssr = false;

export const loader = defineHandler(async () => {
  return {};
});
