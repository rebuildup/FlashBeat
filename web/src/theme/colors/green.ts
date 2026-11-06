import { defineSemanticTokens } from "@pandacss/dev";

/**
 * green palette anchored to Pen v5 (ok #4CC38A / ok-light #8FE0B8).
 * Generated for issue #60; steps 1-8 are hue-consistent interpolations,
 * steps 9-12 are the Pen anchors recorded in docs/design/web-token-mapping.md.
 * Regenerate with the script in that doc, not by hand.
 */
export const green = defineSemanticTokens.colors({
  "1": { value: { _light: "#F8FAF9", _dark: "#0D1411" } },
  "2": { value: { _light: "#F1F6F4", _dark: "#101B16" } },
  "3": { value: { _light: "#E4F1EB", _dark: "#13271E" } },
  "4": { value: { _light: "#D3EEE1", _dark: "#153727" } },
  "5": { value: { _light: "#BBEDD4", _dark: "#164B31" } },
  "6": { value: { _light: "#99EDC4", _dark: "#17633E" } },
  "7": { value: { _light: "#71EFB2", _dark: "#17824E" } },
  "8": { value: { _light: "#3FF39C", _dark: "#15A861" } },
  "9": { value: { _light: "#00EB79", _dark: "#4CC38A" } },
  "10": { value: { _light: "#00D66F", _dark: "#63CB99" } },
  "11": { value: { _light: "#0AB864", _dark: "#8FE0B8" } },
  "12": { value: { _light: "#064B2A", _dark: "#9EFACD" } },
  a1: { value: { _light: "rgb(76, 195, 138, 0.020)", _dark: "rgb(76, 195, 138, 0.016)" } },
  a2: { value: { _light: "rgb(76, 195, 138, 0.047)", _dark: "rgb(76, 195, 138, 0.027)" } },
  a3: { value: { _light: "rgb(76, 195, 138, 0.086)", _dark: "rgb(76, 195, 138, 0.055)" } },
  a4: { value: { _light: "rgb(76, 195, 138, 0.122)", _dark: "rgb(76, 195, 138, 0.090)" } },
  a5: { value: { _light: "rgb(76, 195, 138, 0.161)", _dark: "rgb(76, 195, 138, 0.122)" } },
  a6: { value: { _light: "rgb(76, 195, 138, 0.200)", _dark: "rgb(76, 195, 138, 0.161)" } },
  a7: { value: { _light: "rgb(76, 195, 138, 0.259)", _dark: "rgb(76, 195, 138, 0.220)" } },
  a8: { value: { _light: "rgb(76, 195, 138, 0.341)", _dark: "rgb(76, 195, 138, 0.302)" } },
  a9: { value: { _light: "rgb(76, 195, 138, 0.400)", _dark: "rgb(76, 195, 138, 0.400)" } },
  a10: { value: { _light: "rgb(76, 195, 138, 0.451)", _dark: "rgb(76, 195, 138, 0.451)" } },
  a11: { value: { _light: "rgb(76, 195, 138, 0.522)", _dark: "rgb(76, 195, 138, 0.522)" } },
  a12: { value: { _light: "rgb(76, 195, 138, 0.580)", _dark: "rgb(76, 195, 138, 0.580)" } },
  solid: {
    bg: {
      DEFAULT: { value: { _light: "{colors.green.9}", _dark: "{colors.green.9}" } },
      hover: { value: { _light: "{colors.green.10}", _dark: "{colors.green.10}" } },
    },
    fg: { DEFAULT: { value: { _light: "white", _dark: "#131313" } } },
  },
  subtle: {
    bg: {
      DEFAULT: { value: { _light: "{colors.green.a3}", _dark: "{colors.green.a3}" } },
      hover: { value: { _light: "{colors.green.a4}", _dark: "{colors.green.a4}" } },
      active: { value: { _light: "{colors.green.a5}", _dark: "{colors.green.a5}" } },
    },
    fg: { DEFAULT: { value: { _light: "{colors.green.11}", _dark: "{colors.green.11}" } } },
  },
  surface: {
    bg: {
      DEFAULT: { value: { _light: "{colors.green.a2}", _dark: "{colors.green.a2}" } },
      active: { value: { _light: "{colors.green.a3}", _dark: "{colors.green.a3}" } },
    },
    border: {
      DEFAULT: { value: { _light: "{colors.green.a6}", _dark: "{colors.green.a6}" } },
      hover: { value: { _light: "{colors.green.a7}", _dark: "{colors.green.a7}" } },
    },
    fg: { DEFAULT: { value: { _light: "{colors.green.11}", _dark: "{colors.green.11}" } } },
  },
  outline: {
    bg: {
      hover: { value: { _light: "{colors.green.a2}", _dark: "{colors.green.a2}" } },
      active: { value: { _light: "{colors.green.a3}", _dark: "{colors.green.a3}" } },
    },
    border: { DEFAULT: { value: { _light: "{colors.green.a7}", _dark: "{colors.green.a7}" } } },
    fg: { DEFAULT: { value: { _light: "{colors.green.11}", _dark: "{colors.green.11}" } } },
  },
  plain: {
    bg: {
      hover: { value: { _light: "{colors.green.a3}", _dark: "{colors.green.a3}" } },
      active: { value: { _light: "{colors.green.a4}", _dark: "{colors.green.a4}" } },
    },
    fg: { DEFAULT: { value: { _light: "{colors.green.11}", _dark: "{colors.green.11}" } } },
  },
});
