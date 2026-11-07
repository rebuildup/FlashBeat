# Spec: #118 更新を明示式・任意にする

## 問題
UpdateChecker は起動時に確認 → staging へ自動ダウンロード → OnApplicationQuit で自動適用する。ユーザー同意が無い。

## 要件
1. 起動時は**確認のみ** (非同期・ブロックしない・失敗はログのみ)。チャンネル規則 (stable は prerelease を対象外)・バージョン比較・ダウングレード禁止は維持。
2. 新版があるとき TitleScene (ホーム) にのみ『アップデートあり vX.Y.Z』通知を出す (コード構築 overlay、シーン編集なし)。
   - 右上・セーフマージン、不透明ダークピル + アクセント枠、1024x768..2560x1440 で可読。
   - Canvas: ScaleWithScreenSize 1920x1080 / match 0.5、sortingOrder は Title UI より上・キー設定 overlay (500) より下。
   - テキスト: `アップデートあり vX.Y.Z  [F3: 今すぐ更新]  (押さなければ更新しません)`
   - トリガーは F3 キー **と** マウスクリック (文字キーは使わない・非 QWERTY 対応)。F4 または × でこのセッションは非表示。
   - 何もしなければダウンロードも適用も行わない。
3. ユーザーがトリガーした後のみ: staging へダウンロード (進捗 % 表示) + SHA-256 検証 → 成功で `更新の準備ができました。終了して適用 [F3]` → ユーザーが確認 (F3/ボタン) したときだけ既存の `Update.ps1 -Apply -WaitPid -Relaunch` で適用。確認なし終了では何も適用しない (staging は残り、次回また通知)。
4. 失敗 (ネットワーク / SHA 不一致 / 二重起動) は一行メッセージ表示で現行版維持、ゲームは動き続ける。
5. ゲームプレーを中断しない (通知は Title のみ)。確認ダイアログは再起動することを明記。
6. Option シーンの適用ボタンは維持 (それ自体が明示的操作)。docs/operations/update-release.md と update-patch.md のフロー記述を新フローに更新。

## 不変条件 (テスト対象)
- 状態遷移: Idle → Checking → Available → Downloading → Ready → Applying。
- Applying への遷移経路はユーザー確認メソッドのみ。終了時フックからの適用は存在しない。
- Dismiss はセッション中の表示を消すが staging と状態は保持。
- 失敗時は Available/Ready に戻りメッセージを持ち、現行版は不変。
