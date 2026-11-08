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
        /// **true** (2026-11-08): design 契約 §6 と実装の照合が終わり、
        /// 契約どおりに描ける状態になったので有効化する。
        /// - レーン中心線 8 本 (下端 x=339 step=150 / 上端 x=801 step=18、色 #FFFFFF2E sw2)
        /// - 判定線 (y=792 h=4 #EDEDED) と判定グロウ (y=796 h=2 #FFFFFF4D)
        /// - ノーツは uGUI の遠近法描画 (3D メッシュは隠す)
        ///
        /// 有効時は旧 3D プレイフィールド (Lane / JudgeLine / LaneLights) を隠す。
        /// </summary>
        public static bool Playfield = true;

        /// <summary>
        /// 動画を減光 (design の Video Dim #00000080 全画面) するか。
        /// 減光は「動画 → 減光 → プレイフィールド → HUD」の順の Canvas で行う。
        /// </summary>
        public static bool VideoDim = true;

        /// <summary>
        /// v6 HUD のレーン別判定行 (画面下の PERFECT/GREAT/MISS) を使うか。
        /// 有効時は 3D の判定エフェクト (Judge の判定プレハブ) を隠す。
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
