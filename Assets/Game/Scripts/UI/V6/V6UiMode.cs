namespace FlashBeat.UI.V6
{
    /// <summary>
    /// v6 UI と旧 (v5) UI の切替 (Issue #223 統合担当)。
    ///
    /// 移植が済んだ画面だけ v6 を有効にする。未移植の画面 (Pause / Loading /
    /// Title / Result / Option) は旧 UI のまま動かし、v6 と旧 UI の混在自体は
    /// 移植の進め方を遅らせる理由にしない。
    ///
    /// true のとき: v6 の Game HUD / Select 画面が旧 UI の代わりに作られる。
    /// </summary>
    public static class V6UiMode
    {
        /// <summary>ゲーム画面の HUD とプレイフィールドを v6 で描画する。</summary>
        public static bool GameHud = true;

        /// <summary>選曲画面を v6 の 3 カラムで描画する。</summary>
        public static bool SelectScreen = true;

        /// <summary>
        /// v6 の遠近法プレイフィールド (8 レーンの透視描画) を使うか。
        ///
        /// **false** (2026-11-08): オーナー判断による差し戻し。ゲームプレイの
        /// レーンは元の 3D プレイフィールドに戻す (SRAM commit a156da3 と同じ状態、
        /// Issue #233)。false の間は v6 プレイフィールドは生成されない。
        /// </summary>
        public static bool Playfield = false;

        /// <summary>
        /// 動画を減光 (design の Video Dim #00000080 全画面) するか。
        /// 減光は「動画 → 減光 → プレイフィールド → HUD」の順の Canvas で行う。
        ///
        /// **false** (2026-11-08): 3D レーン前提の既存挙動に戻す (減光しない)。
        /// </summary>
        public static bool VideoDim = false;

        /// <summary>
        /// v6 HUD のレーン別判定行 (画面下の PERFECT/GREAT/MISS) を使うか。
        /// 有効時は 3D の判定エフェクト (Judge の判定プレハブ) を隠す。
        ///
        /// **true** (2026-11-11): オーナーレビュー #379 で v6 の判定行を有効化した。
        /// 3D 側の判定表示とは二重に出さない (UpdateLaneVerdicts がクローンを隠す)。
        /// </summary>
        public static bool VerdictRow = true;

        /// <summary>
        /// Title 中段のタブ行 (▷ GameStart / History / Ranking / Option) を描くか。
        ///
        /// **true** (2026-11-11): 正本 QJtTv ではタブ行 (FHcxk) が表示され、
        /// フッター右下の操作ボタンゾーン (T8HuQS) は `enabled: false` で非表示。
        /// 2026-11-08 にタブ行を消した根拠「フッターの START / 設定と二重」は、
        /// 正本で非表示のゾーンをこちらの都合で描いていたことによる逆転だった。
        /// 正本に合わせてタブ行を出し、フッター右下は描かない。
        /// </summary>
        public static bool TitleTabs = true;

        /// <summary>
        /// Title 左下のキー凡例行 (Space 決定 / ↑↓ 選択 / E 終了) を描くか。
        ///
        /// **true** (2026-11-11): 正本 QJtTv のフッター左 (QQ4jM) は表示されている。
        /// 上と同じ理由で 2026-11-08 の非表示指示を撤回する。
        /// </summary>
        public static bool TitleLegend = true;

        /// <summary>
        /// Title のフッター右下 (設定 / START) を描くか。
        /// 正本 QJtTv ではゾーンごと `enabled: false`。タブ行が同じ操作を持つ。
        /// </summary>
        public static bool TitleFooterActions = false;

        /// <summary>
        /// Title のヘッダーに "FlashBeat" の文字を描くか。
        /// 正本 QJtTv の ci867 は `enabled: false` (Exit ボタンと重なるため)。
        /// </summary>
        public static bool TitleHeaderTitle = false;

        /// <summary>
        /// 旧 (v5) の常設オーバーレイを v6 画面で出さない。
        ///
        /// 対象と理由:
        /// - RankingPanel の「F5 ランキング」ヒント … 正本のどの画面にも無い。
        /// - UpdateChecker の「更新を適用して再起動」ボタン … 正本の更新導線は
        ///   Title ヘッダー (D7gbLV) のみ。Option に出す旧ボタンは全シーンへ残り、
        ///   HUD のキーガイドに重なる。
        /// - MediaCacheOptionPanel / KeyConfigOptionButton (OptionScene のみ生成) …
        ///   v6 Option が同じ操作 (データタブのキャッシュ / Play タブのキー設定) を
        ///   持つため、旧パネルは設計に無い二重表示になる。
        /// </summary>
        public static bool LegacyOverlaysHidden = true;

        /// <summary>
        /// 旧キーガイド (F キー一覧パネル) を隠す。
        /// v6 の HUD は画面下部にキーガイド行を持つので、旧パネルが重なると読めなくなる。
        /// </summary>
        public static void HideLegacyKeyGuide()
        {
            var guides = UnityEngine.Object.FindObjectsOfType<KeyGuideUI>();
            for (int i = 0; i < guides.Length; i++)
            {
                if (guides[i] != null)
                {
                    UnityEngine.Object.Destroy(guides[i].gameObject);
                }
            }

            // 名前_opts の補助オブジェクト (タイトル画面の "KeyGuideHint" など) も消す。
            var all = UnityEngine.Object.FindObjectsOfType<UnityEngine.RectTransform>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                string n = all[i].gameObject.name;
                if (n == "KeyGuideUI" || n == "KeyGuideHint" || n == "KeyGuide Panel")
                {
                    UnityEngine.Object.Destroy(all[i].gameObject);
                }
            }
        }

        /// <summary>
        /// v6 タイトルモードで旧 (v5) タイトル UI を非表示にする。
        /// 対象名の判定は <see cref="FlashBeat.UI.V6.Title.V6TitleLogic.IsLegacyTitleObjectName"/>
        /// (TitleScene.unity 実測: 旧 UI はシーン配置 "Canvas" 配下にあるので、原則は
        /// "Canvas" を非表示にすれば消える。名前での列挙は実機で効かなかった場合の保険)。
        /// v6 が無効なときは呼ばない (旧 UI がそのまま動く)。
        /// 戻り値: 常に true (=「この API は毎フレーム呼んでよい」の合図)。
        /// 旧 v5 TitleView は Start/OnEnable で自前のメニューを後から生成するので、
        /// 1 回だけでは足りない。呼び出し側 (V6TitleViewDriver.Update) は毎フレーム呼ぶ。
        /// </summary>
        /// <remarks>
        /// Issue #396: **この走査は現行コードでは何も見つけない。**
        /// <c>TitleSceneManager</c> のコメントどおり、v5 の <c>TitleView</c> (コード生成) は
        /// どこからも作られておらず、旧 UI はシーン配置の Canvas だけ
        /// (それは <c>TitleSceneManager.BuildV6Title</c> が 1 回隠す)。
        /// それでも走査を残すのは、v5 TitleView を再び使う変更が入ったときに
        /// 二重表示を黙って作らないため。ただし毎フレームは走らせず、
        /// v5 が Canvas を作ったとき (= 生成番号が変わったとき) と、
        /// 保険の定期走査のときだけにする。
        /// </remarks>
        public static bool HideLegacyTitleUi()
        {
            // Issue #396: 旧 v5 UI が新しく現れるのは、v5 が Canvas を作ったときだけ。
            // 生成は V5GameUi.NewCanvas を通るので、その回数が変わったときだけ
            // シーンを走査する。毎フレームの全 Transform 列挙をやめる。
            //
            // 保険として、変化が無くても一定フレームごとに 1 回だけ走査する
            // (登録を通らない生成経路が将来できても二重表示を残さない)。
            int generation = FlashBeat.UI.V5.V5GameUi.CanvasGeneration;
            bool shouldScan = generation != lastScannedGeneration
                || UnityEngine.Time.frameCount - lastScanFrame >= SafetyScanIntervalFrames;
            if (!shouldScan)
            {
                return true;
            }

            lastScannedGeneration = generation;
            lastScanFrame = UnityEngine.Time.frameCount;

            var transforms = UnityEngine.Object.FindObjectsByType<UnityEngine.Transform>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null) continue;
                if (!FlashBeat.UI.V6.Title.V6TitleLogic.IsLegacyTitleObjectName(t.gameObject.name)) continue;
                // v6 自身も "Logo" という名を使う (V6TitleView.BuildRoot)。v6 Canvas 配下の
                // "Logo" は v6 自分なので隠さない。それ以外の旧名は親 Canvas に関係なく隠す
                // (旧 v5 側が v6 Canvas 配下に生成した場合も消す)。
                if (IsUnderV6Canvas(t) && t.gameObject.name == "Logo") continue;
                if (t.gameObject.activeSelf) { t.gameObject.SetActive(false); }
            }
            return true;
        }

        /// <summary>最後に走査したときの v5 Canvas 生成番号 (Issue #396)。</summary>
        private static int lastScannedGeneration = -1;

        /// <summary>最後に走査したフレーム (保険の定期走査用)。</summary>
        private static int lastScanFrame = int.MinValue;

        /// <summary>
        /// 変化が無くても走査する間隔 (フレーム)。登録を通らない生成経路への保険。
        /// 60fps で約 2 秒に 1 回。毎フレームより 2 桁少ない。
        /// </summary>
        public const int SafetyScanIntervalFrames = 120;

        /// <summary>テスト用: 走査の記録を戻す。</summary>
        public static void ResetLegacyScanStateForAudit()
        {
            lastScannedGeneration = -1;
            lastScanFrame = int.MinValue;
            lastV5CanvasGeneration = -1;
            lastV5CanvasScanFrame = int.MinValue;
            lastUpdateNoticeScanFrame = -1;
        }

        /// <summary>
        /// テスト用: v5 の Canvas 生成番号。v5 が画面を作ると増える。
        /// 本番コードはこれを読むだけで、値の意味は
        /// <see cref="FlashBeat.UI.V5.V5GameUi.CanvasGeneration"/> が正本。
        /// </summary>
        public static int LegacyCanvasGeneration
        {
            get { return FlashBeat.UI.V5.V5GameUi.CanvasGeneration; }
        }

        /// <summary>テスト用: 登録と走査の記録をまとめて戻す。</summary>
        public static void ResetLegacyHidingForAudit()
        {
            FlashBeat.UI.V5.V5GameUi.ResetCreatedCanvasesForAudit();
            ResetLegacyScanStateForAudit();
        }

        /// <summary>
        /// テスト用: v5 の Canvas ファクトリを呼ぶ。v5 の画面生成と同じ登録経路を
        /// 通すので、隠す処理の「後から生成された Canvas を隠せるか」を
        /// 実際の生成経路で検証できる (<c>V5GameUi</c> は internal のため)。
        /// </summary>
        public static UnityEngine.Canvas CreateLegacyCanvasForAudit(string name)
        {
            return FlashBeat.UI.V5.V5GameUi.NewCanvas(name, 500);
        }

        /// <summary>親をたどって v6 の Canvas ("V6 ..." という名) 配下なら true。</summary>
        private static bool IsUnderV6Canvas(UnityEngine.Transform t)
        {
            var canvases = t.GetComponentsInParent<UnityEngine.Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                var c = canvases[i];
                if (c != null && c.gameObject.name.StartsWith("V6")) return true;
            }
            return false;
        }

        /// <summary>
        /// 旧 (v5) の更新通知バナー (UpdateNoticeUI のピル) を隠す。
        /// v6 タイトルは UpdateChecker.Model で自分用の更新表示を持つので、
        /// 旧バナーは二重表示になるだけ。**コンポーネント (FlashBeatUpdateNotice) は
        /// 無効化しない**: v6 の更新ボタンが UpdateNoticeUI.Trigger() を呼ぶため、
        /// 非表示にするのは見た目 (UpdateNoticeCanvas) だけ。
        /// UpdateNoticeUI は最初の Update でピルを生成するので、Start 時点では
        /// まだ無い → 見つかり次第隠す (戻り値 true で完了)。
        ///
        /// Issue #385 Phase 3: 走査は <see cref="UpdateNoticeScanIntervalFrames"/>
        /// フレームに 1 回までに絞る。バナーは数フレーム遅れても実害が無く、
        /// 見つかるまで毎フレーム <c>FindObjectsByType&lt;Canvas&gt;</c> を
        /// 呼ぶ必要はない。
        /// </summary>
        public static bool HideLegacyUpdateNotice()
        {
            int frame = UnityEngine.Time.frameCount;
            if (lastUpdateNoticeScanFrame >= 0
                && frame - lastUpdateNoticeScanFrame < UpdateNoticeScanIntervalFrames)
            {
                return false;
            }
            lastUpdateNoticeScanFrame = frame;

            var canvases = UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            bool found = false;
            for (int i = 0; i < canvases.Length; i++)
            {
                var c = canvases[i];
                if (c == null) continue;
                if (c.gameObject.name != "UpdateNoticeCanvas") continue;
                if (c.gameObject.activeSelf) { c.gameObject.SetActive(false); }
                found = true;
            }
            return found;
        }

        /// <summary>
        /// 旧 (v5) の Canvas / ルート ("V5 Title Canvas" / "V5 Select View" など) を
        /// すべて非表示にする。v6 モードでは v5 の描画を重ねない (二重表示防止)。
        ///
        /// Issue #396: 以前は毎フレーム **シーン全体の GameObject を列挙**していた
        /// (`FindObjectsByType<GameObject>`)。v5 の Canvas はすべて
        /// <see cref="FlashBeat.UI.V5.V5GameUi.NewCanvas"/> を通って生成されるので、
        /// 生成時に登録された名前だけを見れば全走査が要らない。
        ///
        /// 登録名は「v5 が作った名前」なので、v6 側の名前 ("V6...") には触らない。
        /// 破棄済みの Canvas は null として飛ばす (登録は名前だけなので残ってよい)。
        /// 冪等: 既に非アクティブなら何もしない。
        ///
        /// Issue #385 Phase 3: 呼び出し自体はドライバが毎フレーム行うが、
        /// 実際の走査は **v5 の Canvas 生成番号が変わったとき** と
        /// <see cref="SafetyScanIntervalFrames"/> ごとの保険だけに絞る
        /// (<see cref="HideLegacyTitleUi"/> と同じ方式)。登録済み Canvas が
        /// v5 側で再表示されても、最悪 2 秒程度で隠し直される。
        /// </summary>
        public static bool HideLegacyV5Canvases()
        {
            int generation = FlashBeat.UI.V5.V5GameUi.CanvasGeneration;
            bool shouldScan = generation != lastV5CanvasGeneration
                || UnityEngine.Time.frameCount - lastV5CanvasScanFrame >= SafetyScanIntervalFrames;
            if (!shouldScan)
            {
                return true;
            }

            lastV5CanvasGeneration = generation;
            lastV5CanvasScanFrame = UnityEngine.Time.frameCount;

            // Issue #396: 生成時に登録された Canvas 参照だけを見る。毎フレームの
            // シーン全列挙 (FindObjectsByType) も配列確保も起きない。
            var canvases = FlashBeat.UI.V5.V5GameUi.CreatedCanvases;
            for (int i = 0; i < canvases.Count; i++)
            {
                UnityEngine.Canvas canvas = canvases[i];
                if (canvas == null) continue; // 破棄済みは偽 null になる
                UnityEngine.GameObject go = canvas.gameObject;
                if (go == null) continue;
                if (go.activeSelf) go.SetActive(false);
            }
            return true;
        }

        /// <summary>最後に V5 Canvas を走査したときの生成番号 (Issue #385 Phase 3)。</summary>
        private static int lastV5CanvasGeneration = -1;

        /// <summary>最後に V5 Canvas を走査したフレーム (保険の定期走査用)。</summary>
        private static int lastV5CanvasScanFrame = int.MinValue;

        /// <summary>
        /// 更新通知バナーを探す走査の間隔 (フレーム)。バナーは急がないので
        /// <see cref="SafetyScanIntervalFrames"/> より短い間隔で十分。
        /// 見つかるまではこの間隔でだけ <c>FindObjectsByType</c> を呼ぶ
        /// (Issue #385 Phase 3: 毎フレームのシーン走査をやめる)。
        /// </summary>
        public const int UpdateNoticeScanIntervalFrames = 30;

        /// <summary>最後に更新通知バナーを走査したフレーム。</summary>
        private static int lastUpdateNoticeScanFrame = -1;

        /// <summary>
        /// Issue #396: 全走査をやめた後の後始末。登録に無い旧 v5 ルート
        /// (過去のビルドがシーンへ置いたもの等) を 1 回だけ掃除する。
        /// 毎フレームは呼ばない (起動時に 1 回)。
        /// </summary>
        public static void HideLegacyV5CanvasesOnce()
        {
            var roots = UnityEngine.Object.FindObjectsByType<UnityEngine.GameObject>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            for (int i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go == null) continue;
                if (!go.name.StartsWith("V5")) continue;
                if (go.activeSelf) go.SetActive(false);
            }
        }
    }
}
