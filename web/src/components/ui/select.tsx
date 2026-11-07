"use client";
import type { Assign, SelectRootProps } from "@ark-ui/react";
import { ark } from "@ark-ui/react/factory";
import { Select, useSelectItemContext } from "@ark-ui/react/select";
import { CheckIcon, ChevronsUpDownIcon } from "lucide-react";
import { forwardRef, type RefAttributes } from "react";
import { createSlotRecipeContext } from "styled-system/jsx";
import { css } from "styled-system/css";
import { type SelectVariantProps, select } from "styled-system/recipes";
import type { HTMLStyledProps } from "styled-system/types";

const { withProvider, withContext } = createSlotRecipeContext(select);

type StyleProps = SelectVariantProps & HTMLStyledProps<"div">;

export type RootProps<T> = Assign<SelectRootProps<T>, StyleProps> & RefAttributes<HTMLDivElement>;

export const Root = withProvider(Select.Root, "root") as Select.RootComponent<StyleProps>;

export const ClearTrigger = withContext(Select.ClearTrigger, "clearTrigger");
export const Content = withContext(Select.Content, "content");

/**
 * Ark UI はクリックとキーボード (Enter / Escape / 矢印) を `Select.Trigger`
 * (role="combobox" の button) にだけ実装している。`Control` は見た目の器で
 * しかないため、Trigger を描画しない呼出しは何も起きない (issue #163 の
 * 「dropdown が使えない」の根本原因)。
 *
 * そこで `Control` を composite にして、children を自動で bare な Trigger
 * で包む。既存・将来の `<Select.Control>` 呼出しは JSX を変えずに操作可能に
 * なり、呼出し側の API は壊れない。見た目 (背景・高さ・余白) は従来どおり
 * Control 側の className が担い、Trigger 自身の recipe スタイルは打ち消す。
 */
const controlTrigger = css({
  alignItems: "inherit",
  appearance: "none",
  background: "transparent",
  border: "0",
  borderRadius: "inherit",
  color: "inherit",
  cursor: "inherit",
  display: "flex",
  flex: "1",
  font: "inherit",
  gap: "inherit",
  height: "full",
  justifyContent: "inherit",
  minWidth: "0",
  outline: "0",
  padding: "0",
  textAlign: "inherit",
  width: "full",
});

const StyledControl = withContext(Select.Control, "control");
const StyledTrigger = withContext(Select.Trigger, "trigger");

export type ControlProps = HTMLStyledProps<"div"> & RefAttributes<HTMLDivElement>;

export const Control = forwardRef<HTMLDivElement, ControlProps>(function Control(props, ref) {
  const { children, ...restProps } = props;
  return (
    <StyledControl ref={ref} {...restProps}>
      <StyledTrigger className={controlTrigger}>{children}</StyledTrigger>
    </StyledControl>
  );
});

export const IndicatorGroup = withContext(ark.div, "indicatorGroup");
export const Item = withContext(Select.Item, "item");
export const ItemGroup = withContext(Select.ItemGroup, "itemGroup");
export const ItemGroupLabel = withContext(Select.ItemGroupLabel, "itemGroupLabel");
export const ItemText = withContext(Select.ItemText, "itemText");
export const Label = withContext(Select.Label, "label");
export const List = withContext(Select.List, "list");
export const Positioner = withContext(Select.Positioner, "positioner");
export const Trigger = withContext(Select.Trigger, "trigger");
export const ValueText = withContext(Select.ValueText, "valueText");
export const Indicator = withContext(Select.Indicator, "indicator", {
  defaultProps: { children: <ChevronsUpDownIcon /> },
});
export const HiddenSelect = Select.HiddenSelect;

export {
  SelectContext as Context,
  SelectItemContext as ItemContext,
  type SelectValueChangeDetails as ValueChangeDetails,
} from "@ark-ui/react/select";

const StyledItemIndicator = withContext(Select.ItemIndicator, "itemIndicator");

export const ItemIndicator = forwardRef<HTMLDivElement, HTMLStyledProps<"div">>(
  function ItemIndicator(props, ref) {
    const item = useSelectItemContext();

    return item.selected ? (
      <StyledItemIndicator ref={ref} {...props}>
        <CheckIcon />
      </StyledItemIndicator>
    ) : (
      <svg aria-hidden="true" focusable="false" />
    );
  },
);
