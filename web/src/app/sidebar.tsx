import { useEffect, useState } from "react";
import { css } from "styled-system/css";
import {
  Archive,
  ChartColumnBig,
  ChevronDown,
  CloudUpload,
  ListMusic,
  PencilRuler,
  Sparkles,
  type LucideIcon,
} from "lucide-react";
import { BrandHeader } from "@/components/brand";

/**
 * Navigation of the Studio (issue #215): one visitor entry in front, the
 * staff routes grouped behind it. The visitor entry is the raised control of
 * design v5 (Prefab/Button Primary); staff entries stay in the muted list
 * look and carry the admin-token boundary on Publish, so a guest sees the
 * permission line before pressing it — not as an error after the fact.
 *
 * Every entry carries an icon (issue #151): a text-only rail is hard to
 * scan, and the icons are the same in every page of the studio.
 */
export const NAV_VISITOR = {
  value: "catalog",
  ja: "曲を選ぶ",
  en: "Pick a song",
  href: "/",
} as const;

/** One navigation entry of the studio. */
export type StudioNavItem = {
  value: string;
  /** The English label (used by `NAV_ITEMS`, the flat icon list). */
  label: string;
  ja: string;
  en: string;
  href: string;
  icon: LucideIcon;
  /** True when the route needs the admin token. */
  admin?: boolean;
};

export const NAV_STAFF_ITEMS: readonly StudioNavItem[] = [
  { value: "dashboard", label: "Dashboard", ja: "ダッシュボード", en: "Dashboard", href: "/dashboard", icon: ChartColumnBig },
  { value: "songs", label: "Songs", ja: "楽曲アーカイブ", en: "Songs", href: "/songs", icon: Archive },
  { value: "new-song", label: "New song", ja: "新曲を登録", en: "New song", href: "/new-song", icon: Sparkles },
  { value: "editor", label: "New chart", ja: "譜面を編集", en: "Edit chart", href: "/chart", icon: PencilRuler },
  { value: "publish", label: "Publish", ja: "公開する", en: "Publish", href: "/publish", icon: CloudUpload, admin: true },
];

/**
 * The flat, icon-first list of every route of the studio. The rail renders
 * `NAV_VISITOR` + `NAV_STAFF_ITEMS`; this export keeps the flat form for
 * callers that want the whole set in one array (issue #151).
 */
export const NAV_ITEMS: readonly StudioNavItem[] = [
  { value: "catalog", label: "Catalog", ja: NAV_VISITOR.ja, en: NAV_VISITOR.en, href: NAV_VISITOR.href, icon: ListMusic },
  ...NAV_STAFF_ITEMS,
];

const sidebar = css({
  bg: "gray.surface.bg",
  borderColor: "gray.surface.border",
  /** Narrow viewports: the rail becomes a compact top bar (issue #70 390px target). */
  alignItems: { base: "stretch", md: "stretch" },
  borderBottomWidth: { base: "1px", md: "0px" },
  borderRightWidth: { base: "0px", md: "1px" },
  display: "flex",
  flexDirection: { base: "column", md: "column" },
  flexWrap: "wrap",
  /**
   * Narrow viewports keep the tight gap of the reflowed rail (issue #74);
   * wide viewports keep the spacing of the desktop rail (4), so the desktop
   * layout is not changed by the narrow-viewport work.
   */
  gap: { base: "2", md: "4" },
  minHeight: "0",
  /**
   * The rail must never widen the document: a narrow viewport reflows the
   * entries (issue #74), so nothing sticks out sideways. The entries wrap
   * inside the rail, so no `overflow-x: hidden` guard is needed — a guard
   * would only hide a future long label without a scroll path.
   */
  maxWidth: "100%",
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
  flexDirection: "column",
  flex: { base: "none", md: "1" },
  gap: "1",
  minWidth: "0",
  width: { base: "full", md: "auto" },
});

/**
 * Narrow viewports: the visitor entry and the staff toggle share one row,
 * the staff entries themselves live in a disclosure below (issue #74).
 * Wide viewports: the wrapper disappears (`display: contents`), so the rail
 * keeps its plain column of entries.
 */
const navTopRow = css({
  alignItems: "center",
  display: { base: "flex", md: "contents" },
  gap: "2",
  minWidth: "0",
  width: { base: "full", md: "auto" },
});

/**
 * The staff group: a grid on narrow viewports, a plain column on wide ones.
 *
 * The open/closed state is carried by `data-open` (mirroring `aria-expanded`
 * of the toggle) instead of a separate class per state, so the disclosure can
 * be asserted without a browser.
 */
const staffGroup = css({
  gap: { base: "2", md: "1" },
  gridTemplateColumns: { base: "repeat(2, minmax(0, 1fr))" },
  minWidth: "0",
  width: { base: "full", md: "auto" },
  "&[data-open=false]": { display: { base: "none", md: "contents" } },
  "&[data-open=true]": { display: { base: "grid", md: "contents" } },
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
  columnGap: "2",
  borderRadius: "l2",
  color: "fg.muted",
  colorPalette: "blue",
  display: "flex",
  flexWrap: "wrap",
  gap: "2",
  minHeight: "10",
  minWidth: "0",
  px: "3",
  textDecoration: "none",
  /** Narrow: long labels wrap inside the grid cell instead of pushing sideways. */
  whiteSpace: { base: "normal", md: "nowrap" },
  _hover: { bg: "gray.subtle.bg.hover", color: "fg.default" },
  _focusVisible: {
    outline: "2px solid",
    outlineColor: "colorPalette.solid.bg",
    outlineOffset: "2px",
  },
  "&[aria-current=page]": { bg: "colorPalette.subtle.bg", color: "colorPalette.subtle.fg" },
  justifyContent: "flex-start",
  textAlign: "left",
  width: { base: "auto", md: "100%" },
});

/** v5 primary CTA class (Prefab/Button Primary): the visitor entry is the
 * one raised control of the rail — same class as the screen primary CTAs. */
const visitorTrigger = css({
  colorPalette: "blue",
  bg: "colorPalette.solid.bg",
  color: "colorPalette.solid.fg",
  flex: { base: "1", md: "none" },
  fontWeight: "medium",
  minWidth: "0",
  _hover: { opacity: "0.9" },
  "&[aria-current=page]": { opacity: "0.9" },
});

/**
 * The disclosure of the staff entries on narrow viewports (issue #74): the
 * rail keeps its own height, so the work area below is not pushed away.
 */
const staffToggle = css({
  alignItems: "center",
  background: "transparent",
  borderColor: "gray.surface.border",
  borderRadius: "l2",
  borderWidth: "1px",
  color: "fg.muted",
  cursor: "pointer",
  display: { base: "inline-flex", md: "none" },
  flexShrink: "0",
  gap: "2",
  minHeight: "10",
  paddingInline: "3",
  textAlign: "left",
  _focusVisible: {
    outline: "2px solid",
    outlineColor: "colorPalette.solid.bg",
    outlineOffset: "2px",
  },
  "&[aria-expanded=true]": { color: "fg.default" },
});

const staffToggleIcon = css({
  color: "currentColor",
  flexShrink: "0",
  transition: "transform 120ms ease-out",
  "[aria-expanded=true] &": { transform: "rotate(180deg)" },
});

const staffDivider = css({
  bg: "gray.surface.border",
  display: { base: "none", md: "block" },
  height: "1px",
  width: "100%",
});

const labelGrid = css({
  display: "grid",
  lineHeight: "1.2",
  minWidth: "0",
  textAlign: "left",
});

const labelEn = css({
  fontSize: "2xs",
  opacity: "0.7",
});

/** Visible admin-token badge on Publish: the boundary shows before the click. */
const adminBadge = css({
  bg: "gray.subtle.bg",
  borderRadius: "full",
  color: "fg.muted",
  fontSize: "2xs",
  marginLeft: "auto",
  paddingBlock: "0.5",
  paddingInline: "2",
  whiteSpace: "nowrap",
});

/** The icon of a navigation entry. */
const navIcon = css({
  color: "currentColor",
  flexShrink: "0",
});

export function Sidebar({ active }: { active?: string }) {
  // The active entry follows the URL; the default covers the server-rendered
  // first paint of `/` (the catalog). A page can pin it with `active`.
  const [path, setPath] = useState("/");
  // Narrow viewports keep the staff entries in a disclosure (issue #74).
  const [staffOpen, setStaffOpen] = useState(false);
  useEffect(() => {
    setPath(window.location.pathname);
  }, []);

  const isActive = (item: StudioNavItem): boolean => {
    if (active !== undefined) return item.value === active;
    if (item.href === "/") return path === "/";
    return path.startsWith(item.href);
  };

  return (
    <aside className={sidebar}>
      <div className={brand}>
        <BrandHeader />
      </div>

      <nav aria-label="Studio" className={navList}>
        <div className={navTopRow}>
          <a
            href={NAV_VISITOR.href}
            aria-current={NAV_VISITOR.href === path ? "page" : undefined}
            className={`${navTrigger} ${visitorTrigger}`}
            data-testid="nav-visitor"
          >
            <ListMusic className={navIcon} size={16} aria-hidden="true" />
            <span className={labelGrid}>
              <span>{NAV_VISITOR.ja}</span>
              <span className={labelEn}>{NAV_VISITOR.en}</span>
            </span>
          </a>

          <button
            type="button"
            className={staffToggle}
            aria-expanded={staffOpen}
            aria-controls="studio-staff-nav"
            onClick={() => setStaffOpen((open) => !open)}
            data-testid="nav-staff-toggle"
          >
            <span className={labelGrid}>
              <span>スタッフ操作</span>
              <span className={labelEn}>Staff</span>
            </span>
            <ChevronDown className={staffToggleIcon} size={16} aria-hidden="true" />
          </button>
        </div>

        <div
          id="studio-staff-nav"
          data-staff-nav=""
          data-open={staffOpen ? "true" : "false"}
          className={staffGroup}
        >
          <div className={staffDivider} aria-hidden="true" />
          <p className={groupLabel}>スタッフ操作 / Staff</p>

          {NAV_STAFF_ITEMS.map((item) => (
            <a
              key={item.value}
              href={item.href}
              aria-current={isActive(item) ? "page" : undefined}
              className={navTrigger}
              data-testid={`nav-${item.value}`}
            >
              <item.icon className={navIcon} size={16} aria-hidden="true" />
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
        </div>
      </nav>
    </aside>
  );
}
