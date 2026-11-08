using UnityEngine;

namespace FlashBeat.UI.V6.Result
{
    /// <summary>
    /// 登録 (提出) の方式。オンライン順位への送信と、端末内だけの保存を分ける
    /// (Issue #240)。
    ///
    /// v6 Result には 2 つの別々の契約がある:
    ///   - オンライン送信 (RankingClient)。対象はリモート曲だけで、応答は
    ///     201 / 429 / オフライン / その他の HTTP に区分される。
    ///   - ローカル保存 (端末内の名前)。既存の v5 は
    ///     <see cref="FlashBeat.UI.V5.ResultView"/> が PlayerPrefs キー
    ///     "V5PlayerName" に名前を書いていた。v6 Result は v5 の ResultView を
    ///     使わないため、この経路が到達不能になっていた。
    ///
    /// 判定と表示の語彙の純ロジックは <see cref="V6ResultLogic"/> が持ち、
    /// ここは方式のenum と PlayerPrefs への永続化だけを担う。
    /// </summary>
    public enum V6RegistrationMode
    {
        /// <summary>オンライン順位へ送信する (リモート曲)。</summary>
        Online,

        /// <summary>端末内だけに保存する (ローカル曲・送信対象外)。</summary>
        Local,
    }

    /// <summary>
    /// ローカル名の PlayerPrefs 永続化 (Issue #240)。キーは v5 と同じ
    /// "V5PlayerName" なので、v5 で保存済みの名前がそのまま初期値になる。
    /// </summary>
    public static class V6LocalNameStore
    {
        /// <summary>保存済みの名前 (未登録は空文字)。</summary>
        public static string Load()
        {
            try
            {
                return PlayerPrefs.GetString(V6ResultLogic.LocalPlayerNamePrefsKey, string.Empty);
            }
            catch (System.Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>名前を保存する (v5 と同じキー・同じ保存形式)。</summary>
        public static void Save(string name)
        {
            try
            {
                PlayerPrefs.SetString(V6ResultLogic.LocalPlayerNamePrefsKey, name ?? string.Empty);
                PlayerPrefs.Save();
            }
            catch (System.Exception)
            {
                // 制限された環境では保存できないが、画面の動作は続ける。
            }
        }

        /// <summary>保存済みの名前を消す (テスト用)。</summary>
        public static void Clear()
        {
            try
            {
                PlayerPrefs.DeleteKey(V6ResultLogic.LocalPlayerNamePrefsKey);
                PlayerPrefs.Save();
            }
            catch (System.Exception)
            {
                // 同上。
            }
        }
    }
}
