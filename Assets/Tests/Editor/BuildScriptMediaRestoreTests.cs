using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #249 / #11: 配布ビルド (BuildScript) の退避メディア復元の検証。
    ///
    /// 2026-11-10 実測: Win64 ビルドは成功したのに、退避した
    /// `Assets/Game/Resources/Musics` を戻すとき、移動先が既に存在している
    /// (ビルド中に同名のフォルダが作り直される) ため Directory.Move が例外になり、
    /// **バッチ全体が失敗扱い** になっていた (バッチは非 0 で終了)。
    ///
    /// ここでは「移動先が存在しても上書きマージで復元できること」と
    /// 「退避と復元を繰り返しても内容が失われないこと」を固定する。
    /// </summary>
    public class BuildScriptMediaRestoreTests
    {
        string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "flashbeat-buildscript-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        /// <summary>private static MovePath を呼べるようにする。</summary>
        static MethodInfo MovePathMethod()
        {
            Type type = Type.GetType("BuildScript, FlashBeat.Editor");
            Assert.IsNotNull(type, "BuildScript (FlashBeat.Editor) が見つからない");
            MethodInfo method = type.GetMethod("MovePath", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "BuildScript.MovePath が見つからない");
            return method;
        }

        static void MovePath(string src, string dst)
        {
            MovePathMethod().Invoke(null, new object[] { src, dst });
        }

        [Test]
        public void MovePath_MovesTheDirectoryWhenTheDestinationIsEmpty()
        {
            string src = Path.Combine(root, "src");
            string dst = Path.Combine(root, "dst");
            Directory.CreateDirectory(src);
            File.WriteAllText(Path.Combine(src, "a.wav"), "a");

            MovePath(src, dst);

            Assert.IsFalse(Directory.Exists(src), "移動元は消える");
            Assert.IsTrue(File.Exists(Path.Combine(dst, "a.wav")), "移動先にファイルがある");
        }

        [Test]
        public void MovePath_MergesIntoAnExistingDestination_InsteadOfThrowing()
        {
            // 再現条件: 移動先 (元の場所) が既に存在している。
            string src = Path.Combine(root, "src");
            string dst = Path.Combine(root, "dst");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(dst);
            File.WriteAllText(Path.Combine(src, "a.wav"), "new-a");
            File.WriteAllText(Path.Combine(dst, "b.wav"), "kept-b");

            // 例外が出ずに済まない (旧実装は Directory.Move で IOException)。
            Assert.DoesNotThrow(() => MovePath(src, dst), "移動先があっても例外にしない");

            Assert.IsFalse(Directory.Exists(src), "移動元は消える");
            Assert.AreEqual("new-a", File.ReadAllText(Path.Combine(dst, "a.wav")), "移動元の内容で上書きされる");
            Assert.AreEqual("kept-b", File.ReadAllText(Path.Combine(dst, "b.wav")), "移動先にしか無いファイルは残る");
        }

        [Test]
        public void MovePath_MergesNestedDirectoriesAndKeepsTheMetaFile()
        {
            string src = Path.Combine(root, "src");
            string dst = Path.Combine(root, "dst");
            Directory.CreateDirectory(Path.Combine(src, "nested"));
            Directory.CreateDirectory(dst);
            File.WriteAllText(Path.Combine(src, "nested", "c.wav"), "c");
            File.WriteAllText(src + ".meta", "meta-body");

            MovePath(src, dst);

            Assert.AreEqual("c", File.ReadAllText(Path.Combine(dst, "nested", "c.wav")), "入れ子のファイルも移る");
            Assert.AreEqual("meta-body", File.ReadAllText(dst + ".meta"), "隣接 .meta も移る");
        }

        [Test]
        public void MovePath_DoesNothingWhenTheSourceIsMissing()
        {
            string src = Path.Combine(root, "nothing");
            string dst = Path.Combine(root, "dst");

            Assert.DoesNotThrow(() => MovePath(src, dst));
            Assert.IsFalse(Directory.Exists(dst), "移動元が無ければ何もしない");
        }
    }
}
