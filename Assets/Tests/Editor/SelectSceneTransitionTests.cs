using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 選曲画面 → ゲーム画面の遷移ガード (Issue #222)。
///
/// Ready はマウス・キーの連打で複数回呼び出されうる。ガードが無いと
/// 同じ遷移が 2 回走ってシーンが重複ロードされ、プレイセッションの状態
/// (GManager.ResetSession) も二重にされる。
///
/// ガードは static なので、シーンをロードせずに EditMode で検証できる。
/// </summary>
public class SelectSceneTransitionTests
{
    [SetUp]
    public void ClearTheGuard()
    {
        SelectSceneTransitionGuard.Reset();
    }

    [TearDown]
    public void ReleaseTheGuard()
    {
        // static のまま残ると他のテストを壊すので、必ず解除する。
        SelectSceneTransitionGuard.Reset();
    }

    [Test]
    public void TheFirstTransitionRequestIsAllowed()
    {
        Assert.IsTrue(SelectSceneTransitionGuard.TryRequest(), "最初の遷移要求は通る");
        Assert.IsTrue(SelectSceneTransitionGuard.IsLocked, "要求された状態のほうがロックされている");
    }

    [Test]
    public void TheSecondTransitionRequestIsIgnored()
    {
        Assert.IsTrue(SelectSceneTransitionGuard.TryRequest(), "最初の遷移要求は通る");

        Assert.IsFalse(SelectSceneTransitionGuard.TryRequest(), "連打の 2 回目は通らない");
        Assert.IsFalse(SelectSceneTransitionGuard.TryRequest(), "連打の 3 回目も通らない");
        Assert.IsTrue(SelectSceneTransitionGuard.IsLocked, "ロックは解除されない");
    }

    [Test]
    public void TheGuardIsReleasedForTheNextSelection()
    {
        Assert.IsTrue(SelectSceneTransitionGuard.TryRequest());

        // 選曲画面へ戻ったとき (SelectSceneManager.Start) / 開始に失敗したときの解除。
        SelectSceneTransitionGuard.Reset();

        Assert.IsFalse(SelectSceneTransitionGuard.IsLocked, "解除後はロックされない");
        Assert.IsTrue(SelectSceneTransitionGuard.TryRequest(), "選び直しからは再び遷移できる");
    }

    [Test]
    public void TheProductionPathAsksTheGuard()
    {
        // GameScene() がガードを通らないと、このファイル以外のテストは無意味になる。
        // (遷移そのものはシーンのロードを伴うので、ここではコードの形だけを固定する)
        string source = File.ReadAllText(
            Path.Combine(Application.dataPath, "Game", "Scripts", "UI", "SelectSceneManager.cs"));

        int start = source.IndexOf("public void GameScene()", System.StringComparison.Ordinal);
        Assert.GreaterOrEqual(start, 0, "GameScene() が見つからない");

        // メソッド本体 (次の "public void" まで) にガードの呼び出しがあるか。
        int end = source.IndexOf("public void ", start + 1, System.StringComparison.Ordinal);
        if (end < 0) end = source.Length;
        string body = source.Substring(start, end - start);

        Assert.IsTrue(body.Contains("SelectSceneTransitionGuard.TryRequest()"),
            "GameScene() は遷移ガードを通る (二重遷移の防止)");
    }

    [Test]
    public void AFailedStartReleasesTheGuard()
    {
        // リモート曲の開始に失敗したときは、選び直しができるようにガードを解除する。
        string source = File.ReadAllText(
            Path.Combine(Application.dataPath, "Game", "Scripts", "UI", "SelectSceneManager.cs"));

        int count = 0;
        int index = 0;
        while (true)
        {
            index = source.IndexOf("SelectSceneTransitionGuard.Reset();", index, System.StringComparison.Ordinal);
            if (index < 0) break;
            count++;
            index += 1;
        }

        // Start() (選び直しの入口) + 開始失敗の 2 経路。
        Assert.GreaterOrEqual(count, 3, "ガードの解除 (選び直し / 開始失敗) が足りない");
    }
}
