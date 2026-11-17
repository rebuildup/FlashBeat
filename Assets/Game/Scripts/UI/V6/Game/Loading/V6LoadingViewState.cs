using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Game.Loading
{
    /// <summary>
    /// V6LoadingView の表示状態・操作 API (Issue #385 Phase 2.8 で分割)。
    /// ローディング / 失敗の切り替え、曲情報の反映、選曲へ戻る / 再試行を持つ。
    /// partial class のためフィールド・メソッドは V6LoadingView 本体と共有する。
    /// </summary>
    public sealed partial class V6LoadingView
    {
        /// <summary>ロード中の表示 (実データ: 曲情報は統合担当が渡す)。</summary>
        /// <param name="chartProgress">
        /// 譜面準備の実進捗 (0..1)。null は進捗不明。design に進捗の可視表現は
        /// 無い (Issue #606) ので値は状態として保持するだけで、専用 UI は出さない。
        /// </param>
        public void SetLoading(string songTitle, string artist, Texture2D jacket, float? chartProgress)
        {
            errorShown = false;
            leaving = false;
            progress = chartProgress;
            ApplySong(songTitle, artist, jacket);
        }

        /// <summary>
        /// 失敗状態を記録する。design にエラー専用の表示は無い (Issue #606)
        /// ため画面はローディングのまま (映像 + ヘッダーの戻る導線)。
        /// </summary>
        public void SetError()
        {
            errorShown = true;
            leaving = false;
        }

        /// <summary>選曲画面へ戻る (既存 HudPlayStatus.BackToSelect と同じ遷移)。</summary>
        public void Back()
        {
            if (leaving)
            {
                return;
            }

            leaving = true;
            if (sceneManager != null)
            {
                sceneManager.RetireGame();
            }
            else
            {
                Initiate.Fade("SelectScene", Color.black, 1.0f);
            }
        }

        private void ApplySong(string title, string artist, Texture2D jacket)
        {
            if (songTitle != null)
            {
                songTitle.text = title != null ? title : "";
            }

            if (songArtist != null)
            {
                songArtist.text = artist != null ? artist : "";
            }

            if (jacketImage != null)
            {
                // SetLoading は毎フレーム呼ばれる。テクスチャが同じ間は
                // Sprite.Create を繰り返さない (実体の作り直しはリークになる)。
                if (!ReferenceEquals(jacketTexture, jacket))
                {
                    jacketTexture = jacket;
                    // Issue #398: design N3HPJ は 56×56 を mode:"cover" で埋める。
                    // 実素材は 16:9 (320×180) なので中心を正方形に切り出してから
                    // Sprite 化する。切り抜きは provider が所有する (同じ原 Texture +
                    // 同じ矩形なら同じ実体を返すので、画面を出入りしても増えない)。
                    // 全体をそのまま入れると 56×31.5 の帯になり、design の
                    // 正方形から大きく外れる (r15 実機で確認)。
                    jacketSprite = V6JacketProvider.SquareCrop(jacket, "V6LoadingJacket");
                }

                jacketImage.sprite = jacketSprite;
                if (jacket != null)
                {
                    jacketImage.type = Image.Type.Simple;
                    jacketImage.preserveAspect = true;
                    jacketImage.color = Color.white;
                }
                else
                {
                    jacketImage.color = V5Tokens.Key2;
                }
            }

            if (noJacketLabel != null)
            {
                noJacketLabel.gameObject.SetActive(jacket == null);
            }
        }
    }
}
