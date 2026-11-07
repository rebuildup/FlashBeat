import { css } from "styled-system/css";
import { BrandHeader } from "@/components/brand";

export const NAV_ITEMS = [
  { value: "catalog", label: "Catalog", href: "/" },
  { value: "editor", label: "New chart", href: "/chart" },
  { value: "publish", label: "Publish", href: "/publish" },
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

export function Sidebar() {
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
            aria-current={item.value === "catalog" ? "page" : undefined}
            className={navTrigger}
          >
            {item.label}
          </a>
        ))}
      </nav>
    </aside>
  );
}
