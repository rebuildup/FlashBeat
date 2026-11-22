import type { ReactNode } from "react";
import { useRouter } from "@void/react";
import { StudioFrame } from "@/app/studio-frame";

/**
 * The root layout of the Studio (issue #801).
 *
 * Void keeps a layout mounted while pages navigate inside it, so the
 * sidebar, its disclosure state and the scroll owner of the frame survive
 * every client-side route change — only `children` (the page) is swapped.
 *
 * The frame is viewport-locked by default: the chart editor, the publish
 * screen, the guided flow and the staff pages all scroll inside their own
 * content area. The catalog is the exception — it reads as one scrolling
 * document — so the frame follows the document-scroll variant on `/`.
 *
 * The scroll mode is picked from the route here, inside the single layout
 * component, on purpose: a named layout would be a different component
 * type and React would remount the whole frame — sidebar included — when
 * navigation crossed between the two variants.
 */
export default function StudioLayout({ children }: { children: ReactNode }) {
  const { path } = useRouter();
  // The chart editor owns a canvas workspace and keeps the full track;
  // every other page shares the bounded content measure (issue #802).
  return (
    <StudioFrame fixed={path !== "/"} fluid={path === "/chart"}>
      {children}
    </StudioFrame>
  );
}
