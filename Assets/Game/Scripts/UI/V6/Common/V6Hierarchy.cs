using System.Collections.Generic;
using UnityEngine;
using FlashBeat.UI.V5;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// v6 のコード生成 UI が共通で使う階層操作。
    ///
    /// 「ゾーンの中身を作り直す」ときは必ずここを通す。破棄は
    /// <see cref="V5GameUi.DestroyNow"/> (再生中はフレーム末の Destroy) なので、
    /// 破棄だけに任せると **同じフレーム中に古い子と新しい子が二重に残る**。
    /// 監査 (Graphic / TMP_Text の列挙)、レイアウト、Find 系が 2 世代を数えて
    /// しまい、Option の実機監査が pass=359 / 181 で揺れる原因になっていた。
    ///
    /// 破棄を予約する前に階層から外すことで、古い世代はその場で見えなくなる
    /// (実体の破棄は Unity に任せるので、再生中の Destroy 制約は守る)。
    /// </summary>
    public static class V6Hierarchy
    {
        /// <summary>parent の直下の子を全て、その場で階層から外して破棄を予約する。</summary>
        public static void ClearChildren(GameObject parent)
        {
            if (parent == null)
            {
                return;
            }

            ClearChildren(parent.transform);
        }

        /// <summary>parent の直下の子を全て、その場で階層から外して破棄を予約する。</summary>
        public static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            // 反復中に階層が変わるので、いったんリストへ写してから外す。
            var children = new List<GameObject>();
            foreach (Transform child in parent)
            {
                children.Add(child.gameObject);
            }

            for (int i = 0; i < children.Count; i++)
            {
                children[i].transform.SetParent(null, false);
                V5GameUi.DestroyNow(children[i]);
            }
        }
    }
}
