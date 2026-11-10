using System;
using UnityEngine;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// v6 UI の解像度と配置を 1 箇所に集約するプロファイル。
    ///
    /// 契約 (Owner 指示):
    /// - UI はスケールしない (scale = 1 のまま)。テキストやアイコンを
    ///   引き伸ばして崩れる根源を型のレベルで断つため、本プロファイルは
    ///   スケール因子を一切持たない。
    /// - 解像度はユーザーが選ばない。ゲーム側が画面の実解像度
    ///   (<see cref="Screen.width"/> × <see cref="Screen.height"/>) から
    ///   自動判定する (解像度設定は増設しない)。
    /// - ワークフレームは design 共通フレーム (rw7zx) の 1728×972 固定。
    ///   どの解像度でも同じピクセルサイズで 1:1 表示する (縮尺を変えない)。
    /// - フレームを置く位置 (origin) だけが解像度ごとに違い、画面から自動で決まる。
    ///   origin の式: ((w - 1728) / 2, (h - 972) / 2) の整数切り捨て。
    /// - 1920×1080 / 2560×1440 / 2560×1664 以外の解像度では、最も近い
    ///   プロファイルのレイアウトを 1:1 のまま使う (拡大縮小しない)。
    ///   origin だけを実画面から計算した中央配置値に差し替える。
    ///
    /// 値の出所: docs/design/v6/flashbeat-v6-nodes.json の
    /// "Resolution Check · Title" フレーム (id POJwG) 内の実配置
    /// (ref "01 Title · v6" の x/y/w/h、scale なし)。
    /// </summary>
    public readonly struct V6UiProfile : IEquatable<V6UiProfile>
    {
        /// <summary>design 共通ワークフレームの幅 (rw7zx)。</summary>
        public const float DesignFrameW = 1728f;

        /// <summary>design 共通ワークフレームの高さ (rw7zx)。</summary>
        public const float DesignFrameH = 972f;

        /// <summary>プロファイル名 (例 "FHD 1920x1080")。</summary>
        public readonly string Name;

        /// <summary>対象画面の実解像度幅 (px)。</summary>
        public readonly int ScreenW;

        /// <summary>対象画面の実解像度高さ (px)。</summary>
        public readonly int ScreenH;

        /// <summary>ワークフレーム幅 (px)。常に <see cref="DesignFrameW"/> (= 1728)。</summary>
        public readonly float FrameW;

        /// <summary>ワークフレーム高さ (px)。常に <see cref="DesignFrameH"/> (= 972)。</summary>
        public readonly float FrameH;

        /// <summary>
        /// 画面左上原点でのフレーム左上の位置 (px)。
        /// design の "Resolution Check" の実配置と一致する。
        /// </summary>
        public readonly Vector2 Origin;

        private V6UiProfile(string name, int screenW, int screenH, Vector2 origin)
        {
            Name = name;
            ScreenW = screenW;
            ScreenH = screenH;
            FrameW = DesignFrameW;
            FrameH = DesignFrameH;
            Origin = origin;
        }

        /// <summary>
        /// design の "Resolution Check" に登録されたプロファイル表 (読み取り専用)。
        /// FHD / 2K / MacBook Air M5 の 3 件。順序は design 記載順。
        /// </summary>
        public static readonly V6UiProfile[] Known =
        {
            new V6UiProfile("FHD 1920x1080", 1920, 1080, new Vector2(96f, 54f)),
            new V6UiProfile("2K 2560x1440", 2560, 1440, new Vector2(416f, 234f)),
            new V6UiProfile("MacBook 2560x1664", 2560, 1664, new Vector2(416f, 346f)),
        };

        /// <summary>
        /// 画面解像度に対応するプロファイルを返す。
        /// 完全一致 (1920×1080 / 2560×1440 / 2560×1664) ならそのプロファイル。
        /// 未登録の解像度なら最も近いプロファイル (画面寸法が近い順) を返す。
        /// いずれの場合も FrameW/FrameH は 1728×972 のまま (スケールしない)。
        /// 未知解像度の <see cref="Origin"/> は実画面から計算した中央配置値
        /// ((w - 1728) / 2, (h - 972) / 2 の切り捨て) を使う。
        /// </summary>
        public static V6UiProfile For(int screenW, int screenH)
        {
            var index = NearestIndex(screenW, screenH);
            var known = Known[index];
            var origin = OriginFor(screenW, screenH);
            return new V6UiProfile(known.Name, screenW, screenH, origin);
        }

        /// <summary>
        /// 画面解像度に最も近い <see cref="Known"/> のインデックス。
        /// 近さは |w 差| + |h 差| (マンハッタン距離)。同点は表の先を優先。
        /// </summary>
        public static int NearestIndex(int screenW, int screenH)
        {
            var best = 0;
            var bestDist = int.MaxValue;
            for (var i = 0; i < Known.Length; i++)
            {
                var dist = Math.Abs(Known[i].ScreenW - screenW) + Math.Abs(Known[i].ScreenH - screenH);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// フレーム origin の式: ((w - 1728) / 2, (h - 972) / 2) の整数切り捨て。
        /// <see cref="V6Frame.OriginFor"/> と同一の式であり、両者は必ず一致する
        /// (EditMode テスト V6UiProfileTests で固定)。
        /// </summary>
        public static Vector2 OriginFor(int screenW, int screenH)
        {
            return new Vector2(
                Mathf.FloorToInt((screenW - DesignFrameW) / 2f),
                Mathf.FloorToInt((screenH - DesignFrameH) / 2f));
        }

        public bool Equals(V6UiProfile other)
        {
            return Name == other.Name
                && ScreenW == other.ScreenW
                && ScreenH == other.ScreenH
                && FrameW.Equals(other.FrameW)
                && FrameH.Equals(other.FrameH)
                && Origin.Equals(other.Origin);
        }

        public override bool Equals(object obj)
        {
            return obj is V6UiProfile other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Name != null ? Name.GetHashCode() : 0;
                hash = (hash * 397) ^ ScreenW;
                hash = (hash * 397) ^ ScreenH;
                hash = (hash * 397) ^ Origin.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{Name} (screen {ScreenW}x{ScreenH}, frame {FrameW}x{FrameH}, origin {Origin})";
        }
    }
}
