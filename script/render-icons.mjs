#!/usr/bin/env node
/**
 * script/render-icons.mjs
 *
 * lucide (ISC / MIT 互換) の SVG アイコンを Unity 用 PNG スプライトへ変換する。
 *
 *   入力: Assets/Game/Icons/lucide/*.svg  (リポジトリ同梱・オフラインで取得済み)
 *   出力: Assets/Game/Resources/Icons/<name>.png      (96x96 / 白ストローク / 背景透明)
 *         Assets/Game/Resources/Icons/<name>.png.meta (Sprite として取り込む TextureImporter 設定)
 *         Assets/Game/Icons/lucide/<name>.svg.meta    (SVGImporter 設定)
 *         Assets/Game/Icons{,/lucide}.meta, Assets/Game/Resources/Icons.meta (フォルダ)
 *         Assets/Game/Icons/lucide/{LICENSE,README.md}.meta
 *
 * 冪等性: 同じ SVG 入力からは常に同じ PNG / .meta が出力される。
 *   - PNG: libvips の PNG エンコーダは時刻情報を埋め込まないため決定論的。
 *   - GUID / spriteID: アセット名・パスから SHA-256 で導出するため再実行しても変わらない。
 *
 * 使い方:
 *   bun install                        (sharp を用意する。bun.lock で固定済み)
 *   bun run script/render-icons.mjs
 *   node script/render-icons.mjs      (Node.js 20+ / sharp をローカルインストール済み)
 */

import { createHash } from 'node:crypto';
import { readFile, readdir, writeFile, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const SVG_DIR = path.join(ROOT, 'Assets', 'Game', 'Icons', 'lucide');
const OUT_DIR = path.join(ROOT, 'Assets', 'Game', 'Resources', 'Icons');

/** 出力 PNG の辺長 (px)。Retina 相当の 2x 解像度。 */
const SIZE = 96;
/** ストローク色。UI は白のストロークを前提にしているため固定。 */
const STROKE_COLOR = '#FFFFFF';
/** ストローク幅。lucide 原本の指定 (viewBox 24 単位) をそのまま維持する。 */
const STROKE_WIDTH = 2;

/**
 * Unity が書き出す TextureImporter の .meta と同じ構造のテンプレート。
 * 既存アセット (Assets/Game/Art/UI/Icons/*.png.meta) の形式に準拠している。
 */
const META_TEMPLATE = `fileFormatVersion: 2
guid: {GUID}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 256
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 1
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 256
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 1
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 256
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 1
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: WebGL
    maxTextureSize: 256
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 1
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData: 
    physicsShape: []
    bones: []
    spriteID: {SPRITE_ID}
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;

/**
 * SVG をレンダリング用 SVG に正規化する。
 * - `currentColor` を白 (#FFFFFF) に置換する (lucide の SVG は `stroke="currentColor"` を含む)。
 * - 出力サイズに合わせて width / height を px で指定する (viewBox はそのままなので
 *   ストローク幅も同比でスケールされる)。
 * - stroke-width は lucide 原本の指定 (2) に固定する。
 */
function prepareSvg(source, size) {
  let svg = source.replace(/currentColor/g, STROKE_COLOR);

  const openTag = /<svg\b[^>]*>/s.exec(svg);
  if (!openTag) {
    throw new Error('SVG の <svg> タグが見つかりません');
  }

  let tag = openTag[0];
  const setAttr = (name, value) => {
    const re = new RegExp(`\\b${name}\\s*=\\s*"[^"]*"`);
    if (re.test(tag)) {
      tag = tag.replace(re, `${name}="${value}"`);
    } else {
      tag = tag.replace(/\s*\/?>$/, (end) => ` ${name}="${value}"${end}`);
    }
  };

  setAttr('width', String(size));
  setAttr('height', String(size));
  setAttr('stroke-width', String(STROKE_WIDTH));

  return svg.slice(0, openTag.index) + tag + svg.slice(openTag.index + openTag[0].length);
}

/**
 * フォルダ用 .meta (DefaultImporter)。
 */
const FOLDER_META_TEMPLATE = `fileFormatVersion: 2
guid: {GUID}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
`;

/**
 * テキストファイル (.md) 用 .meta (TextScriptImporter)。
 */
const TEXT_META_TEMPLATE = `fileFormatVersion: 2
guid: {GUID}
TextScriptImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;

/**
 * 拡張子なしファイル (LICENSE) 用 .meta (DefaultImporter)。
 */
const DEFAULT_META_TEMPLATE = `fileFormatVersion: 2
guid: {GUID}
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;

/**
 * SVG 用 .meta (ScriptedImporter / SVGImporter)。
 * Unity 6000.5.6f1 が書き出す形式そのまま。構造の基準は、同梱の
 * com.unity.modules.vectorgraphics / com.unity.shadergraph が持つサンプル .svg.meta。
 */
const SVG_META_TEMPLATE = `fileFormatVersion: 2
guid: {GUID}
ScriptedImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 2
  userData: 
  assetBundleName: 
  assetBundleVariant: 
  script: {fileID: 12408, guid: 0000000000000000e000000000000000, type: 0}
  svgType: 3
  texturedSpriteMeshType: 0
  svgPixelsPerUnit: 100
  gradientResolution: 64
  alignment: 0
  customPivot: {x: 0, y: 0}
  generatePhysicsShape: 0
  viewportOptions: 0
  preserveViewport: 0
  advancedMode: 0
  tessellationMode: 1
  predefinedResolutionIndex: 1
  targetResolution: 1080
  resolutionMultiplier: 1
  stepDistance: 10
  samplingStepDistance: 100
  maxCordDeviationEnabled: 0
  maxCordDeviation: 1
  maxTangentAngleEnabled: 0
  maxTangentAngle: 5
  keepTextureAspectRatio: 1
  textureSize: 256
  textureWidth: 256
  textureHeight: 256
  wrapMode: 0
  filterMode: 1
  sampleCount: 4
  preserveSVGImageAspect: 0
  useSVGPixelsPerUnit: 0
  spriteData:
    TessellationDetail: 0
    SpriteName: 
    SpritePivot: {x: 0, y: 0}
    SpriteAlignment: 0
    SpriteBorder: {x: 0, y: 0, z: 0, w: 0}
    SpriteRect:
      serializedVersion: 2
      x: 0
      y: 0
      width: 0
      height: 0
    SpriteID: 
    PhysicsOutlines: []
`;

/** アイコン名から GUID (32 桁 hex) を決定論的に導出する。 */
function deriveGuid(name) {
  return createHash('sha256').update(`flashbeat-icon:${name}`).digest('hex').slice(0, 32);
}

/**
 * アセットパスから GUID (32 桁 hex) を決定論的に導出する。
 * PNG 以外の .meta (フォルダ / SVG / テキスト) にも同じ規則を適用する。
 */
function derivePathGuid(assetPath) {
  return createHash('sha256').update(`flashbeat-asset:${assetPath}`).digest('hex').slice(0, 32);
}

/** アイコン名から Unity の spriteID (32 桁 hex) を決定論的に導出する。 */
function deriveSpriteId(name) {
  return createHash('sha256').update(`flashbeat-sprite:${name}`).digest('hex').slice(0, 32);
}

async function main() {
  const entries = (await readdir(SVG_DIR))
    .filter((file) => file.endsWith('.svg'))
    .sort();

  if (entries.length === 0) {
    throw new Error(`SVG が見つかりません: ${SVG_DIR}`);
  }

  await mkdir(OUT_DIR, { recursive: true });

  for (const file of entries) {
    const name = file.replace(/\.svg$/, '');
    const source = await readFile(path.join(SVG_DIR, file), 'utf8');
    const svg = prepareSvg(source, SIZE);

    const pngPath = path.join(OUT_DIR, `${name}.png`);
    await sharp(Buffer.from(svg), { density: 288 })
      .resize(SIZE, SIZE, { fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } })
      .png({ compressionLevel: 9, effort: 10, palette: false })
      .toFile(pngPath);

    const meta = META_TEMPLATE
      .replace('{GUID}', deriveGuid(name))
      .replace('{SPRITE_ID}', deriveSpriteId(name));
    await writeFile(`${pngPath}.meta`, meta, 'utf8');

    console.log(`rendered ${name}.png`);

    // SVG 原本の .meta (Unity の SVGImporter 設定)
    await writeFile(
      path.join(SVG_DIR, `${name}.svg.meta`),
      SVG_META_TEMPLATE.replace('{GUID}', derivePathGuid(`Assets/Game/Icons/lucide/${name}.svg`)),
      'utf8',
    );
  }

  // フォルダ / 添付文書の .meta
  const extraMetas = [
    ['Assets/Game/Icons.meta', FOLDER_META_TEMPLATE],
    ['Assets/Game/Icons/lucide.meta', FOLDER_META_TEMPLATE],
    ['Assets/Game/Resources/Icons.meta', FOLDER_META_TEMPLATE],
    ['Assets/Game/Icons/lucide/README.md.meta', TEXT_META_TEMPLATE],
    ['Assets/Game/Icons/lucide/LICENSE.meta', DEFAULT_META_TEMPLATE],
  ];
  for (const [assetPath, template] of extraMetas) {
    await writeFile(path.join(ROOT, assetPath), template.replace('{GUID}', derivePathGuid(assetPath)), 'utf8');
  }

  console.log(`${entries.length} 個のアイコンを生成しました -> ${path.relative(ROOT, OUT_DIR)}`);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
