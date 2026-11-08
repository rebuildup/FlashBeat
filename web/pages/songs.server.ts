import { defineHandler, type InferProps } from "void";

export type Props = InferProps<typeof loader>;

/**
 * The song management fills itself in the browser (it reads the live staff
 * API), so the page is not server rendered.
 */
export const ssr = false;

export const loader = defineHandler(async () => {
  return {};
});
