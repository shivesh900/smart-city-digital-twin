using NUnit.Framework;

namespace SmartCity.Tests
{
    public class TrafficOptimizerTests
    {
        private static TrafficOptimizer Opt() => new TrafficOptimizer(6f, 30f, 14f);

        [Test]
        public void BaselineIsEqualSplit()
        {
            var p = Opt().Baseline();
            Assert.AreEqual(14f, p.GreenNS);
            Assert.AreEqual(14f, p.GreenEW);
        }

        [Test]
        public void EmptyQueuesKeepBaseline()
        {
            var p = Opt().ComputePlan(0, 0);
            Assert.AreEqual(14f, p.GreenNS);
            Assert.AreEqual(14f, p.GreenEW);
        }

        [Test]
        public void LongerQueueGetsMoreGreen()
        {
            var p = Opt().ComputePlan(12, 3);
            Assert.Greater(p.GreenNS, p.GreenEW);
            var q = Opt().ComputePlan(2, 9);
            Assert.Greater(q.GreenEW, q.GreenNS);
        }

        [Test]
        public void GreensStayWithinBounds()
        {
            var o = Opt();
            foreach (var qa in new[] { 0f, 1f, 5f, 40f, 400f })
                foreach (var qb in new[] { 0f, 2f, 50f })
                {
                    var p = o.ComputePlan(qa, qb);
                    Assert.GreaterOrEqual(p.GreenNS, o.MinGreen); Assert.LessOrEqual(p.GreenNS, o.MaxGreen);
                    Assert.GreaterOrEqual(p.GreenEW, o.MinGreen); Assert.LessOrEqual(p.GreenEW, o.MaxGreen);
                }
        }

        [Test]
        public void GapOutRule()
        {
            var o = Opt();
            Assert.IsFalse(o.ShouldGapOut(3f, 0f, 5f), "never before min green");
            Assert.IsTrue(o.ShouldGapOut(7f, 0f, 5f), "empty green with waiting cross traffic ends early");
            Assert.IsFalse(o.ShouldGapOut(7f, 3f, 5f), "keeps green while serving vehicles");
            Assert.IsFalse(o.ShouldGapOut(7f, 0f, 0f), "no reason to switch if nobody waits");
        }

        [Test]
        public void AdaptiveReducesQueueUnderUnevenDemand()
        {
            var o = Opt();
            var fixedRun = QueueModel.Run(o, false, 0.25f, 0.08f, 0.5f, 1800f);
            var adaptive = QueueModel.Run(o, true, 0.25f, 0.08f, 0.5f, 1800f);
            Assert.Less(adaptive.AverageQueue, fixedRun.AverageQueue,
                $"adaptive {adaptive.AverageQueue:0.0} vs fixed {fixedRun.AverageQueue:0.0}");
        }

        [Test]
        public void AdaptiveIsNotWorseUnderBalancedDemand()
        {
            var o = Opt();
            var fixedRun = QueueModel.Run(o, false, 0.12f, 0.12f, 0.5f, 1800f);
            var adaptive = QueueModel.Run(o, true, 0.12f, 0.12f, 0.5f, 1800f);
            Assert.LessOrEqual(adaptive.AverageQueue, fixedRun.AverageQueue * 1.05f,
                $"adaptive {adaptive.AverageQueue:0.0} vs fixed {fixedRun.AverageQueue:0.0}");
        }
    }
}
