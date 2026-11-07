import type { PublishFailure } from "./types";

/**
 * Where a publish failure belongs.
 *
 * A rejected token is a problem of the token field, everything else is a
 * problem of the request itself. The two are told apart so the operator does
 * not read a broken chart as a wrong token.
 */
export interface PublishFailureView {
  /** Message that belongs to the staff token field. */
  tokenError?: string;
  /** Message that belongs to the request, not to the token. */
  requestError?: string;
  /** Field level problems the API reported. */
  details: string[];
}

/**
 * Split a failure into the token field and the request.
 *
 * Only `unauthorized` is a token problem: the API answered 401 or 403.
 */
export function describeFailure(failure: PublishFailure): PublishFailureView {
  const details = failure.details ?? [];

  if (failure.code === "unauthorized") {
    return { tokenError: failure.message, details };
  }

  return { requestError: failure.message, details };
}
