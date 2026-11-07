import { useEffect, useState } from "react";
import { css } from "styled-system/css";
import {
  Archive,
  ChartColumnBig,
  CloudUpload,
  ListMusic,
  PencilRuler,
  Sparkles,
  type LucideIcon,
} from "lucide-react";
import { BrandHeader } from "@/components/brand";

/**
 * The navigation of the studio.
 *
 * Every entry carries an icon: a text-only rail is hard to scan, and the
 * icons are the same in every page of the studio.
 */
export const NAV_ITEMS: readonly {
  value: string;
  label: string;
  href: string;
  icon: LucideIcon;
}[] = [
  { value: "catalog", label: "Catalog", href: "/", icon: ListMusic },
  { value: "dashboard", label: "Dashboard", href: "/dashboard", icon: ChartColumnBig },
  { value: "songs", label: "Songs", href: "/songs", icon: Archive },
  { value: "new-song", label: "New song", href: "/new-song", icon: Sparkles },
  { value: "editor", label: "New chart", href: "/chart", icon: PencilRuler },
  { value: "publish", label: "Publish", href: "/publish", icon: CloudUpload },
];

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

const navTrigger = css({
  display: "flex",
  minHeight: "10",
  alignItems: "center",
  columnGap: "2",
  px: "3",
  borderRadius: "l2",
  color: "fg.muted",
  textDecoration: "none",
  whiteSpace: "nowrap",
  _hover: { bg: "#262626", color: "fg.default" },
  _focusVisible: { outline: "2px solid #3FA9F5", outlineOffset: "2px" },
  "&[aria-current=page]": { bg: "#2B4457", color: "#9AD3FA" },
  justifyContent: "flex-start",
  textAlign: "left",
  width: { base: "auto", md: "100%" },
});

/** The icon of a navigation entry. */
const navIcon = css({
  color: "currentColor",
  flexShrink: 0,
});

export function Sidebar({ active }: { active?: string }) {
  // The active entry follows the URL; the default covers the server-rendered
  // first paint of `/` (the catalog). A page can pin it with `active`.
  const [path, setPath] = useState("/");
  useEffect(() => {
    setPath(window.location.pathname);
  }, []);

  const isActive = (item: (typeof NAV_ITEMS)[number]): boolean => {
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
        {NAV_ITEMS.map((item) => (
          <a
            key={item.value}
            href={item.href}
            aria-current={isActive(item) ? "page" : undefined}
            className={navTrigger}
          >
            <item.icon className={navIcon} size={16} aria-hidden="true" />
            {item.label}
          </a>
        ))}
      </nav>
    </aside>
  );
}
