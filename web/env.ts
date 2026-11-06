/**
 * Server environment of the festival API.
 *
 * `FLASHBEAT_ADMIN_TOKEN` is the staff bearer token of
 * `PUT /api/v1/admin/songs/{songId}` (contracts/openapi.v1.yaml,
 * `staffToken`). It lives only in the Worker runtime: locally in `.env` or the
 * shell, remotely as a Worker secret (`void secret put
 * FLASHBEAT_ADMIN_TOKEN`, provisioned from the FlashBeat Infisical project).
 * It is never a `VITE_` value, so it can never reach the HTML/JS bundle, and
 * it must never be logged or committed.
 */
import { defineEnv, string } from "void/env";

export default defineEnv({
  FLASHBEAT_ADMIN_TOKEN: string(),
});
