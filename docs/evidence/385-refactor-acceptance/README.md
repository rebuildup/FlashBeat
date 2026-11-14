# #385 リファクタリング前後の画面比較 (最終受入証跡)

## これは何の証拠か

#385 (v6 UI 共通部品抽出 + View 分割 + 旧 UI 整理、PR #470〜#493) は
「**挙動・見た目を変えない**」ことが契約である。このディレクトリは
**リファクタリング直前と直後の実機ビルドで全画面を撮り、ピクセル比較した
受入証跡**である。

| 側 | commit | ビルド |
|---|---|---|
| before | `a98307d2` (PR #469 merge、#385 最初の PR #470 の直前) | この Mac で `BuildScript.BuildStandaloneMacOS` を batchmode 実行 (Unity 6000.5.6f1) |
| after | `c7a5a04e` (origin/main、PR #493 merge) | 同上。CI artifact `flashbeat-macos-62c6d662…` (run 43595874744) は `62c6d662` ビルドで、`62c6d662..c7a5a04e` の差分は `docs/audit/open-issues-audit.md` のみ (Assets/ProjectSettings/Packages 無変更) のためコード同一 |

撮影環境: macOS 実機 (この Mac)、両ビルドとも同一マシン・同一ユーザー
(= 同一 PlayerPrefs / 同一メディアキャッシュ / 同一ネットワーク) で実行。

## 使ったハーネス (全て両 commit に同一内容で存在)

| モード | 引数 | 内容 |
|---|---|---|
| 全画面監査+撮影 | `-autotest v6screens` | Title / Select (本体・空・設定・履歴・ランキング) / Option 4 タブ / Result (+登録モーダル) / GameScene (HUD・Pause・Loading) の計 15 枚を撮り、フレーム収まり・レガシー混在・Unicode 記号の機械チェックを行う (`AutoTestV6Screens`) |
| 解像度行列 | `script/mac-resolution-matrix.sh` | Opening / Title / Select / Option を 1024x768・1280x720・1920x1080・2560x1440 で撮る (#96/#241 ハーネス) |
| 操作検証 | `-autotest v6interact` | スライダドラッグ・Result 段階表示と Space スキップ・再開カウントダウン・Select キャッシュの実操作 PASS/FAIL |
| 実プレイ完走 | `-autotest v6songend` | 最短曲 (ツイッターランド) を自動判定で最後までプレイし Result まで到達 |

比較は `tools/` の Swift 製ピクセル差分ツールで実施
(`pngdiff.swift` = 全画面 diff 画素数、`pngdiffgrid.swift` / `pngdiffascii.swift` =
差分の空間分布、`pngdiffregion.swift` / `pngstats.swift` / `pngcolprof.swift` /
`pngshift.swift` = 領域別解析)。

**ツールごとの差分判定しきい値** (RGB 3ch 絶対差の合計に対する閾値):

| ツール | しきい値 | 備考 |
|---|---|---|
| `pngdiff.swift` | 引数指定 (既定 0)。`compare.sh` は 0 を渡す | **本証跡の diff% は全てこの条件**: RGB 3ch 合計差 > 0 の画素を「差分画素」とした。つまり 1 チャネルでも 1 でも違えば差分 |
| `pngdiffregion.swift` | 引数指定 (既定 30) | 領域限定の集計。領域検証では 0 を明示指定したものを記載 |
| `pngdiffgrid.swift` | 固定 30 | 空間分布の可視化用 (セル内 diff%) |
| `pngdiffascii.swift` | 固定 60 | ASCII ヒートマップ用 |
| `pngdiffbbox.swift` | 引数指定 (既定 30) | 差分 bbox / row/col band |
| `pngshift.swift` | 固定 90 | シフト頑健性チェック用 |
| `pngstats.swift` / `pngcolprof.swift` | — | 統計量のみ (差分判定なし) |

したがって「ピクセル完全一致 (diff=0)」は **RGB ピクセル値の完全一致**
(PNG ファイルのバイト列一致ではない) を意味する。

## 結果サマリ

### 機械チェック (操作・状態): 全て PASS、after が悪化した項目なし

| ハーネス | before | after |
|---|---|---|
| v6screens @2560x1440 | pass=46 fail=0 symbols=70 shots=15 | pass=46 fail=0 symbols=70 shots=15 (run1/run2 とも) |
| v6screens @1920x1080 | pass=46 fail=0 symbols=68 shots=15 | pass=46 fail=0 symbols=70 shots=15 |
| v6interact @2560x1440 | pass=46 **fail=1** (下記注) ×2 回再現 | pass=47 fail=0 |
| v6songend @2560x1440 | pass=32 fail=0 (judged=130==chartNotes, Completed) | pass=32 fail=0 (同) |

**v6interact の before FAIL について**: 「Select 再訪で chart meta を
再読み込みしない」(#377 キャッシュ) が before で `chartMeta=26→54` (+28)、
after で `54→54` (+0)。`V6SelectDataSource` の差分は doc comment のみで
ロジック同一 (#385 の分割は verbatim)。原因は **リモートカタログの
chartVersion が cacheKey に入る** (`remoteId@chartVersion`) ことと、
初回訪問時点でリモート曲が未到着だった環境差。after が「直った」のではなく
カタログ到着タイミングの違い。before/after とも再訪での jacket/chartExists
再読み込みは 0 で、キャッシュ機構自体は両 build で正常。

### ピクセル比較 @2560x1440 (v6screens、15 画面)

**ピクセル完全一致 (diff=0)**:
GameScene-HUD / OptionScene (4 タブ全て) / SelectScene 本体 /
SelectScene-Empty / SelectScene-Settings

**差分あり → 全て動的要因と特定** (後述):
TitleScene 3.56% / SelectScene-History 3.15% / SelectScene-Ranking 2.09% /
ResultScene 6.65% / ResultScene-RegisterModal 5.63% /
GameScene-Loading 4.99% / GameScene-Pause 0.43%

### ピクセル比較 @解像度行列 (4 解像度 × 4 シーン)

| シーン | 1024x768 | 1280x720 | 1920x1080 | 2560x1440 |
|---|---|---|---|---|
| OpeningScene | **0** | **0** | **0** | **0** |
| OptionScene | **0** | **0** | **0** | **0** |
| TitleScene | 3.26% | 3.59% | 3.78% | 5.29% |
| SelectScene | 0.58% | 0.26% | 2.13% | 4.69% |

Opening / Option は全解像度でピクセル完全一致。Title / Select の差分は
背景動画 (`背景アニメーションタイトル.mp4` 等) とジャケット読み込みタイミング
(後述)。AutoShot はシーンを 6 秒間隔で巡回するため、v6screens より
アニメーション位相のばらつきが大きい。

## 差分の切り分け (動的 vs 退行)

### 判定に使った決定的証拠

1. **同一ビルド 2 回撮影 (after-run1 vs after-run2)**:
   ResultScene 6.42% / RegisterModal 5.63% / SelectScene-History 3.46% /
   SelectScene-Ranking 2.35% / TitleScene 6.11% が **同じビルドの連続
   2 回でこれだけずれる** = これらの画面には実行毎に変わる動的要素がある。
   一方 GameScene-HUD / Option 全タブ / Select 本体・Empty・Settings は
   同一ビルドでは 0 diff (完全に決定的)。

2. **before == after-run2 が RGB ピクセル値で完全一致**:
   SelectScene 本体 / Empty / Settings / GameScene-HUD / Option 全タブは
   before (a98307d2) と after (c7a5a04e) の画像が **閾値 0 で
   RGB ピクセル値完全一致** (diff=0/3,686,400)。リファクタリングが
   描画を変えていないことの直接証明。

3. **GameScene-Loading の動画フレーム説を同一ビルド再現で確認**:
   `v6screens-game-only-before-run2` と `-run3` (同じ before ビルドで
   GameScene のみ 2 回撮影) で Loading が **3.43% ずれた**。
   ローディング画面の左側領域は `Resources/Videos/loading.mp4` を
   VideoPlayer → RenderTexture → RawImage で表示しており、撮影時点の
   再生位置で画が変わる。UI クローム (カード枠・進捗・文字) は一致し、
   差分は動画面に限定される (領域解析で確認)。
   なお before-run1 と run3 は一致、run2 だけ別フレーム = 起動毎の
   デコード/再生タイミングでフレームが 1〜数コマずれる。

4. **TitleScene / ResultScene / SelectScene(行列) の背景動画**:
   `Assets/Game/Resources/Videos/` に `背景アニメーションタイトル.mp4` /
   `背景アニメーション結果.mp4` / `背景アニメーション3.mp4` /
   `オープニング.mp4` があり、各シーンの VideoPlayer がループ再生する。
   起動からの経過時間でフレームが変わるため、before/after で数%の差分が
   出るのは必然。OptionScene は全画面を不透明 UI が覆うため 0 diff。

5. **History / Ranking の差分はリモートデータ**:
   `rankingMode=Online` でクラウドのランキング/履歴を表示する。
   before 側の計測後に after 側の `v6songend` がスコアを登録するなど、
   実行間でリモートデータが更新されるため差分が出る。
   機械チェック (フレーム収まり等) は両 build で fail=0。

6. **v6songend の判定数差 (perfect 127+great2 vs 129+0)**:
   自動判定のタイミングジッタ。スコア計算経路 (Judge/GManager) は
   #385 の変更範囲外で、両 build とも `judged=130==chartNotes`・
   `songEndState=Completed`・pass=32 fail=0。

### 動的領域と固定UIの分離検証 (@2560x1440、閾値0)

差分のあった 7 画面について、`pngdiffregion.swift` (閾値0) と
`pngdiffgrid.swift` (32×18 セル) で差分の空間分布を調べ、
**同一ビルド連続実行 (after-run1 vs after-run2) の差分率**と比較した。
固定UIがリファクタリングで変わっていれば、before↔after の差分は
同一ビルド実行間のばらつきを超えて特定領域に集中するはずである。

| 画面 | before↔after | 同一ビルド after1↔after2 | 判定 |
|---|---|---|---|
| TitleScene | 3.56% | **6.11%** | 同一ビルドの方が大きい。差分は下半分 (y≥800) に集中し上部はほぼ 0。背景動画 (`Screen` quad, CameraFarPlane) の再生位相が支配的 |
| ResultScene | 6.65% | 6.42% | 同程度。差分は背景動画の露出マージン部。中央パネル内部は下記の通り微小 |
| ResultScene-RegisterModal | 5.63% | 5.63% | 同一ビルドと完全に同じ差分率。モーダル背後の動画 + 微小差のみ |
| SelectScene-History | 3.15% | 3.46% | 同一ビルドの方が大きい。リモート履歴データ行の更新が原因 |
| SelectScene-Ranking | 2.09% | 2.35% | 同上 (リモートランキング) |
| GameScene-Loading | 4.99% | 0% | 左側動画領域に集中 (下記)。after 2 回は動画フレームまで一致 |
| GameScene-Pause | 0.43% | 0% | 2 箇所のみ (下記)。片方は before 同一ビルドでも 2 状態、もう片方は before-full の一時状態で before game-only/after と画素一致 |

**領域限定 diff の実測値** (before ↔ after-run2、特記なき限り閾値0):

- `ResultScene` パネル内部 (880,320)-(1840,880): diff=1,189/537,600 (**0.22%**)、
  同一ビルド after1↔after2 でも 0.12% — 段階表示アニメーションの位相差と同程度。
  閾値30では before↔after 0.09% / 同一ビルド 0.04%。
- `ResultScene-RegisterModal` 中央 (640,240)-(1840,880): 閾値30で
  diff=33/768,000 (0.004%、実質 0)。閾値0の 0.50% は全て微小な RGB 差
  (mean=0.014) で、同一ビルドでも同じ 0.50% が出る =
  動画のかすかな透け/微差であり構造差ではない。
- `SelectScene-History` 中段 (0,480)-(2560,960): 2.28%。同一ビルドでも 2.62%
  — この領域自体が実行毎に変わるリスト内容であり、固定UIではない。
- `SelectScene-Ranking` 中段 (0,400)-(2560,880): 1.02%。同一ビルドでも 1.19%。
- `GameScene-Loading` 動画領域 (0,0)-(640,1120): **25.34%** と差分が集中。
  残り (640,0)-(2560,1440) は 0.08% で、その殆どは (880,800)-(1200,880) と
  (1280,720)-(1440,800) の進捗表示系の小領域。**この 2 領域は同一 before
  ビルドの game-only run2/run3 間でも同じように変動する** (run2 vs run3 で
  5.28% / 7.98%) ことを確認済み = 動画フレーム位相に連動する動的要素。
- `GameScene-Pause` 全画面 diff=15,895 画素の内訳:
  - x1280-1440, y160-320 の 1 要素 (2,299 画素): **同一 before ビルドでも
    2 状態が観測される** — game-only run2 では meanRGB=16.0 (before-full と
    同じ暗い状態)、run3 では meanRGB=22.6 (after と同じ明るい状態)。
    アニメーション/表示タイミングの位相差。
  - x1280-1600, y1080-1260 の広い領域 (13,544 画素、ただし mean=0.4 の
    微小差): before-full だけがこの状態を撮っており、**同一 before ビルドの
    game-only run2/run3、および after-run1/run2 とは全て一致する**
    (game-only before run2 ↔ after-run2 でこの領域 diff=0)。
    つまり before/after の描画はこの領域でピクセル一致しており、
    before-full の撮影が一時的な途中状態を捉えただけ。

**結論**: 全ての差分画面で、before↔after の差分率は同一ビルド連続実行の
ばらつきと同程度かそれ以下であり、領域限定 diff でも固定UI領域
(不透明パネル内部・モーダル中央・HUD) に構造的な差分は検出されなかった。
差分は (a) 背景/ローディング動画の再生位相、(b) リモートデータ、
(c) カウントダウン/進捗等のアニメーション位相、に帰着する。

### 結論

- **UI クローム (レイアウト・文字・色・状態) はリファクタリングで
  1 ピクセルも変わっていない** — ジャケットなし画面・Option・HUD・
  Select 各状態でピクセル完全一致を確認。差分のあった画面でも、
  上記の領域限定検証により固定UI領域の差分が同一ビルド実行間の
  ばらつきの範囲内であることを確認した。
- 観測された全ての差分は、(a) 同梱背景/ローディング動画の再生位相、
  (b) リモートランキング/履歴データの実行間更新、(c) Result の段階表示
  アニメーション位相、のいずれかに帰着し、**同一ビルドの連続実行でも
  同程度に発生する**ことを確認済み。受入条件の「アニメーション中の差は
  除く」に該当し、**意図しないピクセル差分・レイアウト差・操作退行は
  検出されなかった**。

## 再現方法

```sh
# before 側 (a98307d2) / after 側 (c7a5a04e) それぞれの worktree で:
Unity -batchmode -quit -projectPath <wt> -executeMethod BuildScript.BuildStandaloneMacOS

# 全画面監査+撮影 (例: 2560x1440)
<app>/Contents/MacOS/FlashBeat -autotest v6screens \
  -autotest-out <out> -screen-fullscreen 1 -screen-width 2560 -screen-height 1440

# 解像度行列
script/mac-resolution-matrix.sh <app> <out>

# 操作検証 / 実プレイ完走 (画面引数は v6screens と同じ)
<app>/Contents/MacOS/FlashBeat -autotest v6interact \
  -autotest-out <out> -screen-fullscreen 1 -screen-width 2560 -screen-height 1440
<app>/Contents/MacOS/FlashBeat -autotest v6songend \
  -autotest-out <out> -screen-fullscreen 1 -screen-width 2560 -screen-height 1440

# ピクセル比較 (tools/ はこのディレクトリに同梱)
swift tools/pngdiff.swift before.png after.png
```

## ファイル構成

| ディレクトリ | 内容 |
|---|---|
| `v6screens-2560x1440-before/` | before フル実行 (15 PNG + result.txt) |
| `v6screens-2560x1440-after-run1/` | after 1 回目 (動的差分の確認用) |
| `v6screens-2560x1440-after-run2/` | after 2 回目 (ピクセル一致証明の比較対象) |
| `v6screens-1920x1080-{before,after}/` | 1920x1080 での同ハーネス |
| `v6screens-game-only-before-run2|run3/` | before ビルドの GameScene のみ再撮影 (動画位相の同一ビルド再現証拠) |
| `interact-{before-run1,before-run2,after}/` | 操作検証 3 回分 |
| `songend-{before,after}/` | 実プレイ完走 (PNG 5 枚 + result.txt 各) |
| `matrix-{before,after}/` | 解像度行列 4 解像度 × 4 シーン (summary.txt 付き) |
| `tools/` | ピクセル差分・領域解析に使った Swift スクリプトと compare.sh |

`Player.log` は容量のため収録していない (機械チェック結果は
`autotest-*-result.txt` に集約済み)。
