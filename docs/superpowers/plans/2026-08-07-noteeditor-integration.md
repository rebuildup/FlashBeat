# NoteEditor 統合実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** setchi/NoteEditor を FlashBeat の `Assets/NoteEditor/` に持ち込み、Unity 6000.5.6f1 でコンパイル・シーンロードできる状態にする。

**Architecture:** NoteEditor のリポジトリ構造を `Assets/NoteEditor/` にミラーコピー (Scripts/、Scenes/、Prefabs/、Art/Materials/、Art/Textures/、Audio/、Shaders/、Plugins/UniRx/、LICENSE)。Unity 6 で消えた API を最小修正 (`FindObjectOfType` → `FindAnyObjectByType`)。UniRx は vendored ライブラリのまま配置し、NoteEditor 用 asmdef で分離。FlashBeat 本体とは独立したコンパイル単位にする。

**Tech Stack:** Unity 6000.5.6f1, UniRx (vendored), C# (.NET Standard 2.1)

---

## 設計判断

### UniRx の扱い
- **vendored 維持** (推奨): NoteEditor が同梱する `Assets/Plugins/UniRx/` をそのまま `Assets/NoteEditor/Plugins/UniRx/` にコピーする。UniRx の master ブランチは Unity 6 をサポート済み (neuecc/UniRx アーカイブ後の 2023+ コミット)。
- **UPM 置換** (将来): modern fork (`com.neuecc.unirx` via OpenUPM) への移行は別タスク。本計画ではリスク最小化のため vendored を維持。

### asmdef 構成
- `Assets/NoteEditor/Plugins/UniRx/Scripts/UniRx.asmdef` — UniRx 用 (FlashBeat の FlashBeat.asmdef とは独立)
- `Assets/NoteEditor/NoteEditor.asmdef` — NoteEditor 本体。`UniRx` を参照

これにより `Assets/Game/Scripts/FlashBeat.asmdef` は UniRx を参照せず、NoteEditor のコードは UniRx 経由でしか到達できない。

### NoteEditor のディレクトリ再編
| NoteEditor 標準 | FlashBeat 配置 |
|---|---|
| `Assets/Materials/` | `Assets/NoteEditor/Art/Materials/` |
| `Assets/Textures/` | `Assets/NoteEditor/Art/Textures/` |
| `Assets/Prefabs/` | `Assets/NoteEditor/Prefabs/` |
| `Assets/Scripts/` | `Assets/NoteEditor/Scripts/` |
| `Assets/Scenes/` | `Assets/NoteEditor/Scenes/` |
| `Assets/Sounds/` | `Assets/NoteEditor/Audio/` |
| `Assets/Shaders/` | `Assets/NoteEditor/Shaders/` |
| `Assets/Plugins/UniRx/` | `Assets/NoteEditor/Plugins/UniRx/` |
| `LICENSE` (リポジトリルート) | `Assets/NoteEditor/LICENSE` |

---

## 実装順序

### Task 1: NoteEditor コピー元の確認と記録

**Files:**
- Read: `Assets/NoteEditor/IMPORT.md` (新規作成、NoteEditor commit SHA 記録用)

- [ ] **Step 1: 取得先と SHA を確認**

```bash
cat /tmp/noteeditor-inspect/../../noteeditor-inspect 2>/dev/null || true
git -C /tmp/noteeditor-inspect log -1 --pretty=format:"%H%n%s%n%ad"
```

Expected: コミット SHA + subject + 日付。例: `189256e Update README.md`

- [ ] **Step 2: 取得記録を作成**

`Assets/NoteEditor/IMPORT.md` を以下の内容で作成 (Step 1 の値で SHA を置換):

```markdown
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
- 本計画では最小修正のみ実施 (Task 5 の FindObjectOfType → FindAnyObjectByType)
```

- [ ] **Step 3: コミット**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
mkdir -p Assets/NoteEditor
git add Assets/NoteEditor/IMPORT.md
git commit -m "docs: record NoteEditor import source and SHA"
```

---

### Task 2: NoteEditor の LICENSE をコピー

**Files:**
- Copy: `Assets/NoteEditor/LICENSE` ← `/tmp/noteeditor-inspect/LICENSE`

- [ ] **Step 1: LICENSE をコピー**

```bash
cp /tmp/noteeditor-inspect/LICENSE "C:/Users/rebui/Desktop/FlashBeat/Assets/NoteEditor/LICENSE"
```

- [ ] **Step 2: コミット**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
git add Assets/NoteEditor/LICENSE
git commit -m "chore: add NoteEditor LICENSE (MIT)"
```

---

### Task 3: NoteEditor スクリプトをコピー

**Files:**
- Copy: `Assets/NoteEditor/Scripts/**/*` ← `/tmp/noteeditor-inspect/Assets/Scripts/**/*` (62 .cs ファイル)

- [ ] **Step 1: Unity を使ってコピー (メタファイル生成)**

`manage_asset(action="create_folder")` で `Assets/NoteEditor/Scripts` を作り、`System.IO.File.Copy` で 62 ファイル全てコピー → `AssetDatabase.Refresh` で .meta 生成。

```bash
# Unity を介してコピー (メタファイル生成のため必須)
```

`mcp__UnityMCP__execute_code` で以下の C# 6 互換コードを実行:

```csharp
using UnityEngine;
using UnityEditor;
using System.IO;

var src = "C:/Users/rebui/AppData/Local/Temp/noteeditor-inspect/Assets/Scripts";
var dst = "Assets/NoteEditor/Scripts";

if (!AssetDatabase.IsValidFolder(dst))
{
    AssetDatabase.CreateFolder("Assets/NoteEditor", "Scripts");
}

var srcFiles = Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories);
int copied = 0;
foreach (var srcFile in srcFiles)
{
    var relPath = srcFile.Substring(src.Length + 1).Replace('\\', '/');
    var dstPath = dst + "/" + relPath;
    var dstDir = Path.GetDirectoryName(dstPath);
    if (!AssetDatabase.IsValidFolder(dstDir.Replace('\\','/')))
    {
        Directory.CreateDirectory(dstDir);
        AssetDatabase.Refresh();
    }
    File.Copy(srcFile, dstPath, true);
    copied++;
}
AssetDatabase.Refresh();
return "copied: " + copied;
```

Expected: `copied: 62`

- [ ] **Step 2: ファイル数を確認**

```bash
find "C:/Users/rebui/Desktop/FlashBeat/Assets/NoteEditor/Scripts" -name "*.cs" 2>&1 | wc -l
```

Expected: `62`

- [ ] **Step 3: コンパイル状態を確認**

```
mcp__UnityMCP__refresh_unity(mode="force", scope="scripts", compile="request", wait_for_ready=True)
mcp__UnityMCP__read_console(action="get", types=["error"], count=20)
```

Expected: 1 error (`SingletonMonoBehaviour.cs:14` の `FindObjectOfType`)。他のエラーは出ない想定 (Task 5 で修正)。

- [ ] **Step 4: コミット**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
git add Assets/NoteEditor/Scripts/
git commit -m "feat: copy NoteEditor scripts to Assets/NoteEditor/Scripts/"
```

---

### Task 4: NoteEditor のアセットをコピー

**Files:**
- Copy: `Assets/NoteEditor/Scenes/NoteEditor.unity`
- Copy: `Assets/NoteEditor/Prefabs/*.prefab` (5 ファイル)
- Copy: `Assets/NoteEditor/Art/Materials/Waveform.mat`
- Copy: `Assets/NoteEditor/Art/Textures/**/*` (6 PNG)
- Copy: `Assets/NoteEditor/Audio/*.mp3 *.wav` (2 ファイル)
- Copy: `Assets/NoteEditor/Shaders/Waveform.shader`

- [ ] **Step 1: Unity を介して全アセットをコピー**

```csharp
// execute_code で実行 (C# 6 互換)
using UnityEngine;
using UnityEditor;
using System.IO;

var src = "C:/Users/rebui/AppData/Local/Temp/noteeditor-inspect/Assets";
var dst = "Assets/NoteEditor";

var fileMap = new System.Collections.Generic.List<string[]>{
    new[]{"Materials/Waveform.mat", "Art/Materials"},
    new[]{"Textures/edit_marker_handle.png", "Art/Textures"},
    new[]{"Textures/white_back.png", "Art/Textures"},
    new[]{"Textures/Icons/arrow.png", "Art/Textures/Icons"},
    new[]{"Textures/Icons/icon_caution.png", "Art/Textures/Icons"},
    new[]{"Textures/Icons/icon_directory.png", "Art/Textures/Icons"},
    new[]{"Textures/Icons/icon_file.png", "Art/Textures/Icons"},
    new[]{"Textures/Icons/icon_import.png", "Art/Textures/Icons"},
    new[]{"Prefabs/------------------------------.prefab", "Prefabs"},
    new[]{"Prefabs/BeatNumberText.prefab", "Prefabs"},
    new[]{"Prefabs/FileListItem.prefab", "Prefabs"},
    new[]{"Prefabs/InputNoteKeyCodeSettingsItem.prefab", "Prefabs"},
    new[]{"Prefabs/SettingsView.prefab", "Prefabs"},
    new[]{"Scenes/NoteEditor.unity", "Scenes"},
    new[]{"Shaders/Waveform.shader", "Shaders"},
    new[]{"Sounds/Clap.mp3", "Audio"},
    new[]{"Sounds/Click 2.wav", "Audio"},
};

int copied = 0;
foreach (var m in fileMap)
{
    var srcFile = src + "/" + m[0];
    var dstFolder = dst + "/" + m[1];
    var dstFile = dstFolder + "/" + Path.GetFileName(m[0]);
    if (!AssetDatabase.IsValidFolder(dstFolder))
    {
        Directory.CreateDirectory(dstFolder);
    }
    File.Copy(srcFile, dstFile, true);
    copied++;
}
AssetDatabase.Refresh();
return "copied: " + copied;
```

Expected: `copied: 17`

- [ ] **Step 2: ファイル数を確認**

```bash
find "C:/Users/rebui/Desktop/FlashBeat/Assets/NoteEditor" \( -name "*.prefab" -o -name "*.mat" -o -name "*.shader" -o -name "*.png" -o -name "*.unity" -o -name "*.mp3" -o -name "*.wav" \) ! -path "*/Scripts/*" 2>&1 | wc -l
```

Expected: `17`

- [ ] **Step 3: コミット**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
git add Assets/NoteEditor/Prefabs/ Assets/NoteEditor/Art/ Assets/NoteEditor/Audio/ Assets/NoteEditor/Shaders/ Assets/NoteEditor/Scenes/
git commit -m "feat: copy NoteEditor assets (scenes, prefabs, materials, textures, audio, shaders)"
```

---

### Task 5: NoteEditor の UniRx をコピー

**Files:**
- Copy: `Assets/NoteEditor/Plugins/UniRx/**/*` ← vendored UniRx full source

- [ ] **Step 1: Examples を含めてコピー (UniRx 本体)**

```csharp
// execute_code で実行
using UnityEngine;
using UnityEditor;
using System.IO;

var src = "C:/Users/rebui/AppData/Local/Temp/noteeditor-inspect/Assets/Plugins/UniRx";
var dst = "Assets/NoteEditor/Plugins/UniRx";

if (!AssetDatabase.IsValidFolder("Assets/NoteEditor/Plugins"))
{
    AssetDatabase.CreateFolder("Assets/NoteEditor", "Plugins");
}

var srcFiles = Directory.GetFiles(src, "*", SearchOption.AllDirectories);
int copied = 0;
foreach (var srcFile in srcFiles)
{
    if (srcFile.EndsWith(".meta")) continue;
    var relPath = srcFile.Substring(src.Length + 1).Replace('\\', '/');
    var dstPath = dst + "/" + relPath;
    var dstDir = Path.GetDirectoryName(dstPath);
    if (!Directory.Exists(dstDir))
    {
        Directory.CreateDirectory(dstDir);
    }
    File.Copy(srcFile, dstPath, true);
    copied++;
}
AssetDatabase.Refresh();
return "copied: " + copied;
```

Expected: 270 前後のファイル (Examples 含む)。

Note: Examples フォルダは NoteEditor では不要だが、UniRx のサンプルとして残す (削除すると後で困ることがある)。

- [ ] **Step 2: コンパイル確認**

```
mcp__UnityMCP__refresh_unity(mode="force", scope="scripts", compile="request", wait_for_ready=True)
mcp__UnityMCP__read_console(action="get", types=["error"], count=20)
```

Expected: 1 error (`FindObjectOfType`) のみ。

- [ ] **Step 3: コミット**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
git add Assets/NoteEditor/Plugins/
git commit -m "feat: copy vendored UniRx (master branch supports Unity 6)"
```

---

### Task 6: Unity 6 API 互換性修正 (FindObjectOfType → FindAnyObjectByType)

**Files:**
- Modify: `Assets/NoteEditor/Scripts/Utility/SingletonMonoBehaviour.cs:14`

- [ ] **Step 1: ファイルを確認**

```bash
cat "C:/Users/rebui/Desktop/FlashBeat/Assets/NoteEditor/Scripts/Utility/SingletonMonoBehaviour.cs"
```

Expected: `FindObjectOfType<T>()` の呼び出しが 1 箇所。

- [ ] **Step 2: API 置換**

`FindObjectOfType<T>()` を `FindAnyObjectByType<T>()` に置換。

```bash
cd "C:/Users/rebui/Desktop/FlashBeat"
sed -i 's/FindObjectOfType</FindAnyObjectByType</g' Assets/NoteEditor/Scripts/Utility/SingletonMonoBehaviour.cs
```

Expected: 1 箇所置換。

- [ ] **Step 3: コンパイル確認**

```
mcp__UnityMCP__refresh_unity(mode="force", scope="scripts", compile="request", wait_for_ready=True)
mcp__UnityMCP__read_console(action="get", types=["error"], count=20)
```

Expected: `0 errors` (NoteEditor 起因のエラーが消える)。

- [ ] **Step 4: 既存 EditMode テストが通ることを確認 (FlashBeat 本体への影響なし確認)**

```
mcp__UnityMCP__run_tests(mode="EditMode")
```

Expected: `10/10 passed`。

- [ ] **Step 5: コミット**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
git add Assets/NoteEditor/Scripts/Utility/SingletonMonoBehaviour.cs
git commit -m "fix: update FindObjectOfType to FindAnyObjectByType for Unity 6

Unity 6 で FindObjectOfType は deprecated。FindAnyObjectByType に置換。"
```

---

### Task 7: asmdef 構成 (UniRx 分離 + NoteEditor)

**Files:**
- Create: `Assets/NoteEditor/Plugins/UniRx/Scripts/UniRx.asmdef`
- Create: `Assets/NoteEditor/NoteEditor.asmdef`

- [ ] **Step 1: UniRx asmdef を作成**

`Assets/NoteEditor/Plugins/UniRx/Scripts/UniRx.asmdef`:

```json
{
    "name": "UniRx",
    "rootNamespace": "UniRx",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: NoteEditor asmdef を作成**

`Assets/NoteEditor/NoteEditor.asmdef`:

```json
{
    "name": "NoteEditor",
    "rootNamespace": "NoteEditor",
    "references": [
        "UniRx",
        "Unity.TextMeshPro"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

NoteEditor の NoteEditor.unity シーンは TextMesh Pro を使うので `Unity.TextMeshPro` を参照。

- [ ] **Step 3: コンパイル確認**

```
mcp__UnityMCP__refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=True)
mcp__UnityMCP__read_console(action="get", types=["error"], count=30)
```

Expected: `0 errors`。NoteEditor が独立コンパイル単位として成立。

- [ ] **Step 4: 既存 EditMode テストで 10/10 維持確認**

```
mcp__UnityMCP__run_tests(mode="EditMode")
```

Expected: `10/10 passed`。

- [ ] **Step 5: コミット**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
git add Assets/NoteEditor/Plugins/UniRx/Scripts/UniRx.asmdef Assets/NoteEditor/NoteEditor.asmdef
git commit -m "feat: add asmdef files (UniRx + NoteEditor)

NoteEditor 用に独立したコンパイル単位を作成し、FlashBeat 本体から分離:
- UniRx: vendored Reactive Extensions library
- NoteEditor: NoteEditor 本体、UniRx と TextMeshPro を参照"
```

---

### Task 8: NoteEditor シーンロード確認

**Files:**
- Read-only: `Assets/NoteEditor/Scenes/NoteEditor.unity`

- [ ] **Step 1: シーンを開く**

```
mcp__UnityMCP__manage_scene(action="load", path="Assets/NoteEditor/Scenes/NoteEditor.unity")
```

Expected: シーンが正常にロードされる (missing reference 0)。

- [ ] **Step 2: missing reference を確認**

```
mcp__UnityMCP__manage_scene(action="validate")
```

Expected: 0 missing refs (または NoteEditor 自身が正しく解決できる範囲の missing のみ)。

- [ ] **Step 3: NoteEditor シーンを閉じる (FlashBeat 作業に戻る)**

```
mcp__UnityMCP__manage_scene(action="close_scene", scene_path="Assets/NoteEditor/Scenes/NoteEditor.unity")
```

- [ ] **Step 4: 既存シーンが壊れていないか確認 (リグレッション)**

```
mcp__UnityMCP__manage_scene(action="load", path="Assets/Game/Scenes/GameScene.unity")
mcp__UnityMCP__manage_scene(action="validate")
```

Expected: 0 missing refs (リグレッションなし)。

---

### Task 9: 統合検証とドキュメント更新

**Files:**
- Modify: `CLAUDE.md` (NoteEditor の存在を記載)

- [ ] **Step 1: 全体検証**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
echo "=== NoteEditor ディレクトリ構造 ==="
find Assets/NoteEditor -maxdepth 2 -type d 2>&1 | sort
echo ""
echo "=== スクリプトファイル数 ==="
find Assets/NoteEditor/Scripts -name "*.cs" 2>&1 | wc -l
echo ""
echo "=== アセットファイル数 (Scripts 除く) ==="
find Assets/NoteEditor \( -name "*.prefab" -o -name "*.mat" -o -name "*.shader" -o -name "*.png" -o -name "*.unity" -o -name "*.mp3" -o -name "*.wav" \) ! -path "*/Scripts/*" 2>&1 | wc -l
```

Expected:
- ディレクトリ: Art/, Audio/, Plugins/, Prefabs/, Scenes/, Scripts/ の 6 サブディレクトリ
- Scripts: 62
- Assets: 17

- [ ] **Step 2: CLAUDE.md に NoteEditor セクションを追加**

`CLAUDE.md` の "Architecture" セクションの後に追記:

```markdown
### NoteEditor 統合

`Assets/NoteEditor/` に [setchi/NoteEditor](https://github.com/setchi/NoteEditor) を統合。譜面作成ツールとして独立して動作 (FlashBeat 本体とは別コンパイル単位)。

| ディレクトリ | 内容 |
|---|---|
| `Scripts/` | NoteEditor 本体 (62 .cs ファイル) |
| `Art/Materials/`, `Art/Textures/` | UI 用マテリアル・テクスチャ |
| `Prefabs/` | UI プレハブ (5 個) |
| `Scenes/NoteEditor.unity` | NoteEditor エディタシーン |
| `Audio/`, `Shaders/` | 音源・シェーダ |
| `Plugins/UniRx/` | vendored UniRx (Unity 6 サポート済み) |

asmdef: `UniRx` (vendored lib) + `NoteEditor` (本体、UniRx と TextMeshPro 参照)。FlashBeat 本体 (`FlashBeat.asmdef`) は UniRx を参照しない。
```

- [ ] **Step 3: コミット**

```bash
cd /c/Users/rebui/Desktop/FlashBeat
git add CLAUDE.md
git commit -m "docs: document NoteEditor integration in CLAUDE.md"
```

---

### Task 10: 最終検証

**Files:**
- Read-only: 全アセット

- [ ] **Step 1: EditMode テスト 10/10**

```
mcp__UnityMCP__run_tests(mode="EditMode")
mcp__UnityMCP__get_test_job(job_id="<id>", wait_timeout=60)
```

Expected: `10/10 passed`。

- [ ] **Step 2: 全シーンロード確認**

```
mcp__UnityMCP__manage_scene(action="load", path="Assets/Game/Scenes/Opening.unity")
mcp__UnityMCP__manage_scene(action="load", path="Assets/Game/Scenes/GameScene.unity")
mcp__UnityMCP__manage_scene(action="load", path="Assets/NoteEditor/Scenes/NoteEditor.unity")
```

Expected: 3 シーンとも missing reference 0。

- [ ] **Step 3: コンソール最終確認**

```
mcp__UnityMCP__read_console(action="get", types=["error", "warning"], count=30)
```

Expected: 0 errors。warnings は事前既存 (`JapaneseFontFixer.cs` の obsolete API) + UniRx 由来の informational のみ。

- [ ] **Step 4: Windows ビルド**

```
mcp__UnityMCP__manage_build(action="build", target="windows64", output_path="Builds/Windows/FlashBeat.exe")
```

Expected: 成功。`Builds/Windows/FlashBeat.exe` 生成 (NoteEditor は editor-only なのでビルドには含まれない)。

---

## 完了条件

- [x] `Assets/NoteEditor/` が作成され、Scripts/ + アセット類が格納されている
- [x] `Assets/NoteEditor/NoteEditor.unity` が Unity 6 で missing reference 0 で開く
- [x] NoteEditor のコードが Unity 6 でコンパイルエラーなく通る (`FindObjectOfType` 修正後)
- [x] `UniRx.asmdef` + `NoteEditor.asmdef` でコンパイル単位が分離されている
- [x] FlashBeat 本体 (`FlashBeat.asmdef`) は UniRx を参照していない (asmdef 分離確認)
- [x] 既存の 10 個の EditMode テストがすべてパスする
- [x] FlashBeat 7 シーン (`Assets/Game/Scenes/`) が全て正常ロードする
- [x] Windows ビルドが成功する
- [x] `Assets/NoteEditor/IMPORT.md` で取得元の SHA が記録されている
- [x] `CLAUDE.md` に NoteEditor セクションが追加されている

## 既知の不整合 (本計画のスコープ外)

- NoteEditor の譜面保存形式 (XML/JSON) と FlashBeat の譜面形式 (`Data` JSON) の互換性 — 必要なら将来エクスポート機能追加
- NoteEditor の UniRx を modern fork (`com.neuecc.unirx` via OpenUPM) に置換 — UniRx の API 互換性検証後に別タスクで
- UniRx Examples フォルダ (約 200 ファイル) の削除 — NoteEditor 本体には不要だが vendored ライブラリのおまけとして残している
- NoteEditor の macOS / WebGL ビルド対応 — NoteEditor は Editor ツールなので Standalone ビルドからは除外 (現タスクのビルドでは自然に除外される)

## 実行モード選択

Subagent-Driven (推奨) または Inline Execution を選択。