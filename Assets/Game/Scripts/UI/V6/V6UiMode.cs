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
        /// **既定は false**。既存の 3D レーン (角度・判定ライン・キー入力時の
        /// レーン光) はもともと安定して動いているため、design に合わせる理由で
        /// 差し替えない。オンにすると旧 3D プレイフィールドは隠される。
        /// </summary>
        public static bool Playfield = false;

        /// <summary>
        /// 動画を減光 (design の Video Dim #00000080) するか。
        /// 既定は false: 映像の明るさを元のまま保つ (減光すると 3D レーンも
        /// 暗くなり、元の見え方を崩すため)。
        /// </summary>
        public static bool VideoDim = false;

        /// <summary>
        /// v6 HUD のレーン別判定行 (画面下の PERFECT/GREAT/MISS) を使うか。
        /// 既定は false: 判定のフィードバックは元の 3D エフェクト
        /// (Judge が Instantiate する判定プレハブ) のままにする。
        /// </summary>
        public static bool VerdictRow = false;

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
