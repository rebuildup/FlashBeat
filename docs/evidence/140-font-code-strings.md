# Issue #140 — コード内文字列の字形カバレッジ検証

対象: Issue #140「日本語の □ (豆腐) を根絶: コード内文字列を文字セットに追加 + 動的フォールバックフォント」

本ドキュメントは、`Assets/Tests/Editor/JapaneseFontCodeStringCoverageTests.cs` が
**何を抽出して / どう判定するか** を記録する。実装 (テスト) が無い状態だった部分を埋める 것이本次の目的。

## 1. 対象文字列の抽出方法

### 対象範囲

`Assets/Game/Scripts` 配下のすべての `.cs` ファイル（`Directory.GetFiles(..., "*.cs", SearchOption.AllDirectories)`）。
`JapaneseFontAssetBuilder.CollectCodeTexts()` と同じ範囲に揃えている。

### なぜ「全文」ではなく「文字列リテラル」か

`CollectCodeTexts()` は `.cs` の**全文**をそのままアトラス生成の入力にしているため、
日本語を含む**変数名・識別子・コメント**までアトラスに含まれてしまう。
テスト側で同じ「全文」を要求すると、

- コメントや識別子は画面に一切出ない（＝豆腐の原因にならない）ので、偽陽性になる
- その結果「本来 1 文字も要らない文字が欠けている」だけでテストが落ちる

という問題がある。**偽陽性を優先して避ける** ため、
テストは「**日本語を含む文字列リテラルの中身**」だけを対象にする。

### 字句走査（実装: `ExtractStringLiterals`）

C# のソースを 1 文字ずつ走査し、次の 4 種類を区別する。

| 対象 | 扱い |
|---|---|
| 行コメント `// ...` | 読み飛ばす（終端は改行） |
| ブロックコメント `/* ... */` | 読み飛ばす（終端は `*/`、複数行可） |
| 文字リテラル `'x'` | 読み飛ばす（`\'` エスケープ考慮） |
| 文字列リテラル | **中身を採用** |

文字列リテラルは 3 形式に対応する。

- 通常: `"..."` — `\` エスケープを復号する。`\uXXXX` / `\xXX` は実際の文字に復元する。
- verbatim: `@"..."` — `""` が 1 個の `"` を表す。`\` はエスケープしない。
- 補間: `$"..."` / `$@"..."` — 中身の `{}` **ホール（式）** は読み飛ばす。
  ホールの中はコードであり、リテラルではないため（変数名・式を誤検出しないため）。
  ホール内の入れ子文字列はスキップ平衡の `{}` を数えて end-to-end で読み飛ばす。

最後に、抽出したリテラル文字列の中から **日本語レンジ** の文字だけを採用する。

| レンジ | 内容 |
|---|---|
| `U+3000–U+30FF` | CJK 記号・ひらがな・カタカナ |
| `U+3400–U+4DBF` | CJK 拡張 A |
| `U+4E00–U+9FFF` | CJK 統合漢字 |
| `U+F900–U+FAFF` | CJK 互換漢字 |
| `U+FF00–U+FFEF` | 全角・全角英数字 |
| `U+20000–U+2FFFF` | CJK 拡張 B 以降 |

### 抽出結果の実測（Unity Editor なしで再現）

抽出行列を .NET 8 の単体プログラムに切り出して `Assets/Game/Scripts` に対して実行した結果:

```
distinct jp chars: 372
```

別実装（独立に書いた Python 字句走査）でも **372 文字** で一致した（相互検証済み）。

## 2. 判定基準

### 主アトラス側

`Assets/Game/Fonts/Japanese/NotoSansJP-Medium SDF.asset` は `AtlasPopulationMode.Static` なので、
収録済みの文字は `TMP_FontAsset.HasCharacters(string, out List<char>)`
（＝ `m_CharacterLookupDictionary` 参照）で**厳密に**判定できる。
主アトラスに無い文字は `missing` に入る。

### 動的フォールバック側

`Assets/Game/Fonts/Japanese/NotoSansJP-Dynamic SDF.asset` は `AtlasPopulationMode.Dynamic`
（`m_AtlasPopulationMode: 1`）であり、`m_CharacterTable: []` —— **アトラスには文字が入っていない**。
したがって `HasCharacters` は常に false を返し、主アセットの `HasCharacters` だけでは
「動的で賄える」ことを判定できない。

そこで **前提条件と生成可否を分けて** 判定する。

**(a) 前提条件（テスト `CodeStringLiterals_AreCoveredByMainAtlasOrDynamicFallback` の先頭で検証）**

1. 動的フォールバックアセットが存在する
2. `atlasPopulationMode == AtlasPopulationMode.Dynamic`
3. `sourceFontFile != null`（ラスタライズの素となるフォントファイルがある）

**(b) 生成可否（主アトラスに無い文字のみ）**

上記 (a) が成立するなら、主アトラスに無い文字は
**実行時に動的フォールバックで生成される** とみなす。
ただし「生成できるか」自体は主アセットでは判定できないため、
**同じ `sourceFontFile` から一時的な動的 `TMP_FontAsset` を作って `TryAddCharacters` し、
実際に生成できることを確認する**。生成できなかった文字は「主アトラスにも動的フォールバックにも無い」
= 豆腐になる のでテストを失敗させる。

一時アセットは `Object.DestroyImmediate` で破棄し、リポジトリのアセットは変更しない。
一時アセットが作れない環境では `Font.HasCharacter` にフォールバックする。

**(c) 登録の確認（テスト `DynamicFallback_IsRegisteredOnTheMainFontAsset`）**

動的フォールバックは主アセットの `fallbackFontAssetTable` に登録されていないと
実際の描画で参照されない。登録されていることを別テストで検証する。

### 判定ロジックのまとめ

```
各文字 c (抽出した日本語):
  主アトラスに含まれる        → OK (静的アトラスで描画される)
  含まれない かつ 動的条件成立 → 動的フォールバックで実行時に生成される → OK
  含まれない かつ 動的条件不成立 → FAIL (豆腐になる)
```

## 3. 結果

### 追加したテスト

| テスト名 | 内容 |
|---|---|
| `CodeStringLiterals_AreCoveredByMainAtlasOrDynamicFallback` | 抽出した 372 文字が主アトラス or 動的フォールバックでレンダリング可能であること |
| `DynamicFallback_IsRegisteredOnTheMainFontAsset` | 動的フォールバックが主アセットの `fallbackFontAssetTable` に登録されていること |

### 実測結果

- 抽出された日本語文字: **372 文字**（文字列リテラルのみ）
- 主アトラスが収録する文字: **1756 文字**（`m_CharacterTable` の distinct codepoint 数）
- 抽出した 372 文字のうち、主アトラスに無いもの: **0 文字**

すなわち現状は **372 文字すべてが主アトラスに収録済み** で、
動的フォールバックは「将来新しい漢字が曲名に入った場合」の保険として機能する
（このテストの経路では 実際には動的生成は必要とされていない）。

### 主アトラスの glyph 実測

`Assets/Game/Fonts/Japanese/NotoSansJP-Medium SDF.asset` は Git LFS 管理のため、
**Unity Editor を起動せずにアセット YAML を直接パース**して計測した。

| 項目 | 値 |
|---|---|
| `m_CharacterTable` エントリ数 | 1756 |
| distinct Unicode codepoint 数 | **1756** |
| `m_GlyphTable` エントリ数 (glyph) | 1753 |
| `m_AtlasPopulationMode` | 0 (Static) |

620 文字だった頃からの比較: commit `673f2d2` のメッセージに
「未収録 7150 文字 → 1756 文字 / 必要 762 glyph 全部収録」と記録があり、
現状の 1756 文字はこの再生成の結果。**620 ではなく 1756 文字**。

なお Unity Editor 上からの `TMP_FontAsset` 実測（`font.characterLookupTable.Count` 相当）は
**未実施**（Editor は Supervisor が排他で使用するため、本作业では起動していない）。
ここに出した 1756 / 1753 はアセット YAML の直接パース値であり、
TMP の `characterLookupTable` が持つ codepoint 数と一致する設計（YAML がその出所）である。

### 未計測 / 未実施項目

| 項目 | 状態 |
|---|---|
| Unity Editor 上での `TMP_FontAsset.HasCharacters` による実測 | **未実施**（Editor 排他のため。Unity を起動していない） |
| 実機 Windows での豆腐表示の目視確認 | **未実施**（別 Issue の領域） |
| 動的フォールバックの実行時生成の証跡（スクリーンショット） | **未実施**（現状 372 文字すべて主アトラスにあるため、このテストでは発火しない） |

**注意**: 上記のテストは `Assets/Tests/Editor/JapaneseFontCoverageTests.cs` の既存 4 テスト
（`JapaneseFontAsset_CoversEverySongTitleArtistAndLyric` / `TmpDefaultFont_IsTheJapaneseFont` /
`GamePrefabs_UseTheJapaneseFont` / `BuildScenes_UseTheJapaneseFont`）とは
**独立したファイル・独立したアセット読み込み経路**を持ち、
既存テストの失敗原因（`BuildScenes_UseTheJapaneseFont` の 1 件 Records 失敗）に依存しない。

## 4. 関連コミット

- `0a96857` — `feat(font): regenerate NotoSansJP SDF with code strings + dynamic fallback asset (#140)`
  （`CollectCodeTexts()` / `EnsureDynamicFallback()` の実装）
- `673f2d2` — `fix(font): 日本語 SDF アトラスを再生成 (未収録 7150 文字 → 1756 文字 / 必要 762 glyph 全部収録)`
