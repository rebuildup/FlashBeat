import { useState } from "react";
import { Link } from "@void/react";
import type { Href } from "void/routes";
import { css } from "styled-system/css";
import { auth } from "void/client/react";
import {
  Archive,
  ChartColumnBig,
  ChevronDown,
  CloudUpload,
  Library,
  ListMusic,
  LogIn,
  LogOut,
  PencilRuler,
  Sparkles,
  type LucideIcon,
} from "lucide-react";
import { BrandHeader } from "@/components/brand";
import { useClientVisit } from "./client-visit";

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
  href: Href;
  icon: LucideIcon;
  /** True when the route needs the admin token. */
  admin?: boolean;
};

/**
 * Staff entries grouped by the operator's task (issue #742).
 *
 * The rail used to list monitoring pages before the production flow, so the
 * most frequent path — 登録 → 編集 → 公開 — sat below the fold of attention.
 * The groups now mirror the workflow: 制作 (register, edit, publish, in that
 * order) comes first, 運営 (dashboard, archive) second.
 */
export interface StudioNavGroup {
  value: string;
  /** The Japanese group label. */
  ja: string;
  en: string;
  items: readonly StudioNavItem[];
}

const NAV_NEW_SONG: StudioNavItem = {
  value: "new-song",
  label: "New song",
  ja: "新曲を登録",
  en: "New song",
  href: "/new-song",
  icon: Sparkles,
};

const NAV_EDITOR: StudioNavItem = {
  value: "editor",
  label: "New chart",
  ja: "譜面を編集",
  en: "Edit chart",
  href: "/chart",
  icon: PencilRuler,
};

const NAV_PUBLISH: StudioNavItem = {
  value: "publish",
  label: "Publish",
  ja: "公開する",
  en: "Publish",
  href: "/publish",
  icon: CloudUpload,
  admin: true,
};

const NAV_DASHBOARD: StudioNavItem = {
  value: "dashboard",
  label: "Dashboard",
  ja: "ダッシュボード",
  en: "Dashboard",
  href: "/dashboard",
  icon: ChartColumnBig,
};

const NAV_SONGS: StudioNavItem = {
  value: "songs",
  label: "Songs",
  ja: "楽曲アーカイブ",
  en: "Songs",
  href: "/songs",
  icon: Archive,
};

/**
 * Per-account entries (issue #800): the user's own songs and the account
 * page. These are NOT staff operations — they sit in their own group so
 * the 制作/運営 split of the staff rail stays untouched.
 */
export const NAV_USER_GROUP: StudioNavGroup = {
  value: "account",
  ja: "マイページ",
  en: "Account",
  items: [
    {
      value: "my-songs",
      label: "My songs",
      ja: "マイソング",
      en: "My songs",
      href: "/my-songs",
      icon: Library,
    },
    {
      value: "account",
      label: "Account",
      ja: "アカウント",
      en: "Account",
      href: "/login",
      icon: LogIn,
    },
  ],
};

export const NAV_STAFF_GROUPS: readonly StudioNavGroup[] = [
  {
    value: "produce",
    ja: "制作",
    en: "Produce",
    items: [NAV_NEW_SONG, NAV_EDITOR, NAV_PUBLISH],
  },
  {
    value: "operate",
    ja: "運営",
    en: "Operate",
    items: [NAV_DASHBOARD, NAV_SONGS],
  },
];

/**
 * The flat list of every staff entry, in display order (issue #742).
 * Kept for callers that want the whole set in one array.
 */
export const NAV_STAFF_ITEMS: readonly StudioNavItem[] = NAV_STAFF_GROUPS.flatMap(
  (group) => group.items,
);

/**
 * The flat, icon-first list of every route of the studio. The rail renders
 * `NAV_VISITOR` + `NAV_STAFF_ITEMS`; this export keeps the flat form for
 * callers that want the whole set in one array (issue #151).
 */
export const NAV_ITEMS: readonly StudioNavItem[] = [
  {
    value: "catalog",
    label: "Catalog",
    ja: NAV_VISITOR.ja,
    en: NAV_VISITOR.en,
    href: NAV_VISITOR.href,
    icon: ListMusic,
  },
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

/**
 * One task group of the staff entries (issue #742). `display: contents`
 * keeps the label and the entries as direct items of the staff grid, so
 * the narrow 2-column layout does not gain a nested box.
 */
const staffSubgroup = css({
  display: "contents",
});

/**
 * The 制作/運営 subheading. Unlike `groupLabel` it stays visible on narrow
 * viewports: inside the 2-column grid it spans the full row (`1 / -1`), so
 * the boundary between the production flow and the operations pages reads
 * at a glance.
 */
const subgroupLabel = css({
  color: "fg.subtle",
  fontSize: "2xs",
  gridColumn: { base: "1 / -1", md: "auto" },
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

/** The per-account group sits between the visitor entry and the staff rail. */
const userGroup = css({
  display: "contents",
});

const accountRow = css({
  alignItems: "center",
  borderTopColor: "gray.surface.border",
  borderTopWidth: { base: "0px", md: "1px" },
  color: "fg.subtle",
  display: "flex",
  fontSize: "xs",
  gap: "2",
  justifyContent: "space-between",
  marginTop: "auto",
  minWidth: "0",
  paddingInline: "3",
  paddingTop: "3",
  width: "100%",
});

const accountName = css({
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "nowrap",
});

const accountButton = css({
  alignItems: "center",
  background: "transparent",
  border: "none",
  color: "fg.muted",
  cursor: "pointer",
  display: "inline-flex",
  flexShrink: "0",
  fontSize: "xs",
  gap: "1",
  padding: "1",
  _hover: { color: "fg.default" },
  _focusVisible: {
    outline: "2px solid",
    outlineColor: "colorPalette.solid.bg",
    outlineOffset: "1px",
  },
});

const labelGrid = css({
  display: "grid",
  lineHeight: "1.2",
  minWidth: "0",
  textAlign: "left",
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

export function Sidebar() {
  // Narrow viewports keep the staff entries in a disclosure (issue #74).
  const [staffOpen, setStaffOpen] = useState(false);
  // Issue #800: the account widget reflects the Better Auth session. The
  // hook stays pending until `/api/auth/get-session` answers; until then
  // the row renders nothing rather than flashing a signed-out state.
  const session = auth.useSession();
  const visit = useClientVisit();
  const accountUser = session.data?.user ?? null;

  async function onSignOut() {
    await auth.signOut();
    visit("/");
  }

  return (
    <aside className={sidebar}>
      <div className={brand}>
        <BrandHeader />
      </div>

      <nav aria-label="Studio" className={navList}>
        <div className={navTopRow}>
          {/* Issue #801: Void's Link marks the current page itself
              (`aria-current`/`data-void-active`) and navigates without a
              document reload, so the rail no longer tracks `location`. */}
          <Link
            href={NAV_VISITOR.href}
            prefetch
            className={`${navTrigger} ${visitorTrigger}`}
            data-testid="nav-visitor"
          >
            <ListMusic className={navIcon} size={16} aria-hidden="true" />
            <span className={labelGrid}>
              <span>{NAV_VISITOR.ja}</span>
            </span>
          </Link>

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
            </span>
            <ChevronDown className={staffToggleIcon} size={16} aria-hidden="true" />
          </button>
        </div>

        {/* Issue #800: the per-account entries are always visible — unlike
            the staff disclosure they are the general user's path. */}
        <div className={userGroup} data-testid="nav-user-group">
          <div className={staffDivider} aria-hidden="true" />
          <p className={groupLabel}>{NAV_USER_GROUP.ja}</p>
          {NAV_USER_GROUP.items.map((item) => (
            <Link
              key={item.value}
              href={item.href}
              prefetch
              className={navTrigger}
              data-testid={`nav-${item.value}`}
            >
              <item.icon className={navIcon} size={16} aria-hidden="true" />
              <span className={labelGrid}>
                <span>{item.ja}</span>
              </span>
            </Link>
          ))}
        </div>

        <div
          id="studio-staff-nav"
          data-staff-nav=""
          data-open={staffOpen ? "true" : "false"}
          className={staffGroup}
        >
          <div className={staffDivider} aria-hidden="true" />
          <p className={groupLabel}>スタッフ操作</p>

          {NAV_STAFF_GROUPS.map((group) => (
            <div key={group.value} className={staffSubgroup}>
              {/* 制作/運営の小見出し。狭い画面でも全幅の行として残し、
                  2列グリッドの中でグループの切れ目が分かるようにする。 */}
              <p className={subgroupLabel}>{group.ja}</p>
              {group.items.map((item) => (
                <Link
                  key={item.value}
                  href={item.href}
                  prefetch
                  className={navTrigger}
                  data-testid={`nav-${item.value}`}
                >
                  <item.icon className={navIcon} size={16} aria-hidden="true" />
                  <span className={labelGrid}>
                    <span>{item.ja}</span>
                  </span>
                  {"admin" in item && item.admin ? (
                    <span className={adminBadge} data-testid="nav-publish-admin-badge">
                      要トークン
                    </span>
                  ) : null}
                </Link>
              ))}
            </div>
          ))}
        </div>

        {/* Issue #800: account state of the Better Auth session. Nothing
            renders while the session is pending so a signed-in user never
            sees a signed-out flash. */}
        {!session.isPending ? (
          <div className={accountRow} data-testid="account-widget">
            {accountUser ? (
              <>
                <span className={accountName} title={accountUser.email ?? accountUser.name}>
                  {accountUser.name || accountUser.email}
                </span>
                <button
                  type="button"
                  className={accountButton}
                  onClick={() => void onSignOut()}
                  data-testid="account-sign-out"
                >
                  <LogOut size={12} aria-hidden="true" />
                  ログアウト
                </button>
              </>
            ) : (
              <>
                <span className={accountName}>未ログイン</span>
                <Link className={accountButton} href="/login" data-testid="account-sign-in">
                  <LogIn size={12} aria-hidden="true" />
                  ログイン
                </Link>
              </>
            )}
          </div>
        ) : null}
      </nav>
    </aside>
  );
}
