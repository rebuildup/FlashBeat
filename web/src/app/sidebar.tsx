import { Tabs } from "@/components/ui";
import { css } from "styled-system/css";

export const NAV_ITEMS = [
  { value: "catalog", label: "Catalog" },
  { value: "editor", label: "Chart editor" },
  { value: "publish", label: "Publish" },
] as const;

const sidebar = css({
  bg: "gray.surface.bg",
  borderColor: "gray.surface.border",
  borderRightWidth: "1px",
  display: "flex",
  flexDirection: "column",
  gap: "4",
  minHeight: "0",
  paddingBottom: "4",
  paddingTop: "4",
  px: "3",
});

const brand = css({
  alignItems: "center",
  display: "flex",
  gap: "2",
  paddingInline: "2",
});

const brandMark = css({
  alignItems: "center",
  bg: "colorPalette.solid.bg",
  borderRadius: "l1",
  color: "colorPalette.solid.fg",
  display: "flex",
  fontSize: "2xs",
  fontWeight: "bold",
  height: "6",
  justifyContent: "center",
  width: "6",
});

const brandName = css({
  color: "fg.default",
  fontSize: "xs",
  fontWeight: "semibold",
  letterSpacing: "0.01em",
  lineHeight: "1.2",
});

const brandSubtitle = css({
  color: "fg.subtle",
  fontSize: "2xs",
  lineHeight: "1.2",
});

const navList = css({
  display: "flex",
  flexDirection: "column",
  flex: "1",
  gap: "1",
});

const navTrigger = css({
  justifyContent: "flex-start",
  textAlign: "left",
  width: "100%",
});

const sidebarFooter = css({
  borderTopColor: "gray.surface.border",
  borderTopWidth: "1px",
  color: "fg.subtle",
  display: "flex",
  flexDirection: "column",
  fontSize: "2xs",
  gap: "1",
  paddingInline: "2",
  paddingTop: "3",
});

export function Sidebar() {
  return (
    <aside className={sidebar}>
      <div className={brand}>
        <span className={brandMark}>FB</span>
        <span>
          <span className={brandName}>FlashBeat</span>
          <br />
          <span className={brandSubtitle}>Web Studio</span>
        </span>
      </div>

      <Tabs.List className={navList}>
        {NAV_ITEMS.map((item) => (
          <Tabs.Trigger key={item.value} value={item.value} className={navTrigger}>
            {item.label}
          </Tabs.Trigger>
        ))}
      </Tabs.List>

      <div className={sidebarFooter}>
        <span>Stage S3 · Web Studio</span>
        <span>Contract openapi v1</span>
      </div>
    </aside>
  );
}
