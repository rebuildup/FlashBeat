import { defineSemanticTokens } from "@pandacss/dev";

/**
 * red palette anchored to Pen v5 (danger #FF6B6B / danger-light #FFA8A8).
 * Generated for issue #60; steps 1-8 are hue-consistent interpolations,
 * steps 9-12 are the Pen anchors recorded in docs/design/web-token-mapping.md.
 * Regenerate with the script in that doc, not by hand.
 */
export const red = defineSemanticTokens.colors({
  "1": { value: { _light: "#FAF8F8", _dark: "#140D0D" } },
  "2": { value: { _light: "#F6F1F1", _dark: "#1B1010" } },
  "3": { value: { _light: "#F1E4E4", _dark: "#271313" } },
  "4": { value: { _light: "#EED3D3", _dark: "#371515" } },
  "5": { value: { _light: "#EDBBBB", _dark: "#4B1616" } },
  "6": { value: { _light: "#ED9999", _dark: "#631717" } },
  "7": { value: { _light: "#EF7171", _dark: "#821717" } },
  "8": { value: { _light: "#F33F3F", _dark: "#A81515" } },
  "9": { value: { _light: "#EB0000", _dark: "#FF6B6B" } },
  "10": { value: { _light: "#D60000", _dark: "#FF8A8A" } },
  "11": { value: { _light: "#B80A0A", _dark: "#FFA8A8" } },
  "12": { value: { _light: "#4B0606", _dark: "#FDD3D3" } },
  a1: { value: { _light: "rgb(255, 107, 107, 0.020)", _dark: "rgb(255, 107, 107, 0.016)" } },
  a2: { value: { _light: "rgb(255, 107, 107, 0.047)", _dark: "rgb(255, 107, 107, 0.027)" } },
  a3: { value: { _light: "rgb(255, 107, 107, 0.086)", _dark: "rgb(255, 107, 107, 0.055)" } },
  a4: { value: { _light: "rgb(255, 107, 107, 0.122)", _dark: "rgb(255, 107, 107, 0.090)" } },
  a5: { value: { _light: "rgb(255, 107, 107, 0.161)", _dark: "rgb(255, 107, 107, 0.122)" } },
  a6: { value: { _light: "rgb(255, 107, 107, 0.200)", _dark: "rgb(255, 107, 107, 0.161)" } },
  a7: { value: { _light: "rgb(255, 107, 107, 0.259)", _dark: "rgb(255, 107, 107, 0.220)" } },
  a8: { value: { _light: "rgb(255, 107, 107, 0.341)", _dark: "rgb(255, 107, 107, 0.302)" } },
  a9: { value: { _light: "rgb(255, 107, 107, 0.400)", _dark: "rgb(255, 107, 107, 0.400)" } },
  a10: { value: { _light: "rgb(255, 107, 107, 0.451)", _dark: "rgb(255, 107, 107, 0.451)" } },
  a11: { value: { _light: "rgb(255, 107, 107, 0.522)", _dark: "rgb(255, 107, 107, 0.522)" } },
  a12: { value: { _light: "rgb(255, 107, 107, 0.580)", _dark: "rgb(255, 107, 107, 0.580)" } },
  solid: {
    bg: {
      DEFAULT: { value: { _light: "{colors.red.9}", _dark: "{colors.red.9}" } },
      hover: { value: { _light: "{colors.red.10}", _dark: "{colors.red.10}" } },
    },
    fg: { DEFAULT: { value: { _light: "white", _dark: "#131313" } } },
  },
  subtle: {
    bg: {
      DEFAULT: { value: { _light: "{colors.red.a3}", _dark: "{colors.red.a3}" } },
      hover: { value: { _light: "{colors.red.a4}", _dark: "{colors.red.a4}" } },
      active: { value: { _light: "{colors.red.a5}", _dark: "{colors.red.a5}" } },
    },
    fg: { DEFAULT: { value: { _light: "{colors.red.11}", _dark: "{colors.red.11}" } } },
  },
  surface: {
    bg: {
      DEFAULT: { value: { _light: "{colors.red.a2}", _dark: "{colors.red.a2}" } },
      active: { value: { _light: "{colors.red.a3}", _dark: "{colors.red.a3}" } },
    },
    border: {
      DEFAULT: { value: { _light: "{colors.red.a6}", _dark: "{colors.red.a6}" } },
      hover: { value: { _light: "{colors.red.a7}", _dark: "{colors.red.a7}" } },
    },
    fg: { DEFAULT: { value: { _light: "{colors.red.11}", _dark: "{colors.red.11}" } } },
  },
  outline: {
    bg: {
      hover: { value: { _light: "{colors.red.a2}", _dark: "{colors.red.a2}" } },
      active: { value: { _light: "{colors.red.a3}", _dark: "{colors.red.a3}" } },
    },
    border: { DEFAULT: { value: { _light: "{colors.red.a7}", _dark: "{colors.red.a7}" } } },
    fg: { DEFAULT: { value: { _light: "{colors.red.11}", _dark: "{colors.red.11}" } } },
  },
  plain: {
    bg: {
      hover: { value: { _light: "{colors.red.a3}", _dark: "{colors.red.a3}" } },
      active: { value: { _light: "{colors.red.a4}", _dark: "{colors.red.a4}" } },
    },
    fg: { DEFAULT: { value: { _light: "{colors.red.11}", _dark: "{colors.red.11}" } } },
  },
});
