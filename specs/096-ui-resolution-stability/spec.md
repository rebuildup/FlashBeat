# Spec: #96 低解像度で主要操作が失われない v6 UI

Status: implementation required (2026-11-16). Supersedes the 2026-11-10 "no new code, hardware screenshots only" acceptance approach. Parent: #250. Development quality: [development-convergence](../../docs/operations/development-convergence.md).

## Problem / evidence

PR #521 (main 138f35b2) の `AutoTestV6Screens.AuditScreenClip` で実測。1920×1080 / 2560×1440 / 2560×1664 では clip 0、1024×768 / 1280×720 では固定 1728×972 フレームの負 origin によって Header/Footer が画面外に失われる。Select の Ready/Search/Filter、Result の Retry/Register/OK、Pause の Resume/Retry/Back などはポインタ操作不能。単に画面の観測が不足しているのではなく**製品の実不具合**。現在の screen audit が report-only であることを PASS と読まない。

## Product contract

- 対応範囲: 1024×768, 1280×720, 1366×768, 1600×900, 1920×1080, 2560×1440, 2560×1664。ユーザーの解像度選択を要求しない。
- 主導線: 各画面の実操作 (Title/Option/Result/Opening; 関連 #523 は Select/Game/Pause) を keyboard と pointer の両経路で到達可能にする。決定/戻る/リトライ/選曲/登録、検索・フィルタの情報と文字列の可読性を残す。キー代替があるだけでは隠れた pointer 操作や情報の欠落を許容しない。
- 大解像度: design 座標の 1728×972、scale=1、Pencil v6 の見た目と動作を維持する。
- 小解像度: scale=1 の視覚資産を原則とし、**レスポンシブな再配置・スクロール導線等のレイアウト設計で画面外喪失を解消**する。縮尺による全画面押し込みは初期解としない。最終方式は画面ごとに実装テストと UI 契約へ反映する。全ての frame をそのまま表示し切る必要はないが、主要操作と情報は失われてはいけない。
- Opening は V5/ConstantPixelSize の別系統。v6 固定フレームの欠陥を Opening に機械的に当てはめない。

## Ownership / no overlap

#96 は Title/Result/Option/Opening と共通幾何学・テストの調整を所有。Select/Game/Pause 固有の UI は #523 へ帰属させ、共通 API の変更を排他調整する。既存の判定、メディア、HOLD、スコアや API は変更しない。

## Scenarios / acceptance

1. **Viewport / reachability** — Given 1024×768 / 1280×720 / 1366×768 / 1600×900 と FHD/2K/Mac、When Title/Result/Option/Opening と関連 #523 の各状態（モーダルを含む）を描画・監査、Then **#96 所有画面**の主要 pointer targets / 必須情報は viewport 内または可視スクロール導線で到達できる。#523 所有画面は同 Issue の自動テストで独立に判定する。スクロール可能な ScrollRect/Mask 内は正当な到達範囲を計算し、必須要素の全画面外・遮蔽を FAIL にする。単なる report-only 集計や PNG 撮影を PASS としない。
2. **Interaction / design regression** — Given 低解像度と大画面、When keyboard/pointer で決定/戻る/リトライ/選曲/登録/検索等を動かす、Then input→handler→state→feedback→exit が双方で成立し、大画面の 1728×972 frame origin / scale=1 / design metrics / hit targets / テキストを退化させない。
3. **Automated verification / isolation** — Given 上記を検証する変更 SHA、When 関連 C# を Unity compile + EditMode 回帰（0 tests / skip だけなら失敗）・独立レビュー・軽量 CI に通す、Then 全て green であること。物理 Windows 操作、Mac 擬似解像度、手動の目視はこの実装の完了条件ではなく release 観測に分離する。

## Non-goals

最小解像度を引き上げて当初の 1024×768 対応要求を削除すること、キーボード専用への縮退、全 Unity Player を各 Issue で起動して撮影すること、大規模 UI 再設計、スコア判定変更、本番 API 書き込み。

## Rollback

#96 の共有 viewport/helper 変更が regression を発生させたら当該実装 PR を単独 revert し、#523 の固有 Select/Game/Pause 差分へ暗黙に巻き戻しを伝播させない。共通 API の互換を保てない場合は #523 の変更を一時的に無効化・再調整してから、#96 の既知の旧低解像度欠陥を再度 open defect として追跡する。revert 後は FHD/2K/Mac の関連 EditMode と #523 の caller contract を再テストする。判定/メディア/HOLD/スコア/API は変更しない。
