using System;
using System.Reflection;
using FlashBeat.UI.V6.History;
using FlashBeat.UI.V6.Option;
using FlashBeat.UI.V6.Ranking;
using FlashBeat.UI.V6.Select.View;
using NUnit.Framework;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #415 (第5段階): v6churn (AutoTestV6Churn) は画面ごとの
    /// RebuildCountForAudit / IncrementalUpdateCountForAudit を
    /// 名前ベースのリフレクションで読む。計測対象の各ビューが
    /// 同名プロパティを公開していることを契約として固定し、
    /// 片方だけリネーム/削除されて計測が静かに欠けるのを防ぐ。
    /// </summary>
    public class V6AuditCounterContractTests
    {
        private static readonly Type[] MeasuredViews =
        {
            typeof(V6SelectView),
            typeof(V6HistoryView),
            typeof(V6RankingView),
            typeof(V6OptionView),
        };

        [Test]
        public void MeasuredViews_ExposeRebuildCountForAudit()
        {
            foreach (var type in MeasuredViews)
            {
                var prop = type.GetProperty("RebuildCountForAudit",
                    BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(prop, type.Name + " に RebuildCountForAudit が無い");
                Assert.AreEqual(typeof(int), prop.PropertyType,
                    type.Name + ".RebuildCountForAudit の型が int でない");
            }
        }

        [Test]
        public void MeasuredViews_ExposeIncrementalUpdateCountForAudit()
        {
            foreach (var type in MeasuredViews)
            {
                var prop = type.GetProperty("IncrementalUpdateCountForAudit",
                    BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(prop, type.Name + " に IncrementalUpdateCountForAudit が無い");
                Assert.AreEqual(typeof(int), prop.PropertyType,
                    type.Name + ".IncrementalUpdateCountForAudit の型が int でない");
            }
        }
    }
}
