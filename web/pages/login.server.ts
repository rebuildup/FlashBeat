import { defineHandler, type InferProps } from "void";

export type Props = InferProps<typeof loader>;

/** The login form is client only: it talks to `/api/auth/*` and the session. */
export const ssr = false;

export const loader = defineHandler(async () => {
  return {};
});
