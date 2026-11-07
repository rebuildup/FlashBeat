import { css } from "styled-system/css";

/**
 * Brand assets of the Web Studio (issue #67).
 *
 * The mark is the real Unity app icon (`Assets/Game/Art/Images/flashIcon.png`,
 * LFS oid 377707ea…, 1340×1340 RGBA) resized into `web/public/flashbeat-icon-*.png`.
 * No placeholder tile is rendered anywhere; if the image is missing it must
 * surface as a broken image, not fall back to an invented badge.
 *
 * Contract for navigation owners (#69 sidebar): import `BrandHeader` and render
 * it as the first child of the brand slot. `size` selects the asset variant;
 * the component never recolors or restyles the mark.
 */

export const BRAND_ICON_SIZES = [32, 64, 180, 192, 512] as const;

export type BrandIconSize = (typeof BRAND_ICON_SIZES)[number];

export function brandIconSrc(size: BrandIconSize): string {
  return `/flashbeat-icon-${size}.png`;
}

const brandHeader = css({
  alignItems: "center",
  display: "flex",
  gap: "2",
  minWidth: "0",
});

const brandImage = css({
  borderRadius: "l2",
  display: "block",
  flexShrink: "0",
  height: "8",
  width: "8",
});

const brandName = css({
  color: "fg.default",
  fontSize: "sm",
  fontWeight: "semibold",
  lineHeight: "1.2",
});

const brandSubtitle = css({
  color: "fg.muted",
  fontSize: "xs",
  lineHeight: "1.2",
});

export function BrandHeader({
  subtitle = "Web Studio",
}: {
  subtitle?: string;
}) {
  return (
    <span className={brandHeader}>
      <img
        alt="FlashBeat"
        className={brandImage}
        height={32}
        src={brandIconSrc(64)}
        width={32}
      />
      <span className={css({ minWidth: "0" })}>
        <span className={brandName}>FlashBeat</span>
        <br />
        <span className={brandSubtitle}>{subtitle}</span>
      </span>
    </span>
  );
}
