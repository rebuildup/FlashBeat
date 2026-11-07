# Plan: 109-key-config

## Constraints

- trunk は `origin/release-0-1-3` ベースの branch `109`。PR は Draft で `--base release-0-1-3`。
- 汎用コミット (KeyBindings.cs / KeyConfigUI.cs / Judge.cs 1 行 / KeyBindingsTests.cs) は `4b0685d` (v0.1.2) に cherry-pick 可能な自己完結コミットとして分ける。
- KeyGuideUI 変更と trunk 専用テストは trunk 専用コミットに分離。
- Unity Editor は起動しない → コンパイル検証は目視レビュー + asmdef 参照確認のみ (unverified)。

## Steps

1. `Assets/Game/Scripts/Gameplay/KeyBindings.cs` 新規 (IStorage 注入, JSON, 予約キー, 空レーン規則) + `Assets/Tests/Editor/KeyBindingsTests.cs`。→ commit A (portable)
2. `Judge.cs` の 1 行置換。→ commit B (portable)
3. `Assets/Game/Scripts/UI/KeyConfigUI.cs` 新規 (KeyGuideUI 非依存)。→ commit C (portable)
4. `KeyGuideUI.cs` を KeyBindings 経由に変更 + `Assets/Tests/Editor/KeyBindingsTrunkTests.cs`。→ commit D (trunk only)
5. `specs/109-key-config/` ドキュメント。→ commit E
6. push → Draft PR (`--base release-0-1-3`, body で #109 と #41 に言及)。
7. `GIT_LFS_SKIP_SMUDGE=1 git worktree add --detach /Users/rebui/work/FlashBeat.109p 4b0685d` → branch `109-patch` を作り commit A/B/C を cherry-pick → push (PR なし)。

## Risks

- コンパイル検証不可 (Editor 起動禁止) → 目視レビューで代替。JsonUtility は `List<string>` を持つ `[Serializable]` クラスのみ使用 (ジャグ配列は非対応のためレーンをカンマ区切り文字列で表現)。
- v0.1.2 側の `Judge.cs` は同一構造 (`LaneKeys` が private) → commit B は文脈一致するはず。ズレたら手動適用してパッチブランチ上で別コミットにする。
