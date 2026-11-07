import { defineSemanticTokens } from "@pandacss/dev";

/**
 * gray palette anchored to Pen v5 (Pen well/bg/base ramp + text/text-muted/text-dim).
 * Generated for issue #60; steps 1-8 are hue-consistent interpolations,
 * steps 9-12 are the Pen anchors recorded in docs/design/web-token-mapping.md.
 * Regenerate with the script in that doc, not by hand.
 */
export const gray = defineSemanticTokens.colors({
  "1": { value: { _light: "#FCFCFC", _dark: "#131313" } },
  "2": { value: { _light: "#F5F5F5", _dark: "#1B1B1B" } },
  "3": { value: { _light: "#EDEDED", _dark: "#202020" } },
  "4": { value: { _light: "#E3E3E3", _dark: "#262626" } },
  "5": { value: { _light: "#D6D6D6", _dark: "#2A2A2A" } },
  "6": { value: { _light: "#C4C4C4", _dark: "#2D2D2D" } },
  "7": { value: { _light: "#ABABAB", _dark: "#353535" } },
  "8": { value: { _light: "#8C8C8C", _dark: "#3E3E3E" } },
  "9": { value: { _light: "#737373", _dark: "#4A4A4A" } },
  "10": { value: { _light: "#5A5A5A", _dark: "#8C8C8C" } },
  "11": { value: { _light: "#3D3D3D", _dark: "#B0B0B0" } },
  "12": { value: { _light: "#1B1B1B", _dark: "#FFFFFF" } },
  a1: { value: { _light: "rgb(74, 74, 74, 0.020)", _dark: "rgb(74, 74, 74, 0.016)" } },
  a2: { value: { _light: "rgb(74, 74, 74, 0.047)", _dark: "rgb(74, 74, 74, 0.027)" } },
  a3: { value: { _light: "rgb(74, 74, 74, 0.086)", _dark: "rgb(74, 74, 74, 0.055)" } },
  a4: { value: { _light: "rgb(74, 74, 74, 0.122)", _dark: "rgb(74, 74, 74, 0.090)" } },
  a5: { value: { _light: "rgb(74, 74, 74, 0.161)", _dark: "rgb(74, 74, 74, 0.122)" } },
  a6: { value: { _light: "rgb(74, 74, 74, 0.200)", _dark: "rgb(74, 74, 74, 0.161)" } },
  a7: { value: { _light: "rgb(74, 74, 74, 0.259)", _dark: "rgb(74, 74, 74, 0.220)" } },
  a8: { value: { _light: "rgb(74, 74, 74, 0.341)", _dark: "rgb(74, 74, 74, 0.302)" } },
  a9: { value: { _light: "rgb(74, 74, 74, 0.400)", _dark: "rgb(74, 74, 74, 0.400)" } },
  a10: { value: { _light: "rgb(74, 74, 74, 0.451)", _dark: "rgb(74, 74, 74, 0.451)" } },
  a11: { value: { _light: "rgb(74, 74, 74, 0.522)", _dark: "rgb(74, 74, 74, 0.522)" } },
  a12: { value: { _light: "rgb(74, 74, 74, 0.580)", _dark: "rgb(74, 74, 74, 0.580)" } },
  solid: {
    bg: {
      DEFAULT: { value: { _light: "{colors.gray.9}", _dark: "{colors.gray.9}" } },
      hover: { value: { _light: "{colors.gray.10}", _dark: "{colors.gray.10}" } },
    },
    fg: { DEFAULT: { value: { _light: "white", _dark: "#131313" } } },
  },
  subtle: {
    bg: {
      DEFAULT: { value: { _light: "{colors.gray.a3}", _dark: "{colors.gray.a3}" } },
      hover: { value: { _light: "{colors.gray.a4}", _dark: "{colors.gray.a4}" } },
      active: { value: { _light: "{colors.gray.a5}", _dark: "{colors.gray.a5}" } },
    },
    fg: { DEFAULT: { value: { _light: "{colors.gray.11}", _dark: "{colors.gray.11}" } } },
  },
  surface: {
    bg: {
      DEFAULT: { value: { _light: "{colors.gray.a2}", _dark: "{colors.gray.a2}" } },
      active: { value: { _light: "{colors.gray.a3}", _dark: "{colors.gray.a3}" } },
    },
    border: {
      DEFAULT: { value: { _light: "{colors.gray.a6}", _dark: "{colors.gray.a6}" } },
      hover: { value: { _light: "{colors.gray.a7}", _dark: "{colors.gray.a7}" } },
    },
    fg: { DEFAULT: { value: { _light: "{colors.gray.11}", _dark: "{colors.gray.11}" } } },
  },
  outline: {
    bg: {
      hover: { value: { _light: "{colors.gray.a2}", _dark: "{colors.gray.a2}" } },
      active: { value: { _light: "{colors.gray.a3}", _dark: "{colors.gray.a3}" } },
    },
    border: { DEFAULT: { value: { _light: "{colors.gray.a7}", _dark: "{colors.gray.a7}" } } },
    fg: { DEFAULT: { value: { _light: "{colors.gray.11}", _dark: "{colors.gray.11}" } } },
  },
  plain: {
    bg: {
      hover: { value: { _light: "{colors.gray.a3}", _dark: "{colors.gray.a3}" } },
      active: { value: { _light: "{colors.gray.a4}", _dark: "{colors.gray.a4}" } },
    },
    fg: { DEFAULT: { value: { _light: "{colors.gray.11}", _dark: "{colors.gray.11}" } } },
  },
});
