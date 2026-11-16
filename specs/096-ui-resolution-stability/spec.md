# Spec: #96 低解像度で主要操作が失われない v6 UI

Status: implementation required (2026-11-16). Supersedes the 2026-11-10 "no new code, hardware screenshots only" acceptance approach. Parent: #250. Development quality: [development-convergence](../../docs/operations/development-convergence.md).

## Problem / evidence

PR #521 (main 138f35b2) の `AutoTestV6Screens.AuditScreenClip` で実測。1920×1080 / 2560×1440 / 2560×1664 では clip 0、1024×768 / 1280×720 では固定 1728×972 フレームの負 origin によって Header/Footer が画面外に失われる。Select の Ready/Search/Filter、Result の Retry/Register/OK、Pause の Resume/Retry/Back などはポインタ操作不能。単に画面の観測が不足しているのではなく**製品の実不具合**。現在の screen audit が report-only であることを PASS と読まない。

## Product contract

- 対応範囲: 1024×768, 1280×720, 1366×768, 1600×900, 1920×1080, 2560×1440, 2560×1664。ユーザーの解像度選択を要求しない。
- 主導線: 各画面の実操作 (Title/Option/Result/Open; 関連 #88 は Select/Game/Pause) を keyboard と pointer の両経路で到達可能にする。決定/戻る/リトライ/選曲/登録、検索・フィルタの情報と文字列の可読性を残す。キー代替があるだけでは隠れた pointer 操作や情報の欠落を許容しない。
- 大解像度: design 座標の 1728×972、scale=1、Pencil v6 の見た目と動作を維持する。
- 小解像度: scale=1 の視覚資産を原則とし、**レスポンシブな再配置・スクロール導線等のレイアウト設計で画面外喪失を解消**する。縮尺による全画面押し込みは初期解としない。最終方式は画面ごとに実装テストと UI 契約へ反映する。全ての frame をそのまま表示し切る必要はないが、主要操作と情報は失われてはいけない。
- Opening は V5/ConstantPixelSize の別系統。v6 固定フレームの欠陥を Opening に機械的に当てはめない。

## Ownership / no overlap

#96 は Title/Result/Option/Opening と共通幾何学・テストの調整を所有。Select/Game/Pause 固有の UI は #88 / 該当実装 Issue へ帰属させ、共通 API の変更を排他調整する。既存の判定、メディア、HOLD、スコアや API は変更しない。

## Scenarios / acceptance

1. Given 各 supported viewport, When 各 v6 画面を開いて Header/Footer の主要コントロール・文字情報を監査, Then 必要な操作と情報は viewport 内に表示またはスクロール等で操作可能に到達できる（`Outside` の必須要素 0）。clip の report-only 総数 0 だけで誤って PASS しない。
2. Given 1024×768 / 1280×720, When pointer で Ready/Retry/OK/Back/検索等の主導線を操作, Then input→handler→state→feedback→exit が有効で、キーボードでも同等動作することをテストで確認。
3. Given FHD/2K/Mac, When 旧デザインを再描画, Then frame origin, design metrics, hit targets, 既存テキストは回帰しない。
4. Given scrolling/masking and modal states, When screen clipping を分類, Then スクロールで到達できる要素は誤検知せず、完全に外の致命的な要素は fail する。Mask/Scroll/Canvas の親子順にテストを置く。
5. Existing C# compile + relevant EditMode regression は成功し、現在の省力的 CI gate も PASS。スクリーンショット・実物 Windows 操作・Mac 擬似解像度による物理実機判定は**実装完了要件にしない**。未実機観測は release 時の補足記録としてのみ区別する。

## Non-goals

最小解像度を引き上げて当初の 1024×768 対応要求を削除すること、キーボード専用への縮退、全 Unity Player を各 Issue で起動して撮影すること、大規模 UI 再設計、スコア判定変更、本番 API 書き込み。
