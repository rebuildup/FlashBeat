# run-e — 1 曲フルプレイ + Result → Retry の再入場 (2026-11-15)

- ビルド: `Builds/macOS/FlashBeat.app` (内容は main `263a07b4` と同一。#511 マージ分 + #512 は証跡のみ)
- 方法: `V6MEM_UI=TitleScene script/mac-v6mem.sh <app> <out> 1 1 ツイッターランド`
  (`-autotest v6mem`、PlayOnce が選曲→Ready→Game→自然終了→Result、RetryProbe が Result→Retry→GameScene 再入場)
- 結果: `[V6Mem] done`、FAIL 行なし

## 実測値 (Player.log / autotest-v6mem-result.txt)

| 項目 | 値 |
|---|---|
| 曲終了 | `end state=Completed scene=ResultScene judged=130` (強制終了なし、自然終了→Result 遷移) |
| メディア準備 (初回) | `media ready=True audioMode=StreamCombined videoPrepared=True failure=None` |
| Retry → GameScene | `retry media ready=True videoPrepared=True` (再入場後もメディア準備が完了) |
| Retry 後のプレイ状態 | `census label=retry gameplay scene=GameScene` (GameScene でゲームプレイ状態を確認) |
| Retry 後の退出 | `post-retry select scene=SelectScene` |

## A/V の証跡 (run-a / run-b の Player.log と併せて)

`[MusicManager] STATUS mediaTime=… rms=… peak=… video[playing=True …]` が約 2 秒間隔で記録される:

- `mediaTime` が 0→42 秒へほぼ実時間どおりに進行 (再生クロックが止まらない = #48/#31 の回帰なし)
- `rms` 0.14→0.51 / `peak` ~0.7 の非ゼロ値が演奏中ずっと継続 = **音声が実際に出ている**
- `video[playing=True prepared=True]` = 映像がデコード・描画されている
- `audioMode=StreamCombined` = 音声と映像が同一ストリームの同一クロックで進むため、A/V は構造的に同期する

「音と映像がぴったり合っているか」の人間の主観判定はスクリーンショットでは証明できないため、
機械的に確認できる範囲 (同一クロック・非無音・再生継続) をもって同期の証拠とし、
主観的なlip-syncの最終目視は Win64 受入 (#242 系) に委ねる。
