# lucide アイコン (同梱物)

v6 UI のアイコン用文字 (⚙ ◀ ▶ ← ↑ ≡ など) を lucide の SVG アイコンへ置き換えるための
素材置き場。このディレクトリの SVG は **リポジトリに同梱** しており、ビルド・実行時に
外部 CDN を参照しない (オフラインで動作する)。

- 変換済みの PNG スプライト: `Assets/Game/Resources/Icons/<name>.png`
- 再生成スクリプト: `script/render-icons.mjs` (`bun run script/render-icons.mjs`)

## 取得元

- リポジトリ: https://github.com/lucide-icons/lucide
- 取得 URL 形式: `https://raw.githubusercontent.com/lucide-icons/lucide/<tag>/icons/<name>.svg`
- 取得したタグ: **0.577.0**
- 取得日: **2026-11-08**

### タグを 0.577.0 に固定した理由

lucide の `main` ブランチ (および 0.578.0 以降) では `history` と `music` の 2 アイコンが
**削除済み** であり、指定された名前 (`history` / `music`) のまま取得できない。
0.577.0 はこの 2 つを含む **最新** のリリースであり、23 アイコンすべてを
同じバージョンの artwork で揃えられるため、タグ固定で取得している。

## ライセンス

lucide は **ISC ライセンス** (MIT 互換) で配布されている。Feather 由来のアイコンについては
MIT ライセンス。原文を `LICENSE` に同梱している。

- Lucide 本体: ISC License — Copyright (c) for portions of Lucide are held by Cole Bemis
  2013-2026 as part of Feather (MIT). All other copyright (c) for Lucide are held by
  Lucide Contributors 2026.
- Feather 由来分: MIT License — Copyright (c) 2013-present Cole Bemis

## 改変内容

同梱している SVG 自体は **lucide の原本から一切変更していない** (1 バイトも編集していない)。

変更はスクリプト `script/render-icons.mjs` の中でのみ、PNG へ変換する直前に行っている。

1. `currentColor` → `#FFFFFF`
   lucide の SVG は `stroke="currentColor"` を持つため、そのままでは色が決まらない。
   レンダリング直前に白へ置換する。
2. `width` / `height` を `96` (px) に指定
   元 SVG は `width="24" height="24" viewBox="0 0 24 24"`。viewBox はそのままにして
   出力サイズだけ拡大することで、ストローク幅も同比 (2 → 8px) でスケールされる。
3. `stroke-width` を `2` に固定
   lucide 原本の指定値を維持する (単位は viewBox の 24 単位系)。
4. 背景は透明 (`fill="none"` のまま、PNG も RGBA のアルファ 0)。

PNG 化後、SVG は参照されない (PNG のみ Unity が読み込む)。
元 SVG は再生成・差し替え用に本ディレクトリに残している。

## 同梱アイコン一覧 (23 個)

`arrow-left`, `arrow-right`, `check`, `chevron-down`, `chevron-left`, `chevron-right`,
`chevron-up`, `clock`, `gauge`, `history`, `keyboard`, `list-filter`, `minus`, `music`,
`play`, `plus`, `search`, `settings`, `skip-back`, `skip-forward`, `star`, `trophy`, `x`

## 導入方法 (Unity)

`Assets/Game/Resources/Icons/` は `Resources` 配下なので、スクリプトから

```csharp
Sprite icon = Resources.Load<Sprite>("Icons/settings");
```

で読み込める。`.png.meta` は Sprite 設定 (`textureType: 8`, `spriteMode: 1`,
`alphaIsTransparency: 1`, `maxTextureSize: 256`) 済み。
