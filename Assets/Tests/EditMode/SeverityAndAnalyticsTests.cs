using NUnit.Framework;

namespace SmartCity.Tests
{
    public class SeverityClassifierTests
    {
        private static IncidentContext Ctx(IncidentKind k) => new IncidentContext { Kind = k, ResponderRouteAvailable = true };

        [Test]
        public void BaseScoresMatchDocumentedRules()
        {
            Assert.AreEqual(SeverityLevel.Medium, SeverityClassifier.Classify(Ctx(IncidentKind.Accident)).Level);   // 3
            Assert.AreEqual(SeverityLevel.Low, SeverityClassifier.Classify(Ctx(IncidentKind.BlockedRoad)).Level);   // 2
            Assert.AreEqual(SeverityLevel.Medium, SeverityClassifier.Classify(Ctx(IncidentKind.EmergencyRequest)).Level); // 4
        }

        [Test]
        public void HeavyTrafficAndPeakRaiseSeverity()
        {
            var c = Ctx(IncidentKind.Accident);
            c.VehiclesNearby = 15; c.PeakHour = true;   // 3 + 2 + 1 = 6
            var r = SeverityClassifier.Classify(c);
            Assert.AreEqual(6, r.Score);
            Assert.AreEqual(SeverityLevel.High, r.Level);
        }

        [Test]
        public void NoResponderRouteMakesCritical()
        {
            var c = Ctx(IncidentKind.EmergencyRequest);
            c.ResponderRouteAvailable = false; c.NearbyBlockedRoads = 4;   // 4 + 2 + 1 = 7
            var r = SeverityClassifier.Classify(c);
            Assert.AreEqual(SeverityLevel.Critical, r.Level);
            Assert.That(r.Reasons.Exists(s => s.StartsWith("R6")));
        }

        [Test]
        public void AqiRules()
        {
            var c = Ctx(IncidentKind.PollutionSpike);
            c.LocalAqi = 320f;   // 2 + 2
            Assert.AreEqual(4, SeverityClassifier.Classify(c).Score);
            c.LocalAqi = 210f;   // 2 + 1
            Assert.AreEqual(3, SeverityClassifier.Classify(c).Score);
        }

        [Test]
        public void PeakHourWindows()
        {
            Assert.IsTrue(SeverityClassifier.IsPeakHour(8.5f));
            Assert.IsTrue(SeverityClassifier.IsPeakHour(18f));
            Assert.IsFalse(SeverityClassifier.IsPeakHour(13f));
            Assert.IsFalse(SeverityClassifier.IsPeakHour(2f));
        }
    }

    public class CongestionPredictorTests
    {
        [Test]
        public void FollowsLinearTrend()
        {
            var p = new CongestionPredictor(20, 10f);
            for (int t = 0; t < 20; t++) p.AddSample(t, 0.2f + 0.01f * t);
            Assert.Greater(p.SlopePerSecond, 0.009f);
            Assert.AreEqual("Rising", p.TrendLabel());
            Assert.Greater(p.Predict(10f), 0.39f);
        }

        [Test]
        public void ConstantSeriesIsStableWithZeroError()
        {
            var p = new CongestionPredictor(10, 5f);
            for (int t = 0; t < 40; t++) p.AddSample(t, 0.4f);
            Assert.AreEqual("Stable", p.TrendLabel());
            Assert.AreEqual(0.4f, p.Predict(5f), 1e-3f);
            Assert.Greater(p.EvaluatedForecasts, 0);
            Assert.AreEqual(0f, p.MeanAbsoluteError, 1e-3f);
        }

        [Test]
        public void ForecastIsClamped()
        {
            var p = new CongestionPredictor(10, 100f);
            for (int t = 0; t < 10; t++) p.AddSample(t, 0.1f * t);
            Assert.LessOrEqual(p.Predict(100f), 1f);
        }
    }

    public class ScenarioComparisonTests
    {
        [Test]
        public void DetectsImprovement()
        {
            var before = new TrafficMetrics { DurationSeconds = 120, AvgQueueLength = 20, AvgTripSeconds = 90, AvgSpeedKmh = 20, AvgStoppedShare = 0.4f, TripsPerMinute = 10, CompletedTrips = 20 };
            var after = new TrafficMetrics { DurationSeconds = 120, AvgQueueLength = 12, AvgTripSeconds = 75, AvgSpeedKmh = 24, AvgStoppedShare = 0.3f, TripsPerMinute = 12, CompletedTrips = 24 };
            var rows = ScenarioComparison.Compare(before, after);
            foreach (var r in rows) Assert.AreEqual(1, r.Outcome, r.Name);
            StringAssert.Contains("IMPROVED", ScenarioComparison.Verdict(rows, before, after));
        }

        [Test]
        public void ReportsHonestlyWhenWorse()
        {
            var before = new TrafficMetrics { DurationSeconds = 100, AvgQueueLength = 10, AvgTripSeconds = 60, AvgSpeedKmh = 25, AvgStoppedShare = 0.2f, TripsPerMinute = 12, CompletedTrips = 20 };
            var after = new TrafficMetrics { DurationSeconds = 100, AvgQueueLength = 14, AvgTripSeconds = 70, AvgSpeedKmh = 21, AvgStoppedShare = 0.3f, TripsPerMinute = 10, CompletedTrips = 17 };
            var rows = ScenarioComparison.Compare(before, after);
            StringAssert.Contains("did NOT help", ScenarioComparison.Verdict(rows, before, after));
        }

        [Test]
        public void AccumulatorTimeWeights()
        {
            var acc = new MetricsAccumulator();
            acc.AddSample(1f, 10f, 4f, 0.5f, 0.2f);
            acc.AddSample(3f, 30f, 0f, 0f, 0.6f);
            acc.AddTrip(50f); acc.AddTrip(70f);
            var m = acc.Snapshot();
            Assert.AreEqual(25f, m.AvgSpeedKmh, 1e-4f);
            Assert.AreEqual(1f, m.AvgQueueLength, 1e-4f);
            Assert.AreEqual(60f, m.AvgTripSeconds, 1e-4f);
            Assert.AreEqual(2, m.CompletedTrips);
        }
    }

    public class RecommendationEngineTests
    {
        [Test]
        public void RecommendsOptimizationWhenCongested()
        {
            var s = new CityStateSnapshot { Congestion = 0.6f, WorstIntersectionQueue = 9, WorstIntersection = "Junction C3", MaxAqi = 80, AqiWarning = 200, AqiCritical = 300 };
            var list = RecommendationEngine.Evaluate(s);
            Assert.That(list.Exists(r => r.Title.Contains("adaptive")));
        }

        [Test]
        public void CriticalAqiIsTopPriority()
        {
            var s = new CityStateSnapshot { Congestion = 0.1f, MaxAqi = 350, MaxAqiZone = "Industrial block A1", AqiWarning = 200, AqiCritical = 300 };
            var list = RecommendationEngine.Evaluate(s);
            Assert.AreEqual(RecommendationPriority.Critical, list[0].Priority);
        }

        [Test]
        public void QuietCityGetsInfo()
        {
            var s = new CityStateSnapshot { Congestion = 0.1f, MaxAqi = 60, AqiWarning = 200, AqiCritical = 300 };
            var list = RecommendationEngine.Evaluate(s);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(RecommendationPriority.Info, list[0].Priority);
        }
    }
}
