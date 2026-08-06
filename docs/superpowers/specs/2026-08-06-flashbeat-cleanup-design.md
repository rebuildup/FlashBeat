# FlashBeat プロジェクト構造クリーンアップ + NoteEditor 統合

## 目的

FlashBeat の `Assets/` 直下の散らかったディレクトリ構成をドメイン単位で整理し、譜面作成ツール NoteEditor (https://github.com/setchi/NoteEditor) をこのプロジェクトに直接統合する。

統合の動機は2つ:
1. **構造的理解の促進**: 18 個のディレクトリが `Assets/` 直下に並列配置されており、何がどこに何のためにあるか把握しにくい。
2. **開発体験の改善**: NoteEditor を別プロジェクトとして維持する煩雑さを解消し、1 つのリポジトリで譜面作成とゲーム本体を管理する。

## スコープ

### 含める

- `Assets/` 配下を `Game/`, `NoteEditor/`, `Editor/`, `Tests/`, `ThirdParty/` の 5 ドメインに再編
- `Assets/Game/Scripts/` をフィーチャーグループ (`Songs/`, `Gameplay/`, `UI/`, `Video/`, `Persistence/`) に分割
- NoteEditor を `Assets/NoteEditor/` に持ち込み、Unity 6000.5.6f1 でコンパイルが通る状態にする (UniRx の更新、deprecated API の修正を含む)
- 不要なディレクトリの削除 (`Material/`, `NuGet/`, `Simple Scene Fade Load System/`, `Screenshots/`, `StreamingAssets/` のうち不要分)
- プロジェクトルートのログファイル (`build.log`, `build_out.log`, `build_run.log`, `test-run.log`) を `Logs/` へ移動
- `.gitignore` で上記ログを再生成されないよう調整

### 含めない

- NoteEditor の機能追加・変更・動作検証 (別フェーズ)
- ゲーム本体の機能変更 (直前のリファクタの完成状態を維持)
- NoteEditor 内コードの近代化リファクタ (Unity 6 で動く最低限に留める)

## 新しいディレクトリ構造

```text
Assets/
├── Game/                    FlashBeat ゲーム本体
│   ├── Scenes/              ゲームシーン (Opening, Title, Select, Game, Result, Option, MakeFile, Typing)
│   ├── Scripts/             ゲームスクリプト (フィーチャー別)
│   │   ├── Songs/           GManager, SongData
│   │   ├── Gameplay/        Notes, NotesManager, Judge, LaneFlash, BGFlash, MusicManager
│   │   ├── UI/              各 SceneManager (UI 制御)
│   │   └── Persistence/     SaveLoadManager
│   ├── Prefabs/             ゲーム用プレハブ (Screen, Note, Background 等)
│   ├── Art/                 マテリアル・テクスチャ・スプライト
│   ├── Resources/           ランタイムロード (Musics/, Videos/, <曲名>.json, <曲名>_text.json)
│   └── Fonts/               日本語フォント
├── NoteEditor/              統合する譜面エディタ (setchi/NoteEditor から移植)
│   ├── Scenes/
│   ├── Scripts/
│   ├── Art/                 マテリアル・テクスチャ
│   ├── Audio/               wav 音源 (NoteEditor 専用)
│   ├── Prefabs/
│   ├── Shaders/
│   └── Editor/              NoteEditor 専用エディタ拡張
├── Editor/                  FlashBeat 用エディタ拡張 (BuildScript, JapaneseFontFixer)
├── Tests/                   テスト (EditMode / PlayMode)
├── AudioMixer/              既存 (維持)
└── ThirdParty/              サードパーティ
    └── TextMesh Pro/        既存 (維持)
```

## 移動・整理ルール

### `Assets/Game/` への集約

| 現在 | 移動先 |
|---|---|
| `Assets/Scripts/GManager.cs`, `SongData` 関連 | `Assets/Game/Scripts/Songs/` |
| `Assets/Scripts/Notes.cs`, `NotesManager.cs`, `Judge.cs`, `LaneFlash.cs`, `BGFlash.cs`, `MusicManager.cs` | `Assets/Game/Scripts/Gameplay/` |
| `Assets/Scripts/OpeningSceneManager.cs`, `TitleSceneManager.cs`, `SelectSceneManager.cs`, `GameSceneManager.cs`, `ResultSceneManager.cs`, `OptionSceneManager.cs`, `MakeFileSceneManager.cs`, `TypingSceneManager.cs` | `Assets/Game/Scripts/UI/` |
| `Assets/Scripts/SaveLoadManager.cs` | `Assets/Game/Scripts/Persistence/` |
| `Assets/Art/Prefabs/*` | `Assets/Game/Prefabs/` |
| `Assets/Prefab/YoutubePlayer.prefab`, `YoutubePlayer 1.prefab` | `Assets/Game/Prefabs/` |
| `Assets/Art/Materials/*` | `Assets/Game/Art/Materials/` |
| `Assets/Fonts/Japanese/*` | `Assets/Game/Fonts/Japanese/` |

### 削除 (または中身が空ならフォルダごと削除)

- `Assets/Material/` (前回のリファクタで中身が空)
- `Assets/NuGet/`, `Assets/NuGet.config`, `Assets/packages.config` (Unity プロジェクトで NuGet は不要、誤配置)
- `Assets/Simple Scene Fade Load System/` (既に `SimpleFadeSystem` 等として組み込み済み)
- `Assets/Screenshots/` (デバッグ用一時ファイル、リポジトリに含めない)
- `Assets/Prefab/` (中身の prefab を `Assets/Game/Prefabs/` へ移動した後、空になったフォルダを削除)
- `Assets/Animations/` (中身が空であれば削除)
- `Assets/Images/` (中身が空であれば削除)
- `Assets/StreamingAssets/` (中身が空であれば削除)

### 維持 (位置変更なし)

- `Assets/Editor/` → そのまま (BuildScript.cs, JapaneseFontFixer.cs)
- `Assets/Tests/` → そのまま
- `Assets/AudioMixer/` → そのまま
- `Assets/Resources/` → `Assets/Game/Resources/` (Unity は名前が `Resources` のフォルダをすべてランタイムロード対象にするため、パス指定は変わらない)
- `Assets/TextMesh Pro/` → `Assets/ThirdParty/TextMesh Pro/` (移動するが中身は変更しない)

## NoteEditor 統合

### 取り込み元

- リポジトリ: https://github.com/setchi/NoteEditor
- ライセンス: MIT (FlashBeat に取り込み可能)。元リポジトリの `LICENSE` を `Assets/NoteEditor/LICENSE` として同梱する。
- 取得方法: `git clone` で最新版を取得し、`Assets/NoteEditor/` にコピー (履歴は破棄、setchi の commit 情報は取り込まない)
- 固定バージョン: 取得時点の commit SHA を `Assets/NoteEditor/IMPORT.md` に記録する (履歴を破棄するため、どの版を取り込んだかを追える形で残す)

### ディレクトリ再編

NoteEditor の標準的なフォルダ名を FlashBeat の命名規則に揃える:

| NoteEditor 標準 | NoteEditor 内 FlashBeat 命名 |
|---|---|
| `Assets/Materials/` | `Assets/NoteEditor/Art/Materials/` |
| `Assets/Textures/` | `Assets/NoteEditor/Art/Textures/` |
| `Assets/Prefabs/` | `Assets/NoteEditor/Prefabs/` |
| `Assets/Scripts/` | `Assets/NoteEditor/Scripts/` |
| `Assets/Scenes/` | `Assets/NoteEditor/Scenes/` |
| `Assets/Sounds/` | `Assets/NoteEditor/Audio/` |
| `Assets/Shaders/` | `Assets/NoteEditor/Shaders/` |
| (Editor スクリプト) | `Assets/NoteEditor/Editor/` |

### Unity 6 互換性修正

NoteEditor は Unity 2019.1.5f1 ベースのため、以下を最低限修正する:

- UniRx を UPM 経由で最新版 (または modern fork) に差し替え
- `FindObjectOfType` → `FindAnyObjectByType` (Unity 6 で deprecated)
- `FindObjectsOfType` → `FindObjectsByType`
- その他 Unity 6 で消えた API の置換 (必要に応じて個別対応)

### asmdef 構成

NoteEditor を取り込む際、`Assets/NoteEditor/NoteEditor.asmdef` を新規作成し、UniRx を参照させる。FlashBeat 本体 (`FlashBeat.Editor.asmdef`, `FlashBeat.Tests.asmdef`) との独立性を保つ。

NoteEditor 内に取り込み後に editor 専用スクリプトがあれば `Assets/NoteEditor/Editor/NoteEditor.Editor.asmdef` を別途作成する。

## プロジェクトルート整理

- `build.log`, `build_out.log`, `build_run.log`, `test-run.log` を `Logs/` へ移動
- `.gitignore` に以下を追加:
  ```
  /build*.log
  /test-run.log
  ```
- 既に追跡済みのログファイルは `git rm --cached` で追跡解除

## 不変条件 (維持されるもの)

- 曲データの構造 (`SongData[]` 配列、8 フィールド)
- ゲームシーン名・GUID・参照 (リファクタ後も同じシーンを開くことができる)
- 既存テスト (`Assets/Tests/Editor/SongDataTests.cs`, `JudgeLogicTests.cs`) のパスと内容
- `Hiscore` の保存キー (`"Hiscore"`) と CSV 形式、長さ 42
- `Resources/<曲名>.json`, `Resources/<曲名>_text.json`, `Resources/Musics/<曲名>`, `Resources/Videos/UnityYoutube/<曲名>.mp4` のランタイムロードパス (新構造でも `Resources/...` でアクセス可能)
- YouTube 動画プレイヤー経路 (InvidiousVideoPlayer 主系 + mp4 フォールバック)
- ノーツ判定ロジック・譜面生成・シーン遷移の挙動

## 影響範囲とリスク

### 高リスク

- **GUID 維持**: シーンやプレハブが参照するアセットの GUID を維持するため、Unity の `AssetDatabase.MoveAsset` を使う。失敗すると手動での `.meta` 編集が必要になる。
- **asmdef 影響**: 既存 asmdef (現状 `FlashBeat.Editor.asmdef` と `FlashBeat.Tests.asmdef`) は維持しつつ、NoteEditor 用に新規 asmdef (`Assets/NoteEditor/NoteEditor.asmdef`) を追加する。NoteEditor スクリプトが既存 asmdef に含まれていないか確認し、必要なら分離する。
- **UniRx 依存**: UniRx は NoteEditor のみが使い、FlashBeat 本体では使わないため、asmdef の参照スコープを `Assets/NoteEditor/` に限定する。UPM 経由で `com.neuecc.unirx` または modern fork (`com.github.denis5354.unirx`) を導入する。選定は取り込み時の最新安定版に従う。バージョンによって NoteEditor のコードに差分が出る可能性があるため、取得直後にコンパイルチェックを行う。

### 中リスク

- **NoteEditor の wav 依存**: FlashBeat は mp4/YouTube なので、NoteEditor 内の AudioClip 関連はそのまま動くが、FlashBeat の譜面データ (JSON) と互換性がない。NoteEditor の保存形式を理解し、必要なら FlashBeat 形式へのエクスポート機能を将来追加する (今回はスコープ外)。
- **TextMesh Pro の衝突**: NoteEditor も TextMesh Pro を使う可能性があり、`Assets/ThirdParty/TextMesh Pro/` との参照関係を確認する。

### 低リスク

- ディレクトリ移動のみなので、内容は変更されない。

## 実装順序

1. ベースラインを記録 (現在の構造、テスト結果、コンソール状態)
2. NoteEditor の取得 (プロジェクト外の作業用ディレクトリに `git clone https://github.com/setchi/NoteEditor`)
3. NoteEditor を取り込み (Unity の `AssetDatabase.ImportAsset` で `.meta` を生成)
4. Unity 6 互換性修正 (UniRx 追加、deprecated API 修正)
5. NoteEditor 用 asmdef 作成、FlashBeat asmdef との分離確認
6. コンパイル成功 + NoteEditor の各シーンがエラーなく開けることを確認
7. `Assets/Game/` 構造を段階的に構築:
   a. 先にディレクトリを作って移動 (`AssetDatabase.MoveAsset`)
   b. スクリプトをフィーチャーグループに分類して移動
   c. アセット (Prefabs, Art, Fonts) を移動
8. 不要ディレクトリの削除 (`Material/`, `NuGet/`, `Screenshots/`, 空ディレクトリ)
9. プロジェクトルートのログファイルを `Logs/` へ移動、`.gitignore` 更新
10. すべての EditMode テスト実行 (10/10 維持)
11. 全シーンロード検証
12. Windows ビルド

## 完了条件

- `Assets/` のトップレベルが `Game/`, `NoteEditor/`, `Editor/`, `Tests/`, `ThirdParty/`, `AudioMixer/` のみになっている
- NoteEditor の全シーンが Unity 6000.5.6f1 で開ける
- NoteEditor のコードが Unity 6 でコンパイルエラーなく通る
- 既存の 10 個の EditMode テストがすべてパスする
- Windows ビルドが成功する
- 既存のゲームシーンが全て正常ロードする
- リポジトリルートに `*.log` ファイルが残っていない

## 既知の不整合 (今回スコープ外)

- NoteEditor の譜面保存形式と FlashBeat の譜面形式 (`Data` JSON) の互換性 (取り込み後に実際の保存形式を確認する)
- NoteEditor の AudioClip ベース設計と FlashBeat の mp4/YouTube ベースの統合方法
- NoteEditor の UniRx 依存を将来外す近代化リファクタ
- NoteEditor の macOS / WebGL 対応 (NoteEditor 自体は Unity Editor ツールとして動く)
- NoteEditor の最新化 (本仕様では取得時点 HEAD を固定)
