# FlashBeat リファクタリング設計

## 目的

FlashBeat の既存ゲームプレイとローカルmp4動画再生を維持したまま、曲データの重複管理、アセット配置、機能名の不統一、明らかなファイル名の誤りを整理する。

対象は中規模のリファクタリングとし、譜面判定や演出の仕様変更、入力システムの置き換えは行わない。

## 動画再生の前提

プロジェクトの動画再生は **YouTube ストリーミング（`InvidiousVideoPlayer` 経由で Invidious 公開インスタンスから動画を取得）** が本来の主系である。

`GameScene` 内の動画プレイヤーオブジェクト（`0`〜`26`）には `InvidiousVideoPlayer` コンポーネントと `VideoPlayer` が紐付いており、各 `InvidiousVideoPlayer.VideoId` に YouTube 動画IDが割り当てられている。スペースキー押下時に `YPlayer[songID].GetComponent<InvidiousVideoPlayer>().PlayVideoAsync()` で再生を開始する。

ローカル mp4 ファイル（`Assets/Resources/Videos/UnityYoutube/<曲名>.mp4`）はオフライン用の補助コンテンツとして `VPlayer[songID].m_VideoClip` に割り当てられている。Invidious 経由の YouTube ストリーミングが何らかの理由で失敗した際のフォールバック、または Invidious サーバーが利用できない環境での動作確認用として残す。`MusicManager` は YouTube 再生を試み、失敗した場合のみ mp4 にフォールバックする。

`Assets/Scripts/YoutubePlayy.cs` はかつて `UnityWebRequest` で YouTube 動画URLを直接取得しようとしていた古い経路で、YouTube 側の仕様変更により既に機能していない。今回のリファクタリングではこのファイルを削除し、`InvidiousVideoPlayer` 経由のストリーミングに完全に置き換える。

`GManager.SongURL`（=`videoId`）はサムネイル取得と `InvidiousVideoPlayer.VideoId` の双方で参照される。mp4 再生には影響しない（mp4 再生はシーン側で `m_VideoClip` に紐づく）。

## 維持する不変条件

- 曲ID 0 は `noSong` のプレースホルダーとして残す。
- 実際の選曲範囲は曲ID 1〜26とする。
- 曲IDと `GameScene` の動画プレイヤーオブジェクト（`0`〜`26`）の対応を変更しない。
- 各 VideoPlayer に割り当てられたローカルmp4ファイルを変更しない。
- `Resources/<曲名>.json`、`Resources/<曲名>_text.json`、`Resources/Musics/<曲名>`、`Resources/Videos/UnityYoutube/<曲名>.mp4` のパスを変更しない。
- `Hiscore` の保存キーとCSV形式を変更しない。既存の42件分の容量も維持する。
- ノーツの生成、入力判定、スコア計算、画面遷移の挙動を変更しない。
- TextMesh Pro本体および外部パッケージのアセットは移動・改名しない。

## 曲データの一本化

### 現状の問題

`GManager` は曲名、作曲者、YouTube動画ID、BPM、難易度、総ノーツ数、曲長を7本の並列配列で保持し、静的初期化時に別の `List<SongData>` へコピーしている。配列の追加・削除時に各行の対応を手動で維持する必要があり、データの唯一性がない。

### 変更内容

`SongData[]` を曲情報の唯一の定義にする。

```csharp
public static readonly SongData[] Songs
```

`SongData` のフィールドは次の構成にする。

- `id`
- `title`
- `musician`
- `videoId`（YouTubeサムネイル取得用に保持）
- `bpm`
- `level`
- `totalHits`
- `duration`

現在 `youtubeUrl` と呼ばれている値は11文字のYouTube動画IDで、サムネイル取得でのみ参照されるため `videoId` のままフィールド名を変えるが用途を明確化する。

`GetSong(int id)` は配列を参照する共通アクセスポイントとして残し、無効なIDには `null` を返す。`totalSong` は固定値ではなく `Songs.Length - 1` から求める。

### 参照側の更新

以下のスクリプトでは、必要な曲を `GManager.GetSong(GManager.songID)` または対象IDで取得し、個別の並列配列を参照しないようにする。

- `MusicManager.cs`
- `GameSceneManager.cs`
- `NotesManager.cs`
- `SelectSceneManager.cs`
- `OptionSceneManager.cs`
- `ResultSceneManager.cs`
- `BGFlash.cs`
- `TipingSceneManager.cs`
- `MakeFileSceneManager.cs` の追加手順と関連参照

`MusicManager` の `VPlayer` はシーン上の `VideoPlayer` を保持する既存のInspector配列として残す（mp4 フォールバック用）。`YPlayer` はシーン上の `YoutubePlayer` GameObject（`InvidiousVideoPlayer` 付き）を保持する Inspector 配列として残し、YouTube ストリーミングの主経路として使用する。

### テスト

`SongDataTests` を配列構成に合わせて更新し、次を検証する。

- 曲データが空でない。
- 曲IDと配列インデックスが一致する。
- `Songs[0]` がプレースホルダーである。
- 曲ID 1 のタイトルが既存値である。
- `GetSong` の有効・無効IDの挙動。
- `totalSong` が実曲数と一致する。
- `videoId` を含む曲データの対応が移行前と一致する。

## 動画再生経路の復元と整理

YouTube ストリーミング（`InvidiousVideoPlayer` 経路）が正しく機能する状態を維持しつつ、ローカル mp4 をフォールバックとして残す。

- `Assets/Prefab/YoutubePlayer.prefab` と `Assets/Prefab/YoutubePlayer 1.prefab` を保持し、各 `InvidiousVideoPlayer` の `VideoId` が `GManager.SongURL` と一致していることを確認する。
- `GameScene` の動画プレイヤー子オブジェクト（`0`〜`26`）に `InvidiousVideoPlayer` コンポーネントが付与され、各 `VideoPlayer.m_PlayOnAwake=0` / `m_Url=""` が設定されていることを確認する。
- `GameScene` の `InvidiousInstance` GameObject を保持する。
- `MusicManager` は `YPlayer[songID]` の `InvidiousVideoPlayer` を主系として再生を試み、失敗した場合のみ `VPlayer[songID].Play()` にフォールバックする実装に統一する。
- `Assets/Scripts/FlashBeat.asmdef` の `YoutubePlayer` 参照を保持する。
- `Packages/manifest.json` の `com.ibicha.youtube-player` への参照を保持する。
- `Assets/Scripts/YoutubePlayy.cs` を削除する（既に機能していない古い UnityWebRequest 経路のため）。

`MusicManager.Start()` の `VPlayer.Stop()` 呼び出しは事前状態のリセットのため残し、`Update()` の Space 押下時に YouTube ストリーミング（`InvidiousVideoPlayer.PlayVideoAsync()`）を開始し、失敗時にのみ `VPlayer.Play()` にフォールバックする。

## アセットの整理

UnityのAssetDatabase経由で移動・改名し、GUID参照を維持する。

### ディレクトリ

```text
Assets/
├─ Art/
│  ├─ Materials/
│  └─ Prefabs/
├─ Fonts/
│  └─ Japanese/
├─ Scenes/
├─ Scripts/
├─ Tests/
└─ Resources/
```

現在の `Assets/Material` は `Assets/Art/Materials` へ、現在の `Assets/Prefab` は `Assets/Art/Prefabs` へ移動する。

以下のルート直下アセットは `Assets/Fonts/Japanese` へ移動する。

- `NotoSansJP-Medium.ttf`
- `NotoSansJP-Medium SDF.asset`
- `YuGothB.ttc`
- `YuGothB SDF.asset`
- `japanese_full.txt`

`Assets/Resources` はランタイムのロードパスを壊さないため移動しない。

### マテリアル名

| 現在 | 変更後 |
|---|---|
| `ゲーム背景.mat` | `GameBackground.mat` |
| `ノーツ.mat` | `Note.mat` |
| `パーティクル.mat` | `Particle.mat` |
| `ライト.mat` | `LaneLight.mat` |
| `レーン.mat` | `Lane.mat` |
| `判定線.mat` | `JudgeLine.mat` |
| `背景.mat` | `Background.mat` |
| `黒半透明.mat` | `BlackSemiTransparent.mat` |

### プレハブ名

| 現在 | 変更後 |
|---|---|
| `スクリーン.prefab` | `Screen.prefab` |
| `ノーツ.prefab` | `Note.prefab` |
| `Image (1).prefab` | `ScreenVideo.prefab` |
| `Image (1) 1.prefab` | `FullscreenOverlay.prefab` |
| `BackGround.prefab` | `Background.prefab` |
| `Particle System.prefab` | `ParticleSystem.prefab` |
| `Directional Light.prefab` | `DirectionalLight.prefab` |

既に意味が明確な `Canvas`、`Bad`、`Great`、`Perfect`、`Miss`、`Hiscore`、`SongInfoPanel`、`YoutubePlayer` は改名しない。

## シーンとHierarchy名

表示される曲名・日本語ラベルは変更せず、機能を表すHierarchy名だけを英語化する。

主な変更対象は次のとおり。

| 現在 | 変更後 |
|---|---|
| `ライト` | `LaneLights` |
| `ライト1`〜`ライト8` | `LaneLight1`〜`LaneLight8` |
| `スコアゲージ` | `ScoreGauge` |
| `判定線` | `JudgeLine` |
| `レーン` | `Lane` |
| `問題` | `QuestionText` |
| `ふりがな` | `FuriganaText` |
| `ローマ字` | `RomajiText` |
| `スクリーン` | `Screen` |
| `サムネイル` | `Thumbnail` |

曲名をHierarchy名として使用している表示用オブジェクトは、表示内容との混同を避けるため今回変更しない。

`Assets/Scenes/TipingScene].unity` は `Assets/Scenes/TypingScene.unity` に改名する。対応するスクリプトも `TipingSceneManager.cs` から `TypingSceneManager.cs` に改名し、クラス名を一致させる。

`Assets/Scenes/tttt.unity` は参照とBuild Settingsを確認した後、削除せず `Assets/Scenes/Legacy/` に移動する。不要であることを別途確認できた場合のみ、将来の作業で削除する。

## C#の整理

### スクリプト名

`Light.cs` は Unity標準の `UnityEngine.Light` と紛らわしいため、`LaneFlash.cs` に改名し、クラス名を `LaneFlash` にする。

`Lanesnum`、`alfa` などの内部名は `laneNumber`、`alpha` などに整理する。Inspectorに保存されているフィールド名を変更する場合は `FormerlySerializedAs` を使い、既存シーンの値を保持する。

### 追加の整理

- `GameSceneManager.cs` に残る実行されないコメントアウトコードを削除する。
- `MakeFileSceneManager.cs` の曲追加手順を `SongData[]` と動画ID設定に合わせる。
- `BuildScript.cs` の実在しない `OpeningScene.unity` 参照を `Opening.unity` に修正する。
- 変更対象の主要スクリプトでは、意味の不明瞭なローカル変数名を挙動を変えない範囲で整理する。
- 入力判定やノーツ計算のロジックは書き換えない。

## 実装順序

1. Unity Editorで現状のコンパイル状態、コンソール、EditModeテストを記録する。
2. YouTube 動画経路（`InvidiousVideoPlayer` 経路）が正しく機能する状態であることを確認する。
    - `GameScene` の動画プレイヤー子オブジェクト（`0`〜`26`）に `InvidiousVideoPlayer` が付与されていることを確認。
    - `InvidiousInstance` GameObject が存在することを確認。
    - `YoutubePlayer.prefab` / `YoutubePlayer 1.prefab` の `InvidiousVideoPlayer.VideoId` が `GManager.SongURL` と一致することを確認。
    - `MusicManager` の `YPlayer` 経路が主系として機能することを確認。
    - `Assets/Scripts/YoutubePlayy.cs` を削除。
3. `GManager` を `SongData[]` の単一データ源へ変更する。
4. 全ランタイム参照とEditModeテストを更新する。
5. コンパイル、コンソール、テストを確認する。
6. `LaneFlash` と `TypingSceneManager` のファイル・クラス名を移行する。
7. `BuildScript` とコメントアウトされた死んだコードを整理する。
8. AssetDatabaseでアセットを移動・改名する。
9. シーンファイル名とHierarchy名を整理する。
10. シーン参照、Build Settings、Prefab参照を検証する。
11. EditModeテスト、シーン検証、Windowsビルドを実行する。
12. `GameScene` で複数の曲IDを選択し、Space押下で YouTube ストリーミングが再生され（mp4 フォールバック時のみローカル mp4）、ゲーム終了後にResultSceneへ遷移することを確認する。

## 完了条件

- 曲情報の定義が `SongData[]` の一箇所だけになっている。
- 曲ID 0〜26のタイトル、動画ID、BPM、難易度、総ノーツ数、曲長が移行前と一致する。
- 既存のセーブデータを読み込める。
- Unityのコンパイルエラーがない。
- EditModeテストがすべて成功する。
- Build Settingsの全シーンが有効なパスを指している。
- Windowsビルドが成功する。
- GameSceneで `YPlayer[songID].GetComponent<InvidiousVideoPlayer>().PlayVideoAsync()` が呼び出され、Invidious 経由で YouTube 動画が再生される（複数の曲IDで確認）。失敗時のみ `VPlayer[songID].Play()` にフォールバックする。
- YouTubeプレイヤー関連のコンポーネント・プレハブ・パッケージ参照がコードとシーンから正しく維持されている。

## 既知の不整合（今回スコープ外）

- 曲ID 10 の `title` は `チュートリアル` だが、対応する mp4 は `Evanescent.mp4`、YouTube動画ID `NAeuRhLaqaQ` も `Evanescent` を指している。タイトル改名が必要かは別タスクで判断する。
- 曲ID 0 の `noSong` プレースホルダーには対応する mp4 が `Major.mp4` として存在する。実害はないためこのリファクタリングでは触らない。
- Invidious 公開インスタンス（例: `yewtu.be`, `invidious.snopyta.org`）は可用性が変動する。YouTube ストリーミング経路を本番運用する場合は、セルフホスト Invidious を前提にするか、複数インスタンスのフェイルオーバー仕組みを別途設計する必要がある。

## 対象外

- 曲情報のScriptableObject化
- 動画バックエンドの追加・変更
- 譜面フォーマットの変更
- 入力システムの置き換え
- UIレイアウトや演出のデザイン変更
- TextMesh Proや外部パッケージの整理（`com.ibicha.youtube-player` パッケージは YouTube ストリーミングの基盤なので保持）
- Invidious 公開インスタンスの可用性対策（フェイルオーバー、セルフホスト等）
- `tttt.unity` の完全削除
- 曲ID 10 のタイトル整合
