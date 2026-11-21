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
    /// V6LoadingView のローディング映像まわり (Issue #385 Phase 2.8 で分割)。
    /// VideoPlayer の開始 / 停止、映像面 (RawImage + RenderTexture) の状態遷移、
    /// 再生エラー処理を持つ。partial class のためフィールド・メソッドは
    /// V6LoadingView 本体と共有する。
    /// </summary>
    public sealed partial class V6LoadingView
    {
        /// <summary>
        /// ローディング映像を頭から再生する。clip が無ければ何もしない
        /// (その場合は背景のみ — 白画面は出さない)。
        /// </summary>
        private void StartVideo()
        {
            if (videoPlayer == null || videoPlayer.clip == null)
            {
                return;
            }

            if (videoTexture != null)
            {
                if (!videoTexture.IsCreated())
                {
                    videoTexture.Create();
                }

                // 出力先を張ってから再生する (Prepare が targetTexture を要求する)。
                videoPlayer.targetTexture = videoTexture;
            }

            videoPlayer.Play();
        }

        /// <summary>
        /// 非表示になった時に映像を止めテクスチャを解放する (素の映像を
        /// 裏で再生し続ける / 古いテクスチャを保持したままにしない)。
        /// </summary>
        private void StopVideo()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                videoPlayer.targetTexture = null;
            }

            if (videoRaw != null)
            {
                videoRaw.texture = null;
                videoRaw.enabled = false;
            }

            if (videoTexture != null && videoTexture.IsCreated())
            {
                videoTexture.Release();
            }
        }

        /// <summary>
        /// 映像面の状態を決める (純ロジック — <see cref="V6LoadingVideoPolicy"/> に
        /// 抽出して EditMode テストする)。
        /// </summary>
        private void ApplyVideoSurface()
        {
            if (videoRaw == null)
            {
                return;
            }

            bool hasClip = videoPlayer != null && videoPlayer.clip != null;
            bool prepared = videoPlayer != null && videoPlayer.isPrepared;
            V6LoadingVideoSurface surface = V6LoadingVideoPolicy.Decide(shown, hasClip, prepared, videoErrored);
            if (surface == lastSurface)
            {
                return;
            }

            lastSurface = surface;

            switch (surface)
            {
                case V6LoadingVideoSurface.Video:
                    if (videoTexture != null)
                    {
                        if (!videoTexture.IsCreated())
                        {
                            videoTexture.Create();
                        }

                        videoPlayer.targetTexture = videoTexture;
                        videoRaw.texture = videoTexture;
                    }

                    videoRaw.enabled = true;
                    break;

                case V6LoadingVideoSurface.Background:
                    // 映像が無い / 準備できていない / エラー → 白い RawImage を残さない。
                    videoRaw.enabled = false;
                    videoRaw.texture = null;
                    break;

                default:
                    // Hidden: ビューごと隠れている — テクスチャも背景も残さない。
                    videoRaw.enabled = false;
                    videoRaw.texture = null;
                    break;
            }

            // 背景は常に Commerce 背景 (fallback) として残す。映像が無いときは
            // こちらだけが画面を埋め、白い画面は出ない。
            if (bgImage != null)
            {
                bgImage.enabled = surface != V6LoadingVideoSurface.Hidden;
            }
        }

        /// <summary>ローディング映像の再生エラー。無音で推移しない (Issue #233)。</summary>
        private void OnVideoError(VideoPlayer source, string message)
        {
            videoErrored = true;
            ApplyVideoSurface();
            if (videoErrorLogged) return;
            videoErrorLogged = true;
            Debug.LogWarning($"[V6Loading] loading video unavailable: {message}");
        }
    }

    /// <summary>ローディング画面の映像面の状態。</summary>
    public enum V6LoadingVideoSurface
    {
        /// <summary>ローディング映像を出す (RawImage に映像テクスチャを接続)。</summary>
        Video = 0,

        /// <summary>映像は出さず、背景だけを見せる (白い画面を出さない)。</summary>
        Background = 1,

        /// <summary>何も出さない (ビューごと非表示)。</summary>
        Hidden = 2,
    }

    /// <summary>
    /// ローディング画面の映像面をどの状態にするかの判断 (純ロジック)。
    /// EditMode テストから直接呼べるよう、VideoPlayer に依存しない形で
    /// (clip 有無, 準備完了, エラー) → 状態 だけを受け取る。
    /// </summary>
    public static class V6LoadingVideoPolicy
    {
        /// <summary>
        /// 映像面の状態を決める。
        /// </summary>
        /// <param name="visible">ローディング画面が出ているか。</param>
        /// <param name="hasClip">再生する clip があるか。</param>
        /// <param name="prepared">VideoPlayer が準備完了か。</param>
        /// <param name="errored">再生エラーが起きたか。</param>
        public static V6LoadingVideoSurface Decide(bool visible, bool hasClip, bool prepared, bool errored)
        {
            if (!visible)
            {
                return V6LoadingVideoSurface.Hidden;
            }

            // clip が無い / 準備できない / エラー → 映像は出さない (白画面回避)。
            if (!hasClip || errored || !prepared)
            {
                return V6LoadingVideoSurface.Background;
            }

            return V6LoadingVideoSurface.Video;
        }
    }
}
