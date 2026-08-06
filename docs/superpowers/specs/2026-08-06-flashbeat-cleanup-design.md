# FlashBeat プロジェクト構造クリーンアップ + NoteEditor 統合

## 目的

FlashBeat の `Assets/` 直下の散らかったディレクトリ構成をドメイン単位で整理し、譜面作成ツール NoteEditor (https://github.com/setchi/NoteEditor) をこのプロジェクトに直接統合する。

統合の動機は2つ:
1. **構造的理解の促進**: 18 個のディレクトリが `Assets/` 直下に並列配置されており、何がどこに何のためにあるか把握しにくい。
2. **開発体験の改善**: NoteEditor を別プロジェクトとして維持する煩雑さを解消し、1 つのリポジトリで譜面作成とゲーム本体を管理する。

## スコープ

### 含める

- `Assets/` 配下を `Game/`, `NoteEditor/`, `Editor/`, `Tests/`, `ThirdParty/`, `AudioMixer/` に再編
- `Assets/Game/Scripts/` をフィーチャーグループ (`Songs/`, `Gameplay/`, `UI/`, `Persistence/`, `Common/`) に分割
- NoteEditor を `Assets/NoteEditor/` に持ち込み、Unity 6000.5.6f1 でコンパイルが通る状態にする (UniRx の更新、deprecated API の修正を含む)
- 不要なディレクトリの削除 (`Material/`, `Screenshots/`、および中身を移動した後に空になったフォルダ)
- 削除可否が不明なもの (`Packages/` の NuGet DLL 群、`StreamingAssets/`) は検証したうえで判断する
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
│   ├── Scenes/              ゲームシーン (Opening, Title, Select, Game, Result, Option, MakeFile, Typing) + Legacy/
│   ├── Scripts/             ゲームスクリプト (フィーチャー別)
│   │   ├── FlashBeat.asmdef 既存 asmdef (配下すべてを 1 アセンブリとして含む)
│   │   ├── Songs/           GManager, SongData
│   │   ├── Gameplay/        Notes, NotesManager, Judge, LaneFlash, BGFlash, MusicManager, VideoTime
│   │   ├── UI/              各 SceneManager (UI 制御)
│   │   ├── Persistence/     SaveLoadManager
│   │   └── Common/          SelfDestroy, SimpleTransition
│   ├── Prefabs/             ゲーム用プレハブ (Note, Background, YoutubePlayer 等)
│   ├── Art/                 マテリアル・画像
│   │   ├── Materials/
│   │   └── Images/          曲ジャケット画像・アイコン
│   ├── Animations/          アニメーションクリップ・コントローラ (Combo, Judge, Fade, PanelOpen)
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
    ├── TextMesh Pro/        既存 (移動のみ)
    └── Simple Scene Fade Load System/  Initiate.Fade を提供 (移動のみ)
```

## 移動・整理ルール

### `Assets/Game/` への集約

| 現在 | 移動先 |
|---|---|
| `Assets/Scripts/FlashBeat.asmdef` | `Assets/Game/Scripts/FlashBeat.asmdef` |
| `Assets/Scripts/GManager.cs` | `Assets/Game/Scripts/Songs/` |
| `Assets/Scripts/Notes.cs`, `NotesManager.cs`, `Judge.cs`, `LaneFlash.cs`, `BGFlash.cs`, `MusicManager.cs`, `VideoTime.cs` | `Assets/Game/Scripts/Gameplay/` |
| `Assets/Scripts/OpeningSceneManager.cs`, `TitleSceneManager.cs`, `SelectSceneManager.cs`, `GameSceneManager.cs`, `ResultSceneManager.cs`, `OptionSceneManager.cs`, `MakeFileSceneManager.cs`, `TypingSceneManager.cs` | `Assets/Game/Scripts/UI/` |
| `Assets/Scripts/SaveLoadManager.cs` | `Assets/Game/Scripts/Persistence/` |
| `Assets/Scripts/SelfDestroy.cs`, `SimpleTransition.cs` | `Assets/Game/Scripts/Common/` |
| `Assets/Scenes/*` (`Legacy/` 含む) | `Assets/Game/Scenes/` |
| `Assets/Art/Prefabs/*` | `Assets/Game/Prefabs/` |
| `Assets/Prefab/YoutubePlayer.prefab`, `YoutubePlayer 1.prefab` | `Assets/Game/Prefabs/` |
| `Assets/Art/Materials/*` | `Assets/Game/Art/Materials/` |
| `Assets/Images/*` | `Assets/Game/Art/Images/` |
| `Assets/Animations/*` | `Assets/Game/Animations/` |
| `Assets/Resources/*` | `Assets/Game/Resources/` |
| `Assets/Fonts/Japanese/*` | `Assets/Game/Fonts/Japanese/` |

`SongData` クラスは `GManager.cs` 内に定義されているため、ファイル分割はせずそのまま `Songs/` へ移動する。

### 削除

- `Assets/Material/` (中身が空。確認済み)
- `Assets/Screenshots/` (デバッグ用一時ファイル、リポジトリに含めない)
- `Assets/Prefab/` (中身の prefab を `Assets/Game/Prefabs/` へ移動した後、空になったフォルダを削除)
- `Assets/Art/`, `Assets/Fonts/`, `Assets/Scripts/`, `Assets/Scenes/`, `Assets/Images/`, `Assets/Animations/`, `Assets/Resources/` (中身をすべて移動した後、空になったフォルダを削除)

### 要調査 (削除可否を検証してから判断)

以下は「不要そうに見えるが、消すとビルドが壊れる可能性がある」ため、**削除前に検証ステップを挟む**。

- `Assets/Packages/` (112 ファイル)、`Assets/NuGet/`、`Assets/NuGet.config`、`Assets/packages.config`
  - 実体は NuGet で復元された DLL 群 (`YoutubeExplode 6.4.0` と依存の `AngleSharp`, `System.Text.Json` 等)。
  - `Assets/` 配下と `Packages/` 配下の `.cs` / `.asmdef` を検索した限り、`YoutubeExplode` / `AngleSharp` を参照するコードは**見つかっていない** (YouTube 再生は UPM の `com.ibicha.youtube-player` 3.3.1 が担当)。
  - 検証手順: 一時的に退避 → コンパイル + Windows ビルド + GameScene 再生確認 → 問題なければ削除を確定。壊れた場合は戻して維持する。
- `Assets/StreamingAssets/mp4.mp4` (133 バイト = Git LFS ポインタ)
  - 参照元を検索し、未参照なら `StreamingAssets/` ごと削除。参照があれば維持する。

### 維持 (位置変更なし)

- `Assets/Editor/` → そのまま (BuildScript.cs, JapaneseFontFixer.cs)
- `Assets/Tests/` → そのまま
- `Assets/AudioMixer/` → そのまま

### 移動のみ (中身は変更しない)

- `Assets/Resources/` → `Assets/Game/Resources/` (Unity は名前が `Resources` のフォルダをすべてランタイムロード対象にするため、`Resources.Load` のパス指定は変わらない)
- `Assets/TextMesh Pro/` → `Assets/ThirdParty/TextMesh Pro/`
- `Assets/Simple Scene Fade Load System/` → `Assets/ThirdParty/Simple Scene Fade Load System/` (`SimpleFadeSystem.asmdef` も一緒に移動する)

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
- **asmdef 影響**: 現状 4 つの asmdef がある。
  - `Assets/Scripts/FlashBeat.asmdef` (参照: `Unity.TextMeshPro`, `SimpleFadeSystem`, `YoutubePlayer`) → `Assets/Game/Scripts/FlashBeat.asmdef` へ一緒に移動する。asmdef は配下のサブフォルダをすべて含むため、`Songs/`, `Gameplay/`, `UI/`, `Persistence/` に分割しても追加の asmdef は不要。
  - `Assets/Editor/FlashBeat.Editor.asmdef` → 位置変更なし
  - `Assets/Tests/Editor/FlashBeat.Tests.asmdef` → 位置変更なし
  - `Assets/Simple Scene Fade Load System/SimpleFadeSystem.asmdef` → このフォルダを削除すると `FlashBeat.asmdef` の参照が壊れるため、削除対象から外す (下記「削除」の項を参照)

  これに加えて NoteEditor 用に `Assets/NoteEditor/NoteEditor.asmdef` を新規作成する。
- **UniRx 依存**: UniRx は NoteEditor のみが使い、FlashBeat 本体では使わないため、asmdef の参照スコープを `Assets/NoteEditor/` に限定する。UPM 経由で `com.neuecc.unirx` または modern fork (`com.github.denis5354.unirx`) を導入する。選定は取り込み時の最新安定版に従う。バージョンによって NoteEditor のコードに差分が出る可能性があるため、取得直後にコンパイルチェックを行う。

### 中リスク

- **NoteEditor の wav 依存**: FlashBeat は mp4/YouTube なので、NoteEditor 内の AudioClip 関連はそのまま動くが、FlashBeat の譜面データ (JSON) と互換性がない。NoteEditor の保存形式を理解し、必要なら FlashBeat 形式へのエクスポート機能を将来追加する (今回はスコープ外)。
- **TextMesh Pro の衝突**: NoteEditor も TextMesh Pro を使う可能性があり、`Assets/ThirdParty/TextMesh Pro/` との参照関係を確認する。

### 低リスク

- ディレクトリ移動のみなので、内容は変更されない。

## 実装順序

1. **作業ツリーを綺麗にする**: 現在 40 以上の未コミット変更 (`.editorconfig`, 各 asmdef, TextMesh Pro のアップグレード差分, ログファイル等) が残っている。大量の `MoveAsset` を行うと差分の切り分けが不可能になるため、先にコミットするか意図的に破棄して `git status` を空にする。
2. ベースラインを記録 (現在の構造、テスト結果 10/10、コンソール状態)
3. NoteEditor の取得 (プロジェクト外の作業用ディレクトリに `git clone https://github.com/setchi/NoteEditor`)
4. NoteEditor を取り込み (Unity の `AssetDatabase.ImportAsset` で `.meta` を生成)
5. Unity 6 互換性修正 (UniRx 追加、deprecated API 修正)
6. NoteEditor 用 asmdef 作成、FlashBeat asmdef との分離確認
7. コンパイル成功 + NoteEditor の各シーンがエラーなく開けることを確認
8. `Assets/Game/` 構造を段階的に構築 (各ステップごとにコンパイル確認 + コミット):
   a. 先にディレクトリを作って `AssetDatabase.MoveAsset` で移動
   b. スクリプトをフィーチャーグループに分類して移動 (`FlashBeat.asmdef` も一緒に)
   c. アセット (Scenes, Prefabs, Art, Images, Animations, Resources, Fonts) を移動
   d. サードパーティ (TextMesh Pro, Simple Scene Fade Load System) を `ThirdParty/` へ移動
9. 不要ディレクトリの削除 (`Material/`, `Screenshots/`, 空になったフォルダ)
10. 要調査項目の検証 (`Assets/Packages/` の NuGet DLL 群、`StreamingAssets/mp4.mp4`) — 退避してビルドが通るか確認し、通れば削除、通らなければ戻して維持
11. プロジェクトルートのログファイルを `Logs/` へ移動、`.gitignore` 更新
12. すべての EditMode テスト実行 (10/10 維持)
13. 全シーンロード検証
14. Windows ビルド

## 完了条件

- `Assets/` のトップレベルが `Game/`, `NoteEditor/`, `Editor/`, `Tests/`, `ThirdParty/`, `AudioMixer/` になっている (検証の結果 `Packages/` が必要と判明した場合はこれも残る)
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
- `GManager` の曲データが 7 本の並列配列 (`SongName`, `Musician`, `SongURL`, `SBPM`, `Slevel`, `Shit`, `SongLong`) を `BuildSongs()` で `SongData[]` に組み立てる構造のままである点。前回のリファクタで `Songs[]` を唯一の定義にする予定だったが、並列配列が実質的な source of truth として残っている。今回は移動のみで構造は変えない。
