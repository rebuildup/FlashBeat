# HUD の日本語フォント欠落を直す (Issue #27)

- Issue: [#27](https://github.com/rebuildup/FlashBeat/issues/27) — *[Win] HUD の日本語フォント欠落(曲名・アーティスト名が□表示)*
- Branch: `27` (worktree `C:\Users\rebui\Desktop\FlashBeat.27`) → base `release-0-1-0`
- Unity: `6000.5.6f1` (`C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe`)
- 日付: 2026-11-06

## 1. 再現 (Win64 ベースライン #11 の所見)

`#11` のベースラインで select/result 画面の曲名・アーティスト名が `□` になり、player log に

```
The character with Unicode value \u30ED was not found in the [LiberationSans SDF] font asset
```

が出力されていた。

## 2. 原因

原因は 2 つ重なっていた。

### 2.1 HUD が `LiberationSans SDF` を参照していた

`Assets/Game/Scenes/*.unity` と `Assets/Game/Prefabs/*.prefab` の TextMeshPro は
`m_fontAsset: {guid: ca2f53e234e514e45b9247abc9728e6d}` を参照していたが、
この GUID はリポジトリ内に存在しない (削除済みアセットの残骸)。
TMP は解決できないフォント参照を TMP Settings の既定フォント
(`LiberationSans SDF` = `8f586378b4e144a9851e7b34d9b748ee`) で描画するため、
日本語グリフを持たない LiberationSans が使われ `□` になっていた。

### 2.2 `NotoSansJP-Medium SDF` 自体も日本語をほぼ含んでいなかった

`Assets/Game/Fonts/Japanese/NotoSansJP-Medium SDF.asset` は
`japanese_full.txt` (7150 文字) を文字集合に指定して生成されているが、
作成時の設定が point size 71 / padding 0 / atlas 4096x4096 であり、
**アトラスが 620 文字で埋まって以降が取り込まれなかった**。
実測でアセットの `m_CharacterTable` は 620 エントリしかなく、
曲名/歌詞で使う漢字 (例: `舞` `葉` `蘭` `逃` `避` `部` `生` `田` `世` `吽` `夜` `嵐` `曇` `柊` `沌` `焉` `輔`) が欠落していた。
`m_SourceFontFile` も `{fileID: 0}` で壊れており、既存アセットに文字を追加する手段も無かった。

## 3. 対策

新規のフォント基盤は導入せず、既存の `NotoSansJP-Medium.ttf` と既存の
`JapaneseFontFixer` を拡張して使う。

### 3.1 `Assets/Editor/JapaneseFontAssetBuilder.cs` (新規)

同じ TTF から SDF アセットを静的に再生成する Editor tool。

- 文字集合 = `japanese_full.txt` の非漢字 (ASCII / 記号 / かな / 全角) + ゲームが出す漢字
  (曲名・アーティスト名・歌詞 = `GManager.SongName` / `GManager.Musician` / 各 chart の `lyrics.mondai` / `lyrics.furigana`、
  シーン/プレハブの `m_text`)。
- 動的モードで生成 → `TryAddCharacters` → 保存時に静的アセット (`m_AtlasPopulationMode: 0`) に戻す
  (`TMP_FontAsset.TryAddCharacters` は静的アセットへの追加を拒否するため)。
- point size 48 / padding 5 / atlas 4096x4096 (1 アトラスに収まる)。
- 出力は既存パス (`Assets/Game/Fonts/Japanese/NotoSansJP-Medium SDF.asset`) へ **meta を保ったまま** 書き換えるため、
  GUID と既存の参照は変わらない。
- ゲームが必須の文字 (曲名/アーティスト名/歌詞) が 1 文字でも欠けていれば例外で停止する。

再生成結果: 文字集合 1402 / 必須 762 / 実際に取り込まれたグリフ **1382**
(旧アセットは 620)。`m_SourceFontFile` も TTF へ正しく張れた。

### 3.2 `Assets/Editor/JapaneseFontFixer.cs` (拡張)

- `ApplyJapaneseFontToProject()` を追加 (Editor メニュー `Tools → FlashBeat → Apply Japanese Font To Scenes And Prefabs`、
  バッチ `-executeMethod JapaneseFontFixer.ApplyJapaneseFontToProject`)。
- ビルド対象シーン + `Assets/Game/Scenes` 配下のシーン + `Assets/Game/Prefabs` 配下のプレハブを走査し、
  `LiberationSans SDF` / 解決不能参照の TextMeshPro を日本語フォントへ差し替える (マテリアルも追従)。
- TMP Settings の既定フォント (`m_defaultFontAsset`) も日本語フォントへ変更し、
  解決できない参照を今後 `□` に戻さないようにする。

適用結果: **181 コンポーネント更新** (Title 7 / Select 82 / Game 17 / Result 30 / Option 25 + プレハブ 19 + TMP 既定フォント 1)。

### 3.3 `.gitattributes`

LFS 上書きルールが旧パス (`Assets/NotoSansJP-Medium SDF.asset`) を指していたため、
現パス (`Assets/Game/Fonts/Japanese/...`) へ修正した。

## 4. 検証

### 4.1 ビルド (Win64)

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe' `
    -batchmode -quit `
    -projectPath 'C:\Users\rebui\Desktop\FlashBeat.27' `
    -executeMethod BuildScript.BuildStandaloneWindows64 `
    -logFile 'C:\tmp\unity_build_27.log'
```

```
[BuildScript] Build succeeded: 1588334450 bytes, output to ...\Builds\Windows\FlashBeat.exe
```

コンパイル診断: **0 `error CS`** (`build-log-excerpt.txt`)。

### 4.2 実行 (built player) — select / game / result

`#11` と同じ合成キーボード入力 driver (`docs/evidence/win64-2026-11-06/drive-gameplay.ps1`) で
ビルド済み player を操作し、各画面をスクリーンショットで押さえた。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\docs\evidence\win64-2026-11-06\drive-gameplay.ps1 `
    -Exe 'C:\Users\rebui\Desktop\FlashBeat.27\Builds\Windows\FlashBeat.exe' -OutDir 'C:\tmp\fb_font'
```

| 画面 | 証跡 | 描画結果 |
|---|---|---|
| Select | `01-select-song-titles.png` | `ロストアンブレラ` / `ニセモノ` / `まにまに` / `稲葉曇` が正しく表示 (旧: `□`) |
| Select (No.2 へ移動) | `02-select-song2.png` | `まにまに` が正しく表示 |
| Game | `03-game-lyrics.png` | 歌詞 `誰もがそう思うでしょう` が正しく表示 (旧: `□`) |
| Result | `04-result-song-title-artist.png` | `まにまに` / `r-906` が正しく表示 (旧: `□`) |

譜面 `まにまに` (BPM 174) を最後まで再生し、Result 画面まで到達している
(`Judge: Perfect 0 / Great 0 / Bad 0 / Miss 76` = 譜面 76 notes 全部、assessment `C`)。
未入力の自動実行なので全て Miss は #11 ベースラインと同じ条件。

**player log の比較** (`Player-font-run.log`):

| | `not found in the [...] font asset` |
|---|---|
| #11 ベースライン (`Player-run3-manimani.log`) | **110 件** (`LiberationSans SDF`) |
| 本 PR のビルド | **0 件** |

### 4.3 EditMode テスト

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe' -batchmode `
    -projectPath 'C:\Users\rebui\Desktop\FlashBeat.27' `
    -runTests -testPlatform editmode -testResults 'C:\tmp\TestResults27.xml' `
    -logFile 'C:\tmp\unity_tests27.log'
```

```
<test-run id="2" testcasecount="40" result="Passed" total="40" passed="40" failed="0" ... />
```

既存 36 件 + 追加した `JapaneseFontCoverageTests` 4 件 = **40/40 passed**。

追加したテスト (回帰ガード):

| テスト | 守ること |
|---|---|
| `JapaneseFontAsset_CoversEverySongTitleArtistAndLyric` | SDF アセットが曲名・アーティスト名・歌詞の全ての文字を含む (アトラスが溢れて `□` に戻るのを防ぐ) |
| `TmpDefaultFont_IsTheJapaneseFont` | TMP の既定フォントが日本語フォント (解決不能参照が `□` に戻らない) |
| `GamePrefabs_UseTheJapaneseFont` | ゲーム用プレハブの TextMeshPro が日本語フォント |
| `BuildScenes_UseTheJapaneseFont` | ビルド対象シーンの TextMeshPro が日本語フォント |

テストが空振りしていないことは別途確認済み:
`LiberationSans SDF` は `まにまに` の 4 文字すべてを持たない (→ 旧状態ならテストは失敗する)、
現アセットでも未収録の文字 (`𠮷`) は検出される、
ビルド対象シーンで日本語フォント以外を使っている TextMeshPro は **0 個**。

## 5. 残件 / 注意点

- SDF アセットは静的アトラスなので、**新しい漢字を含む曲/歌詞を追加したら**
  `Tools → FlashBeat → Rebuild NotoSansJP SDF Asset` (または
  `-executeMethod JapaneseFontAssetBuilder.RebuildNotoSansJpSdf`) を実行し、
  `Tools → FlashBeat → Apply Japanese Font To Scenes And Prefabs` で割り当て直すこと。
  文字が足りていない場合は `JapaneseFontCoverageTests` が失敗する。
- `NotoSansJP-Medium TTF` に存在しない記号 20 個 (`‗‛‟‪‫‬‭‮‴‾⁄⁞⁪⁫⁬⁭⁮⁯≒`) は
  アトラスに含まれない (ゲームは使用しない)。ログに warning として残る。
- シーン/プレハブは Unity 6000.5.6f1 の TMP で再シリアライズされている
  (`m_enableWordWrapping` → `m_TextWrappingMode` 等)。値は同一。
- `TMP Settings` の `m_ActiveFontFeatures` は `00000000` → `6e72656b` (kerning 有効) に変わった。
  Editor が設定アセットを保存した際の差分。
- 本 Issue は `#13` (yt-dlp ストリーミング) の後に Windows/Unity 担当が実施する想定だったが、
  `#13` は別 worktree (`FlashBeat.13`) で進行中のため本ブランチは `1c5137e` (=#11 の HEAD) を起点に実施した。
  本 PR はシーン/プレハブ/フォントアセットのみを触っており、`#13` が触る部分
  (`GameScene.unity` の `YoutubePlayer` 配線) とは重複しない。
- 本 Issue とは無関係に、Unity 6000.5.6f1 でプロジェクトを開くと
  `ProjectSettings.asset` の `scriptingDefineSymbols` に `SENTIS_ANALYTICS_ENABLED` が
  追加される (Sentis パッケージ由来)。本 PR ではその差分を revert している。

