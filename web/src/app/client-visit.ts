import { RouterContext } from "@void/react";
import { useCallback, useContext } from "react";

/**
 * Client-side page navigation for hand-offs that carry state in the URL
 * (issue #801): the publish draft round-trips and the back-to-catalog
 * links. Inside the Void pages runtime this is `router.visit`, so the
 * document is not re-fetched and the shared layout stays mounted.
 *
 * `RouterContext` is `null` only outside the pages runtime (isolated
 * component tests, a stray SSR render); there the helper falls back to a
 * plain document navigation, which is what those anchors did before.
 */
export function useClientVisit(): (url: string) => void {
  const router = useContext(RouterContext);
  return useCallback(
    (url: string) => {
      if (router === null) {
        window.location.assign(url);
        return;
      }
      // A failed client visit (offline, aborted) degrades to the document
      // navigation the link used to perform.
      void router.visit(new URL(url, window.location.origin)).catch(() => {
        window.location.assign(url);
      });
    },
    [router],
  );
}
