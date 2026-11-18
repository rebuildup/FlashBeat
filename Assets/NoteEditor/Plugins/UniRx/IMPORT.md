# UniRx 取り込み記録

## 取り込み元
- リポジトリ: https://github.com/neuecc/UniRx
- ライセンス: MIT（`ReadMe.txt` / upstream LICENSE 参照）
- 取り込みバージョン: `5.5.0`（upstream tag）
- 取り込み方法: upstream `Assets/Plugins/UniRx/` を `Assets/NoteEditor/Plugins/UniRx/` にコピー（履歴破棄）
- 照合日: 2026-11-18 — upstream tag `5.5.0` と全ファイル byte 一致を確認

## ローカル追加・差分
- `Scripts/UniRx.asmdef` … Unity assembly definition（ローカル追加、upstream には存在しない）
- `IMPORT.md`（本ファイル）
- 上記以外の upstream ファイルへの改変はない。改変が必要になった場合はこのディレクトリを `linguist-vendored` から外すか、fork 管理へ移すこと（`config/linguist-vendored.sha256` の check が改変を検出する）。

## 統計上の扱い
- `.gitattributes` で `linguist-vendored` 指定済み（GitHub 言語統計から除外。Issue #636）
- NoteEditor 側の取り込み経緯は `Assets/NoteEditor/IMPORT.md` を参照
EOF
} > "$current" --update && bash script/linguist-vendored.sh