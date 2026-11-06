import { absoluteCenter } from "./absolute-center";
import { spinner } from "./spinner";
import { icon } from "./icon";
import { group } from "./group";
import { toast } from "./toast";
import { select } from "./select";
import { dialog } from "./dialog";
import { tabs } from "./tabs";
import { field } from "./field";
import { input } from "./input";
import { button } from "./button";

/**
 * `@park-ui/cli add` writes every recipe into `recipes`, including the ones
 * declared with `defineSlotRecipe`. Slot recipes must live in `slotRecipes`,
 * otherwise Panda emits them as plain recipes and the per-slot styles
 * (`root`, `list`, `trigger`, ...) are silently dropped.
 */
export const recipes = {
  button,
  input,
  group,
  icon,
  spinner,
  absoluteCenter,
};

export const slotRecipes = {
  tabs,
  dialog,
  field,
  select,
  toast,
};
