import { defineSemanticTokens } from "@pandacss/dev";

/**
 * blue palette anchored to Pen v5 (accent #3FA9F5 / accent-light #9AD3FA).
 * Generated for issue #60; steps 1-8 are hue-consistent interpolations,
 * steps 9-12 are the Pen anchors recorded in docs/design/web-token-mapping.md.
 * Regenerate with the script in that doc, not by hand.
 */
export const blue = defineSemanticTokens.colors({
  "1": { value: { _light: "#F8F9FA", _dark: "#0D1114" } },
  "2": { value: { _light: "#F1F4F6", _dark: "#10171B" } },
  "3": { value: { _light: "#E4ECF1", _dark: "#131F27" } },
  "4": { value: { _light: "#D3E3EE", _dark: "#152937" } },
  "5": { value: { _light: "#BBD8ED", _dark: "#16354B" } },
  "6": { value: { _light: "#99CAED", _dark: "#174463" } },
  "7": { value: { _light: "#71BAEF", _dark: "#175582" } },
  "8": { value: { _light: "#3FA8F3", _dark: "#156BA8" } },
  "9": { value: { _light: "#0089EB", _dark: "#3FA9F5" } },
  "10": { value: { _light: "#007DD6", _dark: "#5CB6F7" } },
  "11": { value: { _light: "#0A6FB8", _dark: "#9AD3FA" } },
  "12": { value: { _light: "#062F4B", _dark: "#C1E3FC" } },
  a1: { value: { _light: "rgb(63, 169, 245, 0.020)", _dark: "rgb(63, 169, 245, 0.016)" } },
  a2: { value: { _light: "rgb(63, 169, 245, 0.047)", _dark: "rgb(63, 169, 245, 0.027)" } },
  a3: { value: { _light: "rgb(63, 169, 245, 0.086)", _dark: "rgb(63, 169, 245, 0.055)" } },
  a4: { value: { _light: "rgb(63, 169, 245, 0.122)", _dark: "rgb(63, 169, 245, 0.090)" } },
  a5: { value: { _light: "rgb(63, 169, 245, 0.161)", _dark: "rgb(63, 169, 245, 0.122)" } },
  a6: { value: { _light: "rgb(63, 169, 245, 0.200)", _dark: "rgb(63, 169, 245, 0.161)" } },
  a7: { value: { _light: "rgb(63, 169, 245, 0.259)", _dark: "rgb(63, 169, 245, 0.220)" } },
  a8: { value: { _light: "rgb(63, 169, 245, 0.341)", _dark: "rgb(63, 169, 245, 0.302)" } },
  a9: { value: { _light: "rgb(63, 169, 245, 0.400)", _dark: "rgb(63, 169, 245, 0.400)" } },
  a10: { value: { _light: "rgb(63, 169, 245, 0.451)", _dark: "rgb(63, 169, 245, 0.451)" } },
  a11: { value: { _light: "rgb(63, 169, 245, 0.522)", _dark: "rgb(63, 169, 245, 0.522)" } },
  a12: { value: { _light: "rgb(63, 169, 245, 0.580)", _dark: "rgb(63, 169, 245, 0.580)" } },
  solid: {
    bg: {
      DEFAULT: { value: { _light: "{colors.blue.9}", _dark: "{colors.blue.9}" } },
      hover: { value: { _light: "{colors.blue.10}", _dark: "{colors.blue.10}" } },
    },
    fg: { DEFAULT: { value: { _light: "white", _dark: "#131313" } } },
  },
  subtle: {
    bg: {
      DEFAULT: { value: { _light: "{colors.blue.a3}", _dark: "{colors.blue.a3}" } },
      hover: { value: { _light: "{colors.blue.a4}", _dark: "{colors.blue.a4}" } },
      active: { value: { _light: "{colors.blue.a5}", _dark: "{colors.blue.a5}" } },
    },
    fg: { DEFAULT: { value: { _light: "{colors.blue.11}", _dark: "{colors.blue.11}" } } },
  },
  surface: {
    bg: {
      DEFAULT: { value: { _light: "{colors.blue.a2}", _dark: "{colors.blue.a2}" } },
      active: { value: { _light: "{colors.blue.a3}", _dark: "{colors.blue.a3}" } },
    },
    border: {
      DEFAULT: { value: { _light: "{colors.blue.a6}", _dark: "{colors.blue.a6}" } },
      hover: { value: { _light: "{colors.blue.a7}", _dark: "{colors.blue.a7}" } },
    },
    fg: { DEFAULT: { value: { _light: "{colors.blue.11}", _dark: "{colors.blue.11}" } } },
  },
  outline: {
    bg: {
      hover: { value: { _light: "{colors.blue.a2}", _dark: "{colors.blue.a2}" } },
      active: { value: { _light: "{colors.blue.a3}", _dark: "{colors.blue.a3}" } },
    },
    border: { DEFAULT: { value: { _light: "{colors.blue.a7}", _dark: "{colors.blue.a7}" } } },
    fg: { DEFAULT: { value: { _light: "{colors.blue.11}", _dark: "{colors.blue.11}" } } },
  },
  plain: {
    bg: {
      hover: { value: { _light: "{colors.blue.a3}", _dark: "{colors.blue.a3}" } },
      active: { value: { _light: "{colors.blue.a4}", _dark: "{colors.blue.a4}" } },
    },
    fg: { DEFAULT: { value: { _light: "{colors.blue.11}", _dark: "{colors.blue.11}" } } },
  },
});
