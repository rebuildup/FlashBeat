# Spec: 106 — v5 UI 基盤 (UISizeProfile + デザイントークン + 共通 uGUI ビルダ)

Issue: #106 (Refs #88 #96 #100 #41) — branch `106`, base `release-0-1-3`.

## 目的

design/flashbeat-ui.pen **v5** (Design System v5 / Components v5) と
design/ui-size-profiles.json を Unity C# に写し、Title/Select/Game/Result/Option の
各画面実装が其の上に載る共通 UI 基盤を `Assets/Game/Scripts/UI/V5/` に作る。
新規ファイルのみ、namespace `FlashBeat.UI.V5`。

## 正本

- design/flashbeat-ui.pen v5 (暗号化、pen CLI 経由でのみ値を取得)
- design/ui-size-profiles.json (`profiles` fhd/2k/mac, `select` 規則)
- design/exports/*.png は本 worktree では LFS ポインタのみ (実体なし) —
  値はすべて pen CLI `GetVariables()` と json から取得

## 成果物

1. `UISizeProfile.cs` — json の profiles を不変 C# クラスへ。
   `UISizeProfile.Current` が Screen.width/height から選択:
   height>=1440 → 2k / |w/h-1.6|<0.05 → mac / else fhd。
   fhd 未満の高さ (1024x768, 1280x720) は fhd 値を min(1, h/1080) 倍に縮小
   (Constant Pixel Size x1 でクリップさせないため。mac は 900px 高で設計済みなので縮小不要)。
   `CanvasConfigurator` (MonoBehaviour) が Canvas/CanvasScaler を
   Constant Pixel Size x1 に設定し、解像度変化で再適用。
2. `V5Tokens.cs` — Design System v5 の色/フォントサイズ/角丸を static readonly 定数化
   (pen CLI `Print(GetVariables())` の実値)。
3. `V5Ui.cs` — コードビルダ: RectTransform パネル、TMP テキスト
   (フォントは TMP_Settings.defaultFontAsset / 既存 TMP から取得 —
   プロジェクトの既定フォントは NotoSansJP-Medium SDF)、
   実行時生成の角丸 9-slice スプライト背景 (border 付きで伸縮しても角が潰れない)、
   KeyCap / Tag / ButtonPrimary / ButtonSecondary / SongRow / RankRow、
   マージン付きエッジ相対アンカーヘルパ。公開 API 一覧をファイル先頭コメントに記載。
4. `Assets/Tests/Editor/V5UiTests.cs` — EditMode NUnit:
   プロファイル選択 (1920x1080 / 2560x1440 / 1440x900 / 1024x768 / 1280x720)、
   アンカーヘルパの期待値。

## 非目標

- 画面別実装 (後続 Issue)、Prefab 化、play mode テスト、Cloudflare/本番への反映。
