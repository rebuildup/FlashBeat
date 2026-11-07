import { defineHandler, type InferProps } from "void";

export type Props = InferProps<typeof loader>;

/** The new-song flow is client only: the chart editor draws into a canvas. */
export const ssr = false;

export const loader = defineHandler(async () => {
  return {};
});
