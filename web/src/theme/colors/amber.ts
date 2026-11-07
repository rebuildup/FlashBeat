import { defineSemanticTokens } from "@pandacss/dev";

/**
 * amber palette anchored to Pen v5 (warn #E9B949 / warn-light #F4D88A).
 * Generated for issue #60; steps 1-8 are hue-consistent interpolations,
 * steps 9-12 are the Pen anchors recorded in docs/design/web-token-mapping.md.
 * Regenerate with the script in that doc, not by hand.
 */
export const amber = defineSemanticTokens.colors({
  "1": { value: { _light: "#FAF9F8", _dark: "#14120D" } },
  "2": { value: { _light: "#F6F4F1", _dark: "#1B1810" } },
  "3": { value: { _light: "#F1EDE4", _dark: "#272213" } },
  "4": { value: { _light: "#EEE7D3", _dark: "#372E15" } },
  "5": { value: { _light: "#EDDFBB", _dark: "#4B3D16" } },
  "6": { value: { _light: "#EDD799", _dark: "#634F17" } },
  "7": { value: { _light: "#EFCD71", _dark: "#826517" } },
  "8": { value: { _light: "#F3C33F", _dark: "#A88115" } },
  "9": { value: { _light: "#EBAC00", _dark: "#E9B949" } },
  "10": { value: { _light: "#D69D00", _dark: "#ECC864" } },
  "11": { value: { _light: "#B88A0A", _dark: "#F4D88A" } },
  "12": { value: { _light: "#4B3906", _dark: "#FBE6AC" } },
  a1: { value: { _light: "rgb(233, 185, 73, 0.020)", _dark: "rgb(233, 185, 73, 0.016)" } },
  a2: { value: { _light: "rgb(233, 185, 73, 0.047)", _dark: "rgb(233, 185, 73, 0.027)" } },
  a3: { value: { _light: "rgb(233, 185, 73, 0.086)", _dark: "rgb(233, 185, 73, 0.055)" } },
  a4: { value: { _light: "rgb(233, 185, 73, 0.122)", _dark: "rgb(233, 185, 73, 0.090)" } },
  a5: { value: { _light: "rgb(233, 185, 73, 0.161)", _dark: "rgb(233, 185, 73, 0.122)" } },
  a6: { value: { _light: "rgb(233, 185, 73, 0.200)", _dark: "rgb(233, 185, 73, 0.161)" } },
  a7: { value: { _light: "rgb(233, 185, 73, 0.259)", _dark: "rgb(233, 185, 73, 0.220)" } },
  a8: { value: { _light: "rgb(233, 185, 73, 0.341)", _dark: "rgb(233, 185, 73, 0.302)" } },
  a9: { value: { _light: "rgb(233, 185, 73, 0.400)", _dark: "rgb(233, 185, 73, 0.400)" } },
  a10: { value: { _light: "rgb(233, 185, 73, 0.451)", _dark: "rgb(233, 185, 73, 0.451)" } },
  a11: { value: { _light: "rgb(233, 185, 73, 0.522)", _dark: "rgb(233, 185, 73, 0.522)" } },
  a12: { value: { _light: "rgb(233, 185, 73, 0.580)", _dark: "rgb(233, 185, 73, 0.580)" } },
  solid: {
    bg: {
      DEFAULT: { value: { _light: "{colors.amber.9}", _dark: "{colors.amber.9}" } },
      hover: { value: { _light: "{colors.amber.10}", _dark: "{colors.amber.10}" } },
    },
    fg: { DEFAULT: { value: { _light: "white", _dark: "#131313" } } },
  },
  subtle: {
    bg: {
      DEFAULT: { value: { _light: "{colors.amber.a3}", _dark: "{colors.amber.a3}" } },
      hover: { value: { _light: "{colors.amber.a4}", _dark: "{colors.amber.a4}" } },
      active: { value: { _light: "{colors.amber.a5}", _dark: "{colors.amber.a5}" } },
    },
    fg: { DEFAULT: { value: { _light: "{colors.amber.11}", _dark: "{colors.amber.11}" } } },
  },
  surface: {
    bg: {
      DEFAULT: { value: { _light: "{colors.amber.a2}", _dark: "{colors.amber.a2}" } },
      active: { value: { _light: "{colors.amber.a3}", _dark: "{colors.amber.a3}" } },
    },
    border: {
      DEFAULT: { value: { _light: "{colors.amber.a6}", _dark: "{colors.amber.a6}" } },
      hover: { value: { _light: "{colors.amber.a7}", _dark: "{colors.amber.a7}" } },
    },
    fg: { DEFAULT: { value: { _light: "{colors.amber.11}", _dark: "{colors.amber.11}" } } },
  },
  outline: {
    bg: {
      hover: { value: { _light: "{colors.amber.a2}", _dark: "{colors.amber.a2}" } },
      active: { value: { _light: "{colors.amber.a3}", _dark: "{colors.amber.a3}" } },
    },
    border: { DEFAULT: { value: { _light: "{colors.amber.a7}", _dark: "{colors.amber.a7}" } } },
    fg: { DEFAULT: { value: { _light: "{colors.amber.11}", _dark: "{colors.amber.11}" } } },
  },
  plain: {
    bg: {
      hover: { value: { _light: "{colors.amber.a3}", _dark: "{colors.amber.a3}" } },
      active: { value: { _light: "{colors.amber.a4}", _dark: "{colors.amber.a4}" } },
    },
    fg: { DEFAULT: { value: { _light: "{colors.amber.11}", _dark: "{colors.amber.11}" } } },
  },
});
