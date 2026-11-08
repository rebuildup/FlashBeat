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
        /// v6 の遠近法プレイフィールド (8 レーンの透视描画) を使うか。
        ///
        /// Issue #233 で true に変更: 旧 3D レーンの入力・判定 (Judge / Notes) は
        /// 触らないため動作は変わらない。描画だけが design の透視図になる。
        /// 有効時は旧 3D プレイフィールド (Lane / JudgeLine / LaneLight) を隠す。
        /// mac 実機で確認済み (2026-11-08 autotest: レーン・ノーツ・判定線の描画と完走)。
        /// </summary>
        public static bool Playfield = true;

        /// <summary>
        /// 動画を減光 (design の Video Dim #00000080) するか。
        /// Issue #233 で true に変更: プレイフィールドを v6 (uGUI) に統一した時点で
        /// 旧 3D レーンは描かれないため、減光の副作用 (3D レーンが暗くなる) は消える。
        /// </summary>
        public static bool VideoDim = true;

        /// <summary>
        /// v6 HUD のレーン別判定行 (画面下の PERFECT/GREAT/MISS) を使うか。
        /// Issue #233 で true に変更: mac 実機で判定行の描画を確認済み。
        /// 旧 3D 判定ポップアップは v6 HUD が読み取った後に非表示化される。
        /// </summary>
        public static bool VerdictRow = true;

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
    }
}
