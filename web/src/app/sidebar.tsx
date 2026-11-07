import { useEffect, useState } from "react";
import { css } from "styled-system/css";
import { BrandHeader } from "@/components/brand";

/**
 * Navigation of the Studio (issue #215): one visitor entry in front, the
 * staff routes grouped behind it. The visitor entry is the raised control of
 * design v5 (Prefab/Button Primary); staff entries stay in the muted list
 * look and carry the admin-token boundary on Publish, so a guest sees the
 * permission line before pressing it — not as an error after the fact.
 */
export const NAV_VISITOR = {
  value: "catalog",
  ja: "曲を選ぶ",
  en: "Pick a song",
  href: "/",
} as const;

export const NAV_STAFF_ITEMS = [
  { value: "new-song", ja: "新曲を登録", en: "New song", href: "/new-song" },
  { value: "editor", ja: "譜面を編集", en: "Edit chart", href: "/chart" },
  {
    value: "publish",
    ja: "公開する",
    en: "Publish",
    href: "/publish",
    admin: true,
  },
] as const;

const sidebar = css({
  bg: "gray.surface.bg",
  borderColor: "gray.surface.border",
  /** Narrow viewports: the rail becomes a compact top bar (issue #70 390px target). */
  alignItems: { base: "center", md: "stretch" },
  borderBottomWidth: { base: "1px", md: "0px" },
  borderRightWidth: { base: "0px", md: "1px" },
  display: "flex",
  flexDirection: { base: "row", md: "column" },
  flexWrap: "wrap",
  gap: "4",
  minHeight: "0",
  paddingBottom: { base: "2", md: "4" },
  paddingTop: { base: "2", md: "4" },
  px: "3",
  position: { base: "sticky", md: "static" },
  top: { base: "0", md: "auto" },
  zIndex: { base: "sticky", md: "base" },
});

const brand = css({
  alignItems: "center",
  display: "flex",
  paddingInline: "2",
});

const navList = css({
  display: "flex",
  flexDirection: { base: "row", md: "column" },
  flex: "1",
  gap: "1",
});

const groupLabel = css({
  color: "fg.subtle",
  display: { base: "none", md: "block" },
  fontSize: "2xs",
  letterSpacing: "0.08em",
  paddingInline: "3",
  paddingTop: "2",
  textTransform: "uppercase",
  whiteSpace: "nowrap",
});

const navTrigger = css({
  alignItems: "center",
  borderRadius: "l2",
  color: "fg.muted",
  display: "flex",
  gap: "2",
  minHeight: "10",
  px: "3",
  textDecoration: "none",
  whiteSpace: "nowrap",
  _hover: { bg: "#262626", color: "fg.default" },
  _focusVisible: { outline: "2px solid #3FA9F5", outlineOffset: "2px" },
  "&[aria-current=page]": { bg: "#2B4457", color: "#9AD3FA" },
  justifyContent: "flex-start",
  textAlign: "left",
  width: { base: "auto", md: "100%" },
});

/** v5 primary CTA: the raised #EDEDED control with the dark label. */
const visitorTrigger = css({
  bg: "#EDEDED",
  color: "#1B1B1B",
  fontWeight: "medium",
  _hover: { bg: "#EDEDED", opacity: "0.9", color: "#1B1B1B" },
  "&[aria-current=page]": { bg: "#EDEDED", color: "#1B1B1B", opacity: "0.9" },
});

const staffDivider = css({
  bg: "gray.surface.border",
  height: "1px",
  width: "100%",
});

const labelGrid = css({
  display: "grid",
  lineHeight: "1.2",
  textAlign: "left",
});

const labelEn = css({
  fontSize: "2xs",
  opacity: "0.7",
});

/** Visible admin-token badge on Publish: the boundary shows before the click. */
const adminBadge = css({
  bg: "#00000040",
  borderRadius: "full",
  color: "fg.muted",
  fontSize: "2xs",
  marginLeft: "auto",
  paddingBlock: "0.5",
  paddingInline: "2",
  whiteSpace: "nowrap",
});

export function Sidebar() {
  // The active entry follows the URL; the default covers the server-rendered
  // first paint of `/` (the catalog).
  const [path, setPath] = useState("/");
  useEffect(() => {
    setPath(window.location.pathname);
  }, []);

  return (
    <aside className={sidebar}>
      <div className={brand}>
        <BrandHeader />
      </div>

      <nav aria-label="Studio" className={navList}>
        <a
          href={NAV_VISITOR.href}
          aria-current={NAV_VISITOR.href === path ? "page" : undefined}
          className={`${navTrigger} ${visitorTrigger}`}
          data-testid="nav-visitor"
        >
          <span className={labelGrid}>
            <span>{NAV_VISITOR.ja}</span>
            <span className={labelEn}>{NAV_VISITOR.en}</span>
          </span>
        </a>

        <div className={staffDivider} aria-hidden="true" />
        <p className={groupLabel}>スタッフ操作 / Staff</p>

        {NAV_STAFF_ITEMS.map((item) => (
          <a
            key={item.value}
            href={item.href}
            aria-current={item.href === path ? "page" : undefined}
            className={navTrigger}
            data-testid={`nav-${item.value}`}
          >
            <span className={labelGrid}>
              <span>{item.ja}</span>
              <span className={labelEn}>{item.en}</span>
            </span>
            {"admin" in item && item.admin ? (
              <span className={adminBadge} data-testid="nav-publish-admin-badge">
                要トークン
              </span>
            ) : null}
          </a>
        ))}
      </nav>
    </aside>
  );
}
