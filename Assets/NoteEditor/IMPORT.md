# NoteEditor 取り込み記録

## 取り込み元
- リポジトリ: https://github.com/setchi/NoteEditor
- ライセンス: MIT (`Assets/NoteEditor/LICENSE`)
- 取り込みコミット SHA: 189256ef612105f3ccba1440b9fbd88c38a03db6
- 取り込み日: 2026-08-07
- 取り込み方法: `git clone --depth 1` で取得 → `Assets/NoteEditor/` にコピー (履歴破棄)

## 注記
- NoteEditor 自体は Unity 2019.1.5f1 ベース
- UniRx は vendored (master ブランチが Unity 6 をサポート済み)
- 本計画では最小修正のみ実施 (Task 6 の FindObjectOfType → FindAnyObjectByType)
