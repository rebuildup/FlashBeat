import { Portal } from "@ark-ui/react/portal";
import { createListCollection } from "@ark-ui/react/collection";
import { Field, Input, Select } from "@/components/ui";
import { css } from "styled-system/css";
import { SORT_OPTIONS, type SortKey } from "./catalog-view";

const bar = css({
  alignItems: "flex-end",
  borderBottomColor: "gray.surface.border",
  borderBottomWidth: "1px",
  display: "flex",
  flexWrap: "wrap",
  gap: "3",
  justifyContent: "space-between",
  paddingBottom: "3",
  paddingTop: "3",
  px: "4",
});

const heading = css({
  display: "flex",
  flexDirection: "column",
  gap: "1",
  minWidth: "0",
});

const titleText = css({
  color: "fg.default",
  /** The catalog heading is a page title — same step as `PageHeader` (issue #802). */
  textStyle: "pageTitle",
  margin: "0",
});

const subtitleText = css({
  color: "fg.muted",
  /** Shared secondary-text step (issue #802). */
  textStyle: "rowMeta",
  margin: "0",
});

const tools = css({
  alignItems: "flex-end",
  display: "flex",
  flexWrap: "wrap",
  gap: "2",
});

const toolField = css({
  minWidth: "12rem",
  maxWidth: "24rem",
  flex: "1 1 auto",
});

const selectRoot = css({
  /** Definite basis so Sort/Level pack into one row (issue #166).
   * Issue #802 review: on narrow viewports the selects must grow into the
   * wrapped row instead of staying 8rem — a fixed basis squeezed the level
   * value down to "すべて…". The 9rem basis keeps "すべてのレベル" whole:
   * at 8rem the value ellipsized even on desktop. */
  flex: { base: "1 1 9rem", md: "0 0 9rem" },
  minWidth: "9rem",
  flexShrink: 0,
});

const selectControl = css({
  alignItems: "center",
  bg: "transparent",
  borderColor: "gray.outline.border",
  borderRadius: "l2",
  borderWidth: "1px",
  cursor: "pointer",
  display: "flex",
  gap: "2",
  height: "sizes.10",
  justifyContent: "space-between",
  // Issue #802 review: keep the value text inside the control on narrow
  // widths (ellipsis instead of clipping mid-glyph).
  minWidth: "0",
  overflow: "hidden",
  paddingInline: "3",
  textAlign: "left",
  width: "full",
  _focusVisible: {
    outline: "2px solid",
    outlineColor: "colorPalette.solid.bg",
    outlineOffset: "1px",
  },
});

/** "Search" is the `/` shortcut target (catalog keyboard model, issue #70). */
export const SEARCH_INPUT_ID = "catalog-search";

const sortCollection = createListCollection({
  items: SORT_OPTIONS.map((option) => ({ ...option })),
});

export type LevelFilter = "all" | "low" | "mid" | "high";

export interface LevelFilterItem {
  label: string;
  value: LevelFilter;
}

export const LEVEL_FILTERS: LevelFilterItem[] = [
  { label: "すべてのレベル", value: "all" },
  { label: "レベル 1–9", value: "low" },
  { label: "レベル 10–19", value: "mid" },
  { label: "レベル 20+", value: "high" },
];

const levelCollection = createListCollection<LevelFilterItem>({ items: LEVEL_FILTERS });

export function Topbar({
  title,
  subtitle,
  query,
  onQueryChange,
  level,
  onLevelChange,
  sort,
  onSortChange,
}: {
  title: string;
  subtitle: string;
  query: string;
  onQueryChange: (value: string) => void;
  level: LevelFilter;
  onLevelChange: (value: LevelFilter) => void;
  sort: SortKey;
  onSortChange: (value: SortKey) => void;
}) {
  return (
    <header className={bar}>
      <div className={heading}>
        <h1 className={titleText}>{title}</h1>
        <p className={subtitleText}>{subtitle}</p>
      </div>

      <div className={tools}>
        <Field.Root className={toolField}>
          <Field.Label>検索</Field.Label>
          <Input
            id={SEARCH_INPUT_ID}
            placeholder="曲名またはアーティスト"
            value={query}
            onChange={(event) => onQueryChange(event.currentTarget.value)}
          />
        </Field.Root>

        <Select.Root
          className={selectRoot}
          collection={sortCollection}
          value={[sort]}
          onValueChange={(details) => {
            const [next] = details.value;
            if (next) onSortChange(next as SortKey);
          }}
        >
          <Select.Label>並び替え</Select.Label>
          <Select.Control className={selectControl}>
            <Select.ValueText placeholder="カタログ" />
            <Select.Indicator />
          </Select.Control>
          <Portal>
            <Select.Positioner>
              <Select.Content>
                <Select.List>
                  {SORT_OPTIONS.map((option) => (
                    <Select.Item key={option.value} item={option}>
                      <Select.ItemText>{option.label}</Select.ItemText>
                      <Select.ItemIndicator />
                    </Select.Item>
                  ))}
                </Select.List>
              </Select.Content>
            </Select.Positioner>
          </Portal>
        </Select.Root>

        <Select.Root
          className={selectRoot}
          collection={levelCollection}
          value={[level]}
          onValueChange={(details) => {
            const [next] = details.value;
            if (next) onLevelChange(next as LevelFilter);
          }}
        >
          <Select.Label>レベル</Select.Label>
          <Select.Control className={selectControl}>
            <Select.ValueText placeholder="すべてのレベル" />
            <Select.Indicator />
          </Select.Control>
          <Portal>
            <Select.Positioner>
              <Select.Content>
                <Select.List>
                  {LEVEL_FILTERS.map((filter) => (
                    <Select.Item key={filter.value} item={filter}>
                      <Select.ItemText>{filter.label}</Select.ItemText>
                      <Select.ItemIndicator />
                    </Select.Item>
                  ))}
                </Select.List>
              </Select.Content>
            </Select.Positioner>
          </Portal>
        </Select.Root>
      </div>
    </header>
  );
}
