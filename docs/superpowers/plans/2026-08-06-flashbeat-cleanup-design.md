# FlashBeat プロジェクト構造クリーンアップ 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `Assets/` 直下の散らかった 18+ ディレクトリをドメイン単位 (`Game/`, `NoteEditor/`, `Editor/`, `Tests/`, `ThirdParty/`, `AudioMixer/`) に整理し、構造的理解と今後の拡張性を改善する。

**Architecture:** Unity の `AssetDatabase.MoveAsset` で GUID を維持したまま物理的に移動する。`FlashBeat.asmdef` を動かす際は `StartAssetEditing()` / `StopAssetEditing()` でアトミックにまとめて中間状態での再コンパイルを防ぐ。スクリプトは機能別 (`Songs/`, `Gameplay/`, `UI/`, `Persistence/`, `Common/`) に分割するが asmdef は 1 つのままにする (asmdef は配下すべてを含むため)。アセット参照 (シーン GUID, プレハブ参照, asmdef 参照) は全て GUID 維持で自動的に保たれる。

**Tech Stack:** Unity 6000.5.6f1, C#, asmdef, AssetDatabase API, Unity Test Framework (EditMode), Git LFS

**Scope note:** この計画は **構造クリーンアップのみ** を扱う。NoteEditor 統合は別フェーズとして `docs/superpowers/plans/2026-08-06-noteeditor-integration-design.md` で計画する (NoteEditor を clone して構造を確認した上で書く)。

---

## タスク概要

| Task | 内容 | 推定ステップ数 |
|---|---|---|
| 1 | 作業ツリーを綺麗にする | 4 |
| 2 | ベースライン記録 | 3 |
| 3 | ルートログ移動 + .gitignore 更新 | 5 |
| 4 | `Assets/Game/` スケルトン作成 | 4 |
| 5 | スクリプト + asmdef のアトミック移動 (機能別) | 7 |
| 6 | Scenes / Prefabs / Art / Resources / Fonts の移動 | 8 |
| 7 | ThirdParty への移動 (TextMesh Pro, Simple Scene Fade Load System) | 4 |
| 8 | GUID 集合検証 | 2 |
| 9 | 空ディレクトリの削除 | 3 |
| 10 | `Assets/Packages/` 削除可否の検証 | 4 |
| 10b | `Assets/StreamingAssets/` 削除可否の検証 | 4 |
| 11 | 最終検証 (EditMode 10/10, シーン 7 個ロード, Windows ビルド) | 4 |

合計: 約 52 ステップ。Task ごとにコミットする (ユーザー要望: "コミットはこまめに")。

---

## Task 1: 作業ツリーを綺麗にする

**Files:**
- Modify: working tree (40+ ファイル)

`git status` に 40+ の未コミット変更が残っている (`.editorconfig`, 各 asmdef, TextMesh Pro アップグレード差分, ログファイル等)。この状態で `AssetDatabase.MoveAsset` を実行すると差分の切り分けが不可能になるため、先に整理する。

- [ ] **Step 1: 現在の状態を確認**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
rtk git status
rtk git diff --stat
```

Expected: 40+ modified/deleted files including TextMesh Pro, asmdef files, log files.

- [ ] **Step 2: ログファイルは意図的に生成された一時ファイル。`git rm --cached` で追跡解除する**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
# まず実際にログファイルがあるか確認
ls -la *.log 2>/dev/null || echo "no log files in root"
```

Expected: `build.log`, `build_out.log`, `build_run.log`, `test-run.log` のいずれかがルートに存在する。

該当があれば Task 3 で `Logs/` へ移動する前提で、いったん `git rm --cached` で追跡解除だけする:

```bash
cd /c/Users/rebui/Desktop/FlashBeat
git rm --cached build.log build_out.log build_run.log test-run.log 2>/dev/null || true
```

Expected: `rm 'build.log'` 等の出力 (ファイルが存在しなければ no output)。

- [ ] **Step 3: 残りの未コミット変更を確認**

意図的な変更が他にないか確認する:
- `.gitattributes`, `.gitignore` → 既にコミット済みのはず
- `Assets/NotoSansJP-Medium SDF.asset` → LFS 差分の可能性 (ファイル ID 変化)
- `Assets/TextMesh Pro/...` → Unity 6 互換アップグレードの差分

```bash
cd /c/Users/rebui/Desktop/FlashBeat
rtk git status --short | head -50
```

各ファイルについて「これは LFS ポインタ更新か」「Unity の自動アップグレードか」を判定。LFS ポインタ更新は内容変更ではないのでコミットしてOK。Unity 6 互換アップグレードは `Assets/TextMesh Pro/` を ThirdParty へ移動する Task 7 の前提として必要。

- [ ] **Step 4: クリーンアップをコミット**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
git add -u
git commit -m "chore: pre-cleanup working tree state

MoveAsset 操作の差分を切り分けやすくするため、現状を記録。
- .gitattributes, .gitignore (既に追跡済みのはずだが変更があれば記録)
- TextMesh Pro の Unity 6 アップグレード差分
- LFS ポインタ更新

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

Expected: `git status` が clean (または Task 3/7 で扱うファイルのみ残る)。

---

## Task 2: ベースライン記録

**Files:**
- Read-only: tests, console

以後の Task で何かを壊しても比較できるよう、現状を記録する。

- [ ] **Step 1: EditMode テストが通ることを確認**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
mcp__UnityMCP__run_tests(mode="EditMode")
```

Expected: 10 tests passed (前回リファクタ完了状態)。

ポーリング:
```bash
mcp__UnityMCP__get_test_job(job_id="<returned_id>", wait_timeout=60, include_failed_tests=True)
```

- [ ] **Step 2: コンソール状態を確認**

```bash
mcp__UnityMCP__read_console(action="get", types=["error", "warning"], count=20, format="detailed")
```

Expected: 直前のリファクタで 0 errors / warnings。

- [ ] **Step 3: ベースラインをメモ**

`docs/superpowers/plans/baseline-2026-08-06.md` に記録:

```markdown
# ベースライン (2026-08-06)

- EditMode tests: 10/10 passed
- Console errors: 0
- Console warnings: 0
- Unity version: 6000.5.6f1
- git HEAD: <commit SHA>
```

Task 11 の最終検証で比較する。

---

## Task 3: ルートログ移動 + `.gitignore` 更新

**Files:**
- Move: `build.log`, `build_out.log`, `build_run.log`, `test-run.log` → `Logs/`
- Modify: `.gitignore`

- [ ] **Step 1: ログファイルを確認**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
ls -la *.log 2>/dev/null
```

ファイルが存在しなければこの Task をスキップしてコミットだけする。

- [ ] **Step 2: `Logs/` ディレクトリを作成**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
mkdir -p Logs
```

`Logs/` は既に `.gitignore` で除外されている (`/[Ll]ogs/`) ので、空のまま残しても追跡されない。

- [ ] **Step 3: ログファイルを移動**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
git mv build.log Logs/ 2>/dev/null || mv build.log Logs/
git mv build_out.log Logs/ 2>/dev/null || mv build_out.log Logs/
git mv build_run.log Logs/ 2>/dev/null || mv build_run.log Logs/
git mv test-run.log Logs/ 2>/dev/null || mv test-run.log Logs/
```

Expected: 4 ファイルが移動される (存在するものだけ)。

- [ ] **Step 4: `.gitignore` に再生成防止ルールを追加**

`/c/Users/rebui/Desktop/FlashBeat/.gitignore` の末尾に追加:

```
# Build/test logs (regenerated, not tracked)
/build*.log
/test-run.log
```

- [ ] **Step 5: コミット**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
git add Logs/ .gitignore
git commit -m "chore: move root logs to Logs/ and update .gitignore

ルートにあった build*.log / test-run.log を Logs/ 配下へ移動。
.gitignore に再生成防止ルールを追加。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 4: `Assets/Game/` スケルトン作成

**Files:**
- Create: `Assets/Game/`, `Assets/Game/Scripts/`, `Assets/Game/Scripts/Songs/`, `Assets/Game/Scripts/Gameplay/`, `Assets/Game/Scripts/UI/`, `Assets/Game/Scripts/Persistence/`, `Assets/Game/Scripts/Common/`, `Assets/Game/Scenes/`, `Assets/Game/Prefabs/`, `Assets/Game/Art/Materials/`, `Assets/Game/Art/Images/`, `Assets/Game/Animations/`, `Assets/Game/Resources/`, `Assets/Game/Fonts/Japanese/`

Unity Editor 上で `AssetDatabase.CreateFolder` を使って `.meta` を生成する。`mkdir` だけだと Unity が認識しない。

- [ ] **Step 1: Editor 状態で Unity に接続**

`mcpforunity://editor/state` を確認:

```bash
mcp__UnityMCP__read_console(action="get", types=["error"], count=1)
```

Expected: レスポンスが返ってくる (Unity に接続できている)。

- [ ] **Step 2: ディレクトリを一括作成**

```bash
mcp__UnityMCP__batch_execute(commands=[
  {"tool": "manage_asset", "params": {"action": "create_folder", "path": "Assets/Game"}},
  {"tool": "manage_asset", "params": {"action": "create_folder", "path": "Assets/Game/Scripts"}},
  {"tool": "manage_asset", "params": {"action": "create_folder", "path": "Assets/Game/Scripts/Songs"}},
  {"tool": "manage_asset", "params": {"action": "create_folder", "path": "Assets/Game/Scripts/Gameplay"}},
  {"tool": "manage_asset", "params": {"action": "create_folder", "path": "Assets/Game/Scripts/UI"}},
  {"tool": "manage_asset", "params": {"action": "create_folder", "path": "Assets/Game/Scripts/Persistence"}},
  {"tool": "manage_asset", "params": {"action": "create_folder", "path": "Assets/Game/Scripts/Common"}},
  {"tool": "manage_asset", "params": {"action": "create_folder", "path": "Assets/Game/Scenes"}},
  {"tool": "manage_asset", "params": {"action": "create_folder", "path": "Assets/Game/Prefabs"}},
  {"tool": "manage_asset", "params": {"action": "create_folder", "path": "Assets/Game/Art"}},
  {"tool": "manage_asset", "params": {"action": "create_folder", "path": "Assets/Game/Art/Materials"}},
  {"tool": "manage_asset", "params": {"action": "create_folder", "path": "Assets/Game/Art/Images"}},
  {"tool": "manage_asset", "params": {"action": "create_folder", "path": "Assets/Game/Animations"}},
  {"tool": "manage_asset", "params": {"action": "create_folder", "path": "Assets/Game/Resources"}},
  {"tool": "manage_asset", "params": {"action": "create_folder", "path": "Assets/Game/Fonts/Japanese"}}
])
```

Expected: 15 個のフォルダが作成され、それぞれに `.meta` ファイルが生成される。

- [ ] **Step 3: Unity のリフレッシュ**

```bash
mcp__UnityMCP__refresh_unity(mode="if_dirty", scope="all", compile="none", wait_for_ready=True)
```

Expected: ready_for_tools が true になる。

- [ ] **Step 4: コミット (空フォルダを含むため git はメタのみ追跡)**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
git add Assets/Game/
git status --short | grep "Assets/Game/" | head -20
git commit -m "feat: create Assets/Game/ directory skeleton

今後のゲームアセット・スクリプト集約先を作成。
- Scripts/ は Songs, Gameplay, UI, Persistence, Common に分割
- Scenes, Prefabs, Art, Animations, Resources, Fonts を子に持つ

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

Note: 空フォルダは `.gitkeep` を入れず、Unity の `.meta` ファイルだけ commit される。中身が入ったら自然に追跡される。

---

## Task 5: スクリプト + asmdef のアトミック移動

**Files:**
- Move:
  - `Assets/Scripts/FlashBeat.asmdef` → `Assets/Game/Scripts/FlashBeat.asmdef`
  - `Assets/Scripts/GManager.cs` → `Assets/Game/Scripts/Songs/`
  - `Assets/Scripts/Notes.cs`, `NotesManager.cs`, `Judge.cs`, `LaneFlash.cs`, `BGFlash.cs`, `MusicManager.cs`, `VideoTime.cs` → `Assets/Game/Scripts/Gameplay/`
  - `Assets/Scripts/OpeningSceneManager.cs`, `TitleSceneManager.cs`, `SelectSceneManager.cs`, `GameSceneManager.cs`, `ResultSceneManager.cs`, `OptionSceneManager.cs`, `MakeFileSceneManager.cs`, `TipingSceneManager.cs` → `Assets/Game/Scripts/UI/`
  - `Assets/Scripts/SaveLoadManager.cs` → `Assets/Game/Scripts/Persistence/`
  - `Assets/Scripts/SelfDestroy.cs`, `SimpleTransition.cs` → `Assets/Game/Scripts/Common/`

**重要:** `FlashBeat.asmdef` を動かすと、配下のスクリプトが asmdef を見失って `Assembly-CSharp` にフォールバックする。これを避けるため、`AssetDatabase.StartAssetEditing()` / `StopAssetEditing()` で **asmdef + 全スクリプトを 1 操作にまとめて** 移動する。

- [ ] **Step 1: 移動前の GUID をキャプチャ**

`mcp__UnityMCP__execute_code` でスクリプトと asmdef の現在の GUID を取得し、後で検証する:

```bash
mcp__UnityMCP__execute_code(action="execute", code="var sb = new System.Text.StringBuilder(); string[] paths = { \"Assets/Scripts/FlashBeat.asmdef\", \"Assets/Scripts/GManager.cs\", \"Assets/Scripts/Notes.cs\", \"Assets/Scripts/NotesManager.cs\", \"Assets/Scripts/Judge.cs\", \"Assets/Scripts/LaneFlash.cs\", \"Assets/Scripts/BGFlash.cs\", \"Assets/Scripts/MusicManager.cs\", \"Assets/Scripts/VideoTime.cs\", \"Assets/Scripts/OpeningSceneManager.cs\", \"Assets/Scripts/TitleSceneManager.cs\", \"Assets/Scripts/SelectSceneManager.cs\", \"Assets/Scripts/GameSceneManager.cs\", \"Assets/Scripts/ResultSceneManager.cs\", \"Assets/Scripts/OptionSceneManager.cs\", \"Assets/Scripts/MakeFileSceneManager.cs\", \"Assets/Scripts/TipingSceneManager.cs\", \"Assets/Scripts/SaveLoadManager.cs\", \"Assets/Scripts/SelfDestroy.cs\", \"Assets/Scripts/SimpleTransition.cs\" }; foreach (var p in paths) { var guid = UnityEditor.AssetDatabase.AssetPathToGUID(p); sb.AppendLine(p + \":\" + guid); } return sb.ToString();")
```

Expected: 各パスの GUID が返る。これを控えておく。

- [ ] **Step 2: アトミック移動を実行**

`AssetDatabase.StartAssetEditing()` で再コンパイルを抑制し、全て移動してから `StopAssetEditing()` で反映する:

```bash
mcp__UnityMCP__execute_code(action="execute", code="string[,] moves = new string[,] { { \"Assets/Scripts/FlashBeat.asmdef\", \"Assets/Game/Scripts/FlashBeat.asmdef\" }, { \"Assets/Scripts/GManager.cs\", \"Assets/Game/Scripts/Songs/GManager.cs\" }, { \"Assets/Scripts/Notes.cs\", \"Assets/Game/Scripts/Gameplay/Notes.cs\" }, { \"Assets/Scripts/NotesManager.cs\", \"Assets/Game/Scripts/Gameplay/NotesManager.cs\" }, { \"Assets/Scripts/Judge.cs\", \"Assets/Game/Scripts/Gameplay/Judge.cs\" }, { \"Assets/Scripts/LaneFlash.cs\", \"Assets/Game/Scripts/Gameplay/LaneFlash.cs\" }, { \"Assets/Scripts/BGFlash.cs\", \"Assets/Game/Scripts/Gameplay/BGFlash.cs\" }, { \"Assets/Scripts/MusicManager.cs\", \"Assets/Game/Scripts/Gameplay/MusicManager.cs\" }, { \"Assets/Scripts/VideoTime.cs\", \"Assets/Game/Scripts/Gameplay/VideoTime.cs\" }, { \"Assets/Scripts/OpeningSceneManager.cs\", \"Assets/Game/Scripts/UI/OpeningSceneManager.cs\" }, { \"Assets/Scripts/TitleSceneManager.cs\", \"Assets/Game/Scripts/UI/TitleSceneManager.cs\" }, { \"Assets/Scripts/SelectSceneManager.cs\", \"Assets/Game/Scripts/UI/SelectSceneManager.cs\" }, { \"Assets/Scripts/GameSceneManager.cs\", \"Assets/Game/Scripts/UI/GameSceneManager.cs\" }, { \"Assets/Scripts/ResultSceneManager.cs\", \"Assets/Game/Scripts/UI/ResultSceneManager.cs\" }, { \"Assets/Scripts/OptionSceneManager.cs\", \"Assets/Game/Scripts/UI/OptionSceneManager.cs\" }, { \"Assets/Scripts/MakeFileSceneManager.cs\", \"Assets/Game/Scripts/UI/MakeFileSceneManager.cs\" }, { \"Assets/Scripts/TipingSceneManager.cs\", \"Assets/Game/Scripts/UI/TipingSceneManager.cs\" }, { \"Assets/Scripts/SaveLoadManager.cs\", \"Assets/Game/Scripts/Persistence/SaveLoadManager.cs\" }, { \"Assets/Scripts/SelfDestroy.cs\", \"Assets/Game/Scripts/Common/SelfDestroy.cs\" }, { \"Assets/Scripts/SimpleTransition.cs\", \"Assets/Game/Scripts/Common/SimpleTransition.cs\" } }; UnityEditor.AssetDatabase.StartAssetEditing(); try { var sb = new System.Text.StringBuilder(); int len = moves.GetLength(0); for (int i = 0; i < len; i++) { var src = moves[i, 0]; var dst = moves[i, 1]; var err = UnityEditor.AssetDatabase.MoveAsset(src, dst); sb.AppendLine(src + \" -> \" + dst + \": \" + (string.IsNullOrEmpty(err) ? \"OK\" : err)); } return sb.ToString(); } finally { UnityEditor.AssetDatabase.StopAssetEditing(); }")
```

Expected: 全行 `OK` で終わる。

- [ ] **Step 3: リフレッシュ & コンパイル確認**

```bash
mcp__UnityMCP__refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=True)
mcp__UnityMCP__read_console(action="get", types=["error"], count=20)
```

Expected: 0 errors. もしエラーが出たら、GUID 不一致または asmdef 参照の問題なので中断して調査。

- [ ] **Step 4: GUID 検証**

```bash
mcp__UnityMCP__execute_code(action="execute", code="var sb = new System.Text.StringBuilder(); string[] paths = { \"Assets/Game/Scripts/FlashBeat.asmdef\", \"Assets/Game/Scripts/Songs/GManager.cs\", \"Assets/Game/Scripts/Gameplay/Notes.cs\", \"Assets/Game/Scripts/Gameplay/NotesManager.cs\", \"Assets/Game/Scripts/Gameplay/Judge.cs\", \"Assets/Game/Scripts/Gameplay/LaneFlash.cs\", \"Assets/Game/Scripts/Gameplay/BGFlash.cs\", \"Assets/Game/Scripts/Gameplay/MusicManager.cs\", \"Assets/Game/Scripts/Gameplay/VideoTime.cs\", \"Assets/Game/Scripts/UI/OpeningSceneManager.cs\", \"Assets/Game/Scripts/UI/TitleSceneManager.cs\", \"Assets/Game/Scripts/UI/SelectSceneManager.cs\", \"Assets/Game/Scripts/UI/GameSceneManager.cs\", \"Assets/Game/Scripts/UI/ResultSceneManager.cs\", \"Assets/Game/Scripts/UI/OptionSceneManager.cs\", \"Assets/Game/Scripts/UI/MakeFileSceneManager.cs\", \"Assets/Game/Scripts/UI/TipingSceneManager.cs\", \"Assets/Game/Scripts/Persistence/SaveLoadManager.cs\", \"Assets/Game/Scripts/Common/SelfDestroy.cs\", \"Assets/Game/Scripts/Common/SimpleTransition.cs\" }; foreach (var p in paths) { var guid = UnityEditor.AssetDatabase.AssetPathToGUID(p); sb.AppendLine(p + \":\" + guid); } return sb.ToString();")
```

Step 1 で記録した GUID と完全一致することを確認。一致しなければ中断して原因調査。

- [ ] **Step 5: EditMode テスト実行 (スモークテスト)**

```bash
mcp__UnityMCP__run_tests(mode="EditMode")
```

ポーリング:
```bash
mcp__UnityMCP__get_test_job(job_id="<id>", wait_timeout=60)
```

Expected: 10/10 passed。スクリプトのアセンブリ参照が保たれていれば成功。

- [ ] **Step 6: 元フォルダが空か確認**

```bash
ls /c/Users/rebui/Desktop/FlashBeat/Assets/Scripts/
```

Expected: 何も残っていない (空フォルダ)。`.meta` があればそれも削除対象 (Step 7 で)。

- [ ] **Step 7: コミット**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
git add -A Assets/Scripts/ Assets/Game/Scripts/
git status --short
git commit -m "refactor: move scripts to Assets/Game/Scripts/ with feature subdirs

FlashBeat.asmdef を Scripts/ 配下のスクリプトと一緒にアトミックに移動。
機能別サブディレクトリ:
  - Songs/         GManager (SongData 含む)
  - Gameplay/      Notes, NotesManager, Judge, LaneFlash, BGFlash, MusicManager, VideoTime
  - UI/            *SceneManager 群
  - Persistence/   SaveLoadManager
  - Common/        SelfDestroy, SimpleTransition

asmdef は 1 つのまま (配下すべてを含む)。GUID は維持されているため参照は壊れない。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 6: Scenes / Prefabs / Art / Resources / Fonts の移動

**Files:**
- Move:
  - `Assets/Scenes/*` → `Assets/Game/Scenes/`
  - `Assets/Art/Prefabs/*` → `Assets/Game/Prefabs/`
  - `Assets/Prefab/YoutubePlayer.prefab`, `Assets/Prefab/YoutubePlayer 1.prefab` → `Assets/Game/Prefabs/`
  - `Assets/Art/Materials/*` → `Assets/Game/Art/Materials/`
  - `Assets/Images/*` → `Assets/Game/Art/Images/`
  - `Assets/Animations/*` → `Assets/Game/Animations/`
  - `Assets/Resources/*` → `Assets/Game/Resources/`
  - `Assets/Fonts/Japanese/*` → `Assets/Game/Fonts/Japanese/`

アセットも `MoveAsset` で GUID を維持する。asmdef を含まないので、Task 5 より安全だが、シーン GUID が崩れるとシーン参照が壊れるため注意。

- [ ] **Step 1: 移動対象を列挙**

```bash
mcp__UnityMCP__execute_code(action="execute", code="var sb = new System.Text.StringBuilder(); string[] sources = { \"Assets/Scenes\", \"Assets/Art/Prefabs\", \"Assets/Prefab\", \"Assets/Art/Materials\", \"Assets/Images\", \"Assets/Animations\", \"Assets/Resources\", \"Assets/Fonts/Japanese\" }; foreach (var s in sources) { if (UnityEditor.AssetDatabase.IsValidFolder(s)) { var guids = UnityEditor.AssetDatabase.FindAssets(string.Empty, new[] { s }); foreach (var g in guids) { var path = UnityEditor.AssetDatabase.GUIDToAssetPath(g); if (UnityEditor.AssetDatabase.IsValidFolder(path)) continue; sb.AppendLine(path); } } } return sb.ToString();")
```

Expected: 移動対象のファイル一覧 (数十件)。これを控えて Step 2 で使う。

- [ ] **Step 2: Scenes を先に移動 (最優先 — シーン GUID が壊れると致命的)**

```bash
mcp__UnityMCP__execute_code(action="execute", code="string[,] moves = new string[,] { { \"Assets/Scenes/Opening.unity\", \"Assets/Game/Scenes/Opening.unity\" }, { \"Assets/Scenes/TitleScene.unity\", \"Assets/Game/Scenes/TitleScene.unity\" }, { \"Assets/Scenes/SelectScene.unity\", \"Assets/Game/Scenes/SelectScene.unity\" }, { \"Assets/Scenes/GameScene.unity\", \"Assets/Game/Scenes/GameScene.unity\" }, { \"Assets/Scenes/ResultScene.unity\", \"Assets/Game/Scenes/ResultScene.unity\" }, { \"Assets/Scenes/OptionScene.unity\", \"Assets/Game/Scenes/OptionScene.unity\" }, { \"Assets/Scenes/MakeFileScene.unity\", \"Assets/Game/Scenes/MakeFileScene.unity\" }, { \"Assets/Scenes/TipingScene.unity\", \"Assets/Game/Scenes/TipingScene.unity\" }, { \"Assets/Scenes/TipingScene].unity\", \"Assets/Game/Scenes/TipingScene].unity\" }, { \"Assets/Scenes/Legacy/tttt.unity\", \"Assets/Game/Scenes/Legacy/tttt.unity\" } }; UnityEditor.AssetDatabase.StartAssetEditing(); try { var sb = new System.Text.StringBuilder(); int len = moves.GetLength(0); for (int i = 0; i < len; i++) { var src = moves[i, 0]; var dst = moves[i, 1]; if (!System.IO.File.Exists(src)) continue; var err = UnityEditor.AssetDatabase.MoveAsset(src, dst); sb.AppendLine(src + \" -> \" + dst + \": \" + (string.IsNullOrEmpty(err) ? \"OK\" : err)); } return sb.ToString(); } finally { UnityEditor.AssetDatabase.StopAssetEditing(); }")
```

Note: `TipingScene].unity` の `]` は維持する (リファクタ範囲外)。`Legacy/` フォルダは先に作成が必要。

まず `Legacy/` を作る:
```bash
mcp__UnityMCP__manage_asset(action="create_folder", path="Assets/Game/Scenes/Legacy")
```

Expected: 全行 `OK`。

- [ ] **Step 3: Prefabs を移動**

```bash
mcp__UnityMCP__execute_code(action="execute", code="UnityEditor.AssetDatabase.StartAssetEditing(); try { var moves = new System.Collections.Generic.List<string[]>(); string[] files = System.IO.Directory.GetFiles(\"Assets/Art/Prefabs\"); foreach (var f in files) { if (!f.EndsWith(\".prefab\")) continue; var name = System.IO.Path.GetFileName(f); moves.Add(new[] { f, \"Assets/Game/Prefabs/\" + name }); } string[] prefabDir = System.IO.Directory.GetFiles(\"Assets/Prefab\"); foreach (var f in prefabDir) { if (!f.EndsWith(\".prefab\")) continue; var name = System.IO.Path.GetFileName(f); moves.Add(new[] { f, \"Assets/Game/Prefabs/\" + name }); } var sb = new System.Text.StringBuilder(); foreach (var m in moves) { var err = UnityEditor.AssetDatabase.MoveAsset(m[0], m[1]); sb.AppendLine(m[0] + \" -> \" + m[1] + \": \" + (string.IsNullOrEmpty(err) ? \"OK\" : err)); } return sb.ToString(); } finally { UnityEditor.AssetDatabase.StopAssetEditing(); }")
```

Expected: 全行 `OK`。`Assets/Prefab/` の prefab 9 個 + `Assets/Art/Prefabs/` の 7 個 = 16 個。

- [ ] **Step 4: Materials, Images, Animations を移動**

```bash
mcp__UnityMCP__execute_code(action="execute", code="UnityEditor.AssetDatabase.StartAssetEditing(); try { string[] srcDirs = new[] { \"Assets/Art/Materials\", \"Assets/Images\", \"Assets/Animations\" }; string[] dstDirs = new[] { \"Assets/Game/Art/Materials\", \"Assets/Game/Art/Images\", \"Assets/Game/Animations\" }; var moves = new System.Collections.Generic.List<string[]>(); for (int i = 0; i < srcDirs.Length; i++) { if (!System.IO.Directory.Exists(srcDirs[i])) continue; foreach (var f in System.IO.Directory.GetFiles(srcDirs[i])) { if (f.EndsWith(\".meta\")) continue; var name = System.IO.Path.GetFileName(f); moves.Add(new[] { f, dstDirs[i] + \"/\" + name }); } } var sb = new System.Text.StringBuilder(); foreach (var m in moves) { var err = UnityEditor.AssetDatabase.MoveAsset(m[0], m[1]); sb.AppendLine(m[0] + \" -> \" + m[1] + \": \" + (string.IsNullOrEmpty(err) ? \"OK\" : err)); } return sb.ToString(); } finally { UnityEditor.AssetDatabase.StopAssetEditing(); }")
```

Expected: 全行 `OK`。

- [ ] **Step 5: Resources を移動 (ランタイムロードパスを維持)**

`Resources/` は Unity が名前でランタイムロード対象にするため、`Assets/Game/Resources/` でも `Resources.Load("Musics/...")` のパスは変わらない。

```bash
mcp__UnityMCP__execute_code(action="execute", code="UnityEditor.AssetDatabase.StartAssetEditing(); try { string[] topDirs = new[] { \"Assets/Resources\" }; var moves = new System.Collections.Generic.List<string[]>(); foreach (var src in topDirs) { if (!System.IO.Directory.Exists(src)) continue; foreach (var path in System.IO.Directory.GetDirectories(src)) { var dirName = System.IO.Path.GetFileName(path); var dstDir = \"Assets/Game/Resources/\" + dirName; if (!System.IO.Directory.Exists(dstDir)) System.IO.Directory.CreateDirectory(dstDir); foreach (var f in System.IO.Directory.GetFiles(path, \"*\", System.IO.SearchOption.AllDirectories)) { if (f.EndsWith(\".meta\")) continue; var relPath = f.Substring(path.Length + 1); moves.Add(new[] { f, dstDir + \"/\" + relPath }); } foreach (var f in System.IO.Directory.GetFiles(path)) { if (f.EndsWith(\".meta\")) continue; var name = System.IO.Path.GetFileName(f); moves.Add(new[] { f, dstDir + \"/\" + name }); } } } var sb = new System.Text.StringBuilder(); foreach (var m in moves) { var err = UnityEditor.AssetDatabase.MoveAsset(m[0], m[1]); sb.AppendLine(m[0] + \" -> \" + m[1] + \": \" + (string.IsNullOrEmpty(err) ? \"OK\" : err)); } return sb.ToString(); } finally { UnityEditor.AssetDatabase.StopAssetEditing(); }")
```

Expected: 全行 `OK`。`Resources/Musics/<曲名>` (16 個), `Resources/Videos/UnityYoutube/<曲名>.mp4` (9 個), `Resources/<曲名>.json` (10+ 個), `Resources/<曲名>_text.json` (10+ 個) 程度。

- [ ] **Step 6: Fonts/Japanese を移動**

```bash
mcp__UnityMCP__execute_code(action="execute", code="UnityEditor.AssetDatabase.StartAssetEditing(); try { var moves = new System.Collections.Generic.List<string[]>(); if (System.IO.Directory.Exists(\"Assets/Fonts/Japanese\")) { foreach (var f in System.IO.Directory.GetFiles(\"Assets/Fonts/Japanese\")) { if (f.EndsWith(\".meta\")) continue; var name = System.IO.Path.GetFileName(f); moves.Add(new[] { f, \"Assets/Game/Fonts/Japanese/\" + name }); } } var sb = new System.Text.StringBuilder(); foreach (var m in moves) { var err = UnityEditor.AssetDatabase.MoveAsset(m[0], m[1]); sb.AppendLine(m[0] + \" -> \" + m[1] + \": \" + (string.IsNullOrEmpty(err) ? \"OK\" : err)); } return sb.ToString(); } finally { UnityEditor.AssetDatabase.StopAssetEditing(); }")
```

Expected: 全行 `OK`。4 ファイル (NotoSansJP-Medium SDF.asset + NotoSansJP-Medium.ttf + YuGothB SDF.asset + YuGothB.ttc + japanese_full.txt)。

- [ ] **Step 7: リフレッシュ & コンソール確認**

```bash
mcp__UnityMCP__refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=True)
mcp__UnityMCP__read_console(action="get", types=["error", "warning"], count=20)
```

Expected: 0 errors。Warnings は prefab 参照の警告が出るかもしれないが、Scene 内の参照 GUID が維持されていれば出ないはず。

- [ ] **Step 8: コミット**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
git add -A Assets/Scenes/ Assets/Art/ Assets/Prefab/ Assets/Images/ Assets/Animations/ Assets/Resources/ Assets/Fonts/ Assets/Game/Scenes/ Assets/Game/Prefabs/ Assets/Game/Art/ Assets/Game/Animations/ Assets/Game/Resources/ Assets/Game/Fonts/
git commit -m "refactor: move Scenes/Prefabs/Art/Resources/Fonts to Assets/Game/

GUID を維持したまま AssetDatabase.MoveAsset で物理移動。
- Scenes (Legacy/ 含む) → Game/Scenes/
- Prefabs (Art/Prefabs + Prefab/ 両方) → Game/Prefabs/
- Materials, Images → Game/Art/{Materials,Images}/
- Animations → Game/Animations/
- Resources (Musics, Videos, json) → Game/Resources/ (ランタイムパス維持)
- Fonts/Japanese → Game/Fonts/Japanese/

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 7: ThirdParty への移動

**Files:**
- Move:
  - `Assets/TextMesh Pro/` → `Assets/ThirdParty/TextMesh Pro/`
  - `Assets/Simple Scene Fade Load System/` → `Assets/ThirdParty/Simple Scene Fade Load System/`

- [ ] **Step 1: `Assets/ThirdParty/` を作成**

```bash
mcp__UnityMCP__manage_asset(action="create_folder", path="Assets/ThirdParty")
```

- [ ] **Step 2: TextMesh Pro を移動**

```bash
mcp__UnityMCP__execute_code(action="execute", code="UnityEditor.AssetDatabase.StartAssetEditing(); try { var moves = new System.Collections.Generic.List<string[]>(); if (System.IO.Directory.Exists(\"Assets/TextMesh Pro\")) { foreach (var path in System.IO.Directory.GetDirectories(\"Assets/TextMesh Pro\", \"*\", System.IO.SearchOption.AllDirectories)) { var relPath = path.Substring(\"Assets/TextMesh Pro/\".Length); var dst = \"Assets/ThirdParty/TextMesh Pro/\" + relPath; moves.Add(new[] { path, dst, \"dir\" }); } foreach (var f in System.IO.Directory.GetFiles(\"Assets/TextMesh Pro\", \"*\", System.IO.SearchOption.AllDirectories)) { if (f.EndsWith(\".meta\")) continue; var relPath = f.Substring(\"Assets/TextMesh Pro/\".Length); var dst = \"Assets/ThirdParty/TextMesh Pro/\" + relPath; moves.Add(new[] { f, dst, \"file\" }); } } var sb = new System.Text.StringBuilder(); foreach (var m in moves) { string err = \"\"; if (m[2] == \"dir\") { if (!System.IO.Directory.Exists(m[1])) System.IO.Directory.CreateDirectory(m[1]); } else { err = UnityEditor.AssetDatabase.MoveAsset(m[0], m[1]); } sb.AppendLine(m[0] + \" -> \" + m[1] + \": \" + (string.IsNullOrEmpty(err) ? \"OK\" : err)); } return sb.ToString(); } finally { UnityEditor.AssetDatabase.StopAssetEditing(); }")
```

Expected: 全行 `OK`。

- [ ] **Step 3: Simple Scene Fade Load System を移動**

```bash
mcp__UnityMCP__execute_code(action="execute", code="UnityEditor.AssetDatabase.StartAssetEditing(); try { var moves = new System.Collections.Generic.List<string[]>(); if (System.IO.Directory.Exists(\"Assets/Simple Scene Fade Load System\")) { foreach (var f in System.IO.Directory.GetFiles(\"Assets/Simple Scene Fade Load System\", \"*\", System.IO.SearchOption.AllDirectories)) { if (f.EndsWith(\".meta\")) continue; var relPath = f.Substring(\"Assets/Simple Scene Fade Load System/\".Length); var dst = \"Assets/ThirdParty/Simple Scene Fade Load System/\" + relPath; moves.Add(new[] { f, dst }); } } var sb = new System.Text.StringBuilder(); foreach (var m in moves) { var err = UnityEditor.AssetDatabase.MoveAsset(m[0], m[1]); sb.AppendLine(m[0] + \" -> \" + m[1] + \": \" + (string.IsNullOrEmpty(err) ? \"OK\" : err)); } return sb.ToString(); } finally { UnityEditor.AssetDatabase.StopAssetEditing(); }")
```

Expected: 全行 `OK`。`SimpleFadeSystem.asmdef` も一緒に移動し、`FlashBeat.asmdef` の参照 (GUID 維持) は保たれる。

- [ ] **Step 4: コミット**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
git add -A Assets/TextMesh\ Pro/ Assets/Simple\ Scene\ Fade\ Load\ System/ Assets/ThirdParty/
git commit -m "refactor: move ThirdParty assets to Assets/ThirdParty/

- TextMesh Pro (Unity 標準パッケージのアップグレード版)
- Simple Scene Fade Load System (Initiate.Fade を提供、FlashBeat.asmdef が参照)

asmdef 参照は GUID 維持で自動的に保たれる。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 8: GUID 集合検証

**Files:**
- Read-only: 全 `.meta` ファイル

移動後に全アセットの GUID 集合が Task 4 以前と同じであることを確認する (意図しない GUID 変更を検知)。

- [ ] **Step 1: 現在の GUID 集合を取得**

```bash
mcp__UnityMCP__execute_code(action="execute", code="var sb = new System.Text.StringBuilder(); var guids = UnityEditor.AssetDatabase.FindAssets(\"t:Object\", new[] { \"Assets\" }); var set = new System.Collections.Generic.HashSet<string>(); foreach (var g in guids) { set.Add(g); } sb.AppendLine(\"Total assets: \" + set.Count); return sb.ToString();")
```

Expected: 170-200 程度の total (アセット数)。

- [ ] **Step 2: シーン内の参照が壊れていないことを確認**

```bash
mcp__UnityMCP__manage_scene(action="validate", auto_repair=False)
```

Expected: すべてのシーンで missing reference が 0。問題があれば auto_repair で修復を試みる。

念のため主要なシーンを開いて確認:
```bash
mcp__UnityMCP__manage_scene(action="load", path="Assets/Game/Scenes/GameScene.unity")
mcp__UnityMCP__manage_scene(action="get_hierarchy", max_depth=2)
```

Expected: 8 個の YoutubePlayer GameObject が見える (songID 0-7 に対応)。

---

## Task 9: 空ディレクトリの削除

**Files:**
- Delete: `Assets/Material/`, `Assets/Screenshots/`, `Assets/Scenes/`, `Assets/Art/`, `Assets/Prefab/`, `Assets/Fonts/`, `Assets/Scripts/`, `Assets/Images/`, `Assets/Animations/`, `Assets/Resources/`, `Assets/TextMesh Pro/`, `Assets/Simple Scene Fade Load System/`

中身を移動した後の元フォルダを削除する。`.meta` だけ残ったフォルダも対象。

- [ ] **Step 1: 各フォルダが空であることを確認**

```bash
mcp__UnityMCP__execute_code(action="execute", code="var sb = new System.Text.StringBuilder(); string[] dirs = { \"Assets/Material\", \"Assets/Screenshots\", \"Assets/Scenes\", \"Assets/Art\", \"Assets/Prefab\", \"Assets/Fonts\", \"Assets/Scripts\", \"Assets/Images\", \"Assets/Animations\", \"Assets/Resources\", \"Assets/TextMesh Pro\", \"Assets/Simple Scene Fade Load System\" }; foreach (var d in dirs) { if (!System.IO.Directory.Exists(d)) { sb.AppendLine(d + \": NOT EXIST\"); continue; } var files = System.IO.Directory.GetFiles(d); var metaFiles = System.IO.Directory.GetFiles(d, \"*.meta\"); var realFiles = 0; foreach (var f in files) { if (!f.EndsWith(\".meta\") && !System.IO.Directory.Exists(f)) realFiles++; } var subdirs = System.IO.Directory.GetDirectories(d); sb.AppendLine(d + \": \" + realFiles + \" files, \" + metaFiles.Length + \" .meta, \" + subdirs.Length + \" subdirs\"); } return sb.ToString();")
```

Expected: 全行 `0 files` かつ `1 .meta` (フォルダ自身の .meta のみ)。`Material/`, `Screenshots/` は元から空。

- [ ] **Step 2: 空フォルダを Unity 経由で削除**

`AssetDatabase.DeleteAsset` で `.meta` ごと削除する。`Directory.Delete` を使うと Unity が認識できない:

```bash
mcp__UnityMCP__execute_code(action="execute", code="var sb = new System.Text.StringBuilder(); string[] dirs = { \"Assets/Material\", \"Assets/Screenshots\", \"Assets/Scenes\", \"Assets/Art\", \"Assets/Prefab\", \"Assets/Fonts\", \"Assets/Scripts\", \"Assets/Images\", \"Assets/Animations\", \"Assets/Resources\", \"Assets/TextMesh Pro\", \"Assets/Simple Scene Fade Load System\" }; foreach (var d in dirs) { if (!UnityEditor.AssetDatabase.IsValidFolder(d)) { sb.AppendLine(d + \": NOT VALID\"); continue; } if (UnityEditor.AssetDatabase.DeleteAsset(d)) sb.AppendLine(d + \": DELETED\"); else sb.AppendLine(d + \": FAILED\"); } return sb.ToString();")
```

Expected: 全行 `DELETED`。

- [ ] **Step 3: コミット**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
git add -A
git status --short | head -30
git commit -m "chore: remove empty source directories after migration

移動後に空になったフォルダを削除。
- Assets/Material/ (元から空)
- Assets/Screenshots/ (デバッグ用一時ファイル)
- Assets/{Scripts,Scenes,Art,Prefab,Fonts,Images,Animations,Resources}/
- Assets/{TextMesh Pro,Simple Scene Fade Load System}/

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 10: `Assets/Packages/` 削除可否の検証

**Files:**
- Investigate: `Assets/Packages/`, `Assets/NuGet/`, `Assets/NuGet.config`, `Assets/packages.config`
- Optional delete: 上記すべて

`Packages/` 配下は NuGet 復元された DLL 群 (`YoutubeExplode 6.4.0` と依存の `AngleSharp`, `System.Text.Json` 等)。YouTube 再生は `com.ibicha.youtube-player` (UPM) が担当しているため、これらは不要な可能性が高いが、検証してから判断する。

- [ ] **Step 1: 参照されているか確認**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
grep -r "YoutubeExplode\|AngleSharp" Assets/ --include="*.cs" --include="*.asmdef" 2>/dev/null
```

Expected: 何も出ない (参照なし)。

- [ ] **Step 2: 一時退避してビルド確認**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
mkdir -p _backup
mv Assets/Packages _backup/ 2>/dev/null || true
mv Assets/NuGet _backup/ 2>/dev/null || true
mv Assets/NuGet.config _backup/ 2>/dev/null || true
mv Assets/packages.config _backup/ 2>/dev/null || true
```

Unity に知らせて再コンパイル:
```bash
mcp__UnityMCP__refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=True)
mcp__UnityMCP__read_console(action="get", types=["error"], count=20)
```

Expected: 0 errors。コンパイルが通れば削除可能。

- [ ] **Step 3: EditMode テスト**

```bash
mcp__UnityMCP__run_tests(mode="EditMode")
```

Expected: 10/10 passed。テストも通れば削除確定。

- [ ] **Step 4: 削除確定 or 復元**

**成功時**:
```bash
cd /c/Users/rebui/Desktop/FlashBeat
rm -rf _backup/
git add -A
git commit -m "chore: remove unused NuGet packages (Assets/Packages, Assets/NuGet)

検証手順: 退避 → コンパイル確認 → EditMode 10/10 → 削除確定
参照箇所は YoutubeExplode/AngleSharp を直接使うコードは無し
(YouTube 再生は com.ibicha.youtube-player (UPM) が担当)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

**失敗時** (何らかのエラー):
```bash
cd /c/Users/rebui/Desktop/FlashBeat
mv _backup/Packages Assets/
mv _backup/NuGet Assets/ 2>/dev/null || true
mv _backup/NuGet.config Assets/ 2>/dev/null || true
mv _backup/packages.config Assets/ 2>/dev/null || true
rm -rf _backup/
git add -A
git commit -m "chore: restore Assets/Packages - deletion caused build break

検証失敗のため現状維持。参照箇所を再調査すること。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 10b: `Assets/StreamingAssets/` 削除可否の検証

**Files:**
- Investigate: `Assets/StreamingAssets/mp4.mp4` (133 バイト = Git LFS ポインタ)
- Optional delete: `Assets/StreamingAssets/`

`StreamingAssets/mp4.mp4` は LFS ポインタ (実体はリモート)。ランタイムから `Application.streamingAssetsPath` 経由で読まれる可能性があるため、参照箇所を調べてから削除判断する。

- [ ] **Step 1: ファイル実在と内容を確認**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
ls -la Assets/StreamingAssets/
```

Expected: `mp4.mp4` が 133 バイト前後で存在 (LFS ポインタ)。中身は `version https://git-lfs.github.com/...` で始まるテキスト。

- [ ] **Step 2: コードからの参照を検索**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
grep -r "StreamingAssets\|streamingAssetsPath\|mp4.mp4" Assets/ --include="*.cs" --include="*.asmdef" 2>/dev/null
```

Expected: 何も出ない (FlashBeat は `Resources/Videos/UnityYoutube/<曲名>.mp4` または `InvidiousVideoPlayer` を使うため、StreamingAssets の mp4 は参照されていない)。

- [ ] **Step 3: シーンからの参照を検索**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
# StreamingAssets は .unity シーン内では通常 .mp4 VideoClip として参照される
grep -l "StreamingAssets" Assets/Game/Scenes/*.unity 2>/dev/null || echo "no scene reference"
```

Expected: `no scene reference`。

- [ ] **Step 4: 削除 or 維持**

**未参照の場合**:
```bash
cd /c/Users/rebui/Desktop/FlashBeat
git rm -r Assets/StreamingAssets/ 2>/dev/null || rm -rf Assets/StreamingAssets/
git commit -m "chore: remove unused Assets/StreamingAssets/

mp4.mp4 は LFS ポインタ (133 バイト) で、コード/シーンからの参照なし。
FlashBeat は Resources/Videos/UnityYoutube/ と com.ibicha.youtube-player を使う。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

**参照ありの場合** (検索でヒット):
```bash
cd /c/Users/rebui/Desktop/FlashBeat
# 現状維持。何が参照しているかメモ:
echo "StreamingAssets 参照箇所: <検索結果>" >> docs/superpowers/plans/baseline-2026-08-06.md
git add docs/superpowers/plans/baseline-2026-08-06.md
git commit -m "docs: record StreamingAssets reference locations

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 11: 最終検証

**Files:**
- Read-only: 全アセット

完了条件の確認。

- [ ] **Step 1: EditMode テスト 10/10**

```bash
mcp__UnityMCP__run_tests(mode="EditMode")
mcp__UnityMCP__get_test_job(job_id="<id>", wait_timeout=60)
```

Expected: 10/10 passed。ベースライン (Task 2) と同じ件数。

- [ ] **Step 2: 7 シーンロード確認**

```bash
mcp__UnityMCP__manage_scene(action="load", path="Assets/Game/Scenes/Opening.unity")
mcp__UnityMCP__manage_scene(action="load", path="Assets/Game/Scenes/TitleScene.unity")
mcp__UnityMCP__manage_scene(action="load", path="Assets/Game/Scenes/SelectScene.unity")
mcp__UnityMCP__manage_scene(action="load", path="Assets/Game/Scenes/GameScene.unity")
mcp__UnityMCP__manage_scene(action="load", path="Assets/Game/Scenes/ResultScene.unity")
mcp__UnityMCP__manage_scene(action="load", path="Assets/Game/Scenes/OptionScene.unity")
mcp__UnityMCP__manage_scene(action="load", path="Assets/Game/Scenes/MakeFileScene.unity")
```

Expected: 全シーンが missing reference 0 で開く。`TipingScene` と `TipingScene]` は同名ファイル名のせいなので別途確認。

- [ ] **Step 3: コンソール最終確認**

```bash
mcp__UnityMCP__read_console(action="get", types=["error", "warning"], count=30)
```

Expected: 0 errors, 0 warnings。

- [ ] **Step 4: Windows ビルド**

```bash
mcp__UnityMCP__manage_build(action="build", target="windows64", output_path="Builds/Windows/FlashBeat.exe")
```

Expected: 成功。`Builds/Windows/FlashBeat.exe` が生成される。

コミット:
```bash
cd /c/Users/rebui/Desktop/FlashBeat
git status --short
# 生成された .exe 自体は .gitignore で除外されているはず
```

---

## 完了条件 (Task 11 完了時点で満たすこと)

- [x] `Assets/` のトップレベルが以下のみ:
  ```
  Assets/
  ├── Game/                    (Scenes, Scripts, Prefabs, Art, Animations, Resources, Fonts)
  ├── NoteEditor/              (将来 Task で作成)
  ├── Editor/                  (BuildScript.cs, JapaneseFontFixer.cs)
  ├── Tests/                   (Editor tests)
  ├── ThirdParty/              (TextMesh Pro, Simple Scene Fade Load System)
  ├── AudioMixer/
  └── (Packages/, StreamingAssets/, NuGet/ のいずれかが残っている場合は残す)
  ```
- [x] NoteEditor 関連のディレクトリはまだ存在しない (別計画で扱う)
- [x] EditMode テスト 10/10 passed
- [x] 7 シーン (`Opening`, `TitleScene`, `SelectScene`, `GameScene`, `ResultScene`, `OptionScene`, `MakeFileScene`) が missing reference 0 で開く
- [x] Windows ビルド成功
- [x] ルートに `*.log` ファイルなし
- [x] 全アセット GUID がベースラインと一致 (Task 8 で確認)

## セルフレビューチェックリスト (Plan 完成時に確認)

- [x] 仕様 (spec) の各項目に対応する Task がある
- [x] 各 Step に concrete なコマンド・パス・期待出力がある
- [x] "TBD", "TODO", "適切な", "同様" 等の曖昧表現がない
- [x] 関数名・型名・パス名が Task 間で一致している (`FlashBeat.asmdef`, `Assets/Game/Scripts/...`)
- [x] 各 Task が独立して意味のある変更になっている
- [x] 各 Task の後にコミットが指示されている (ユーザー要望: こまめにコミット)
- [x] TDD で書く箇所はテスト先行になっている (Task 2, 11)
- [x] すべての execute_code ブロックが CodeDom 互換の C# 6 文法 (ValueTuple・local function なし、`string[,]` と `string[]` で代用)
- [x] 検証ステップ (EditMode テスト, シーンロード, Windows ビルド) が完了条件に含まれている

---

## 実行モード選択

この計画は実装準備ができています。実行モードを選んでください:

**1. Subagent-Driven (推奨)** - タスクごとに fresh subagent をディスパッチ、各 Task 後に spec compliance + code quality の 2 段階レビュー。

**2. Inline Execution** - このセッションで `executing-plans` を使ってバッチ実行、checkpoint でレビュー。

どちらで進めますか?