using System;

namespace SmartCity
{
    /// <summary>Green times (seconds) for the two signal groups of an intersection.</summary>
    public struct SignalTimingPlan
    {
        public float GreenNS;
        public float GreenEW;
        public string Reason;

        public override string ToString() => $"NS {GreenNS:0.0}s / EW {GreenEW:0.0}s";
    }

    /// <summary>
    /// Queue-based adaptive signal timing (rule-based, fully explainable - no machine learning).
    ///
    /// Baseline: fixed-time control, equal green for both directions every cycle.
    /// Optimized:
    ///  1. Green split proportional to measured queue lengths:  share_i = (q_i + 0.5) / (q_NS + q_EW + 1).
    ///  2. Cycle budget grows with total demand: budget = 1.6 * baselineGreen + secondsPerQueuedVehicle * (q_NS + q_EW),
    ///     clamped to [2 * minGreen, 2 * maxGreen].
    ///  3. Each green is clamped to [minGreen, maxGreen] so no direction is starved.
    ///  4. Gap-out (actuated control): after minGreen, a green ends early when its own queue is empty
    ///     and the crossing direction has waiting vehicles.
    /// </summary>
    public sealed class TrafficOptimizer
    {
        public float MinGreen = 6f;
        public float MaxGreen = 30f;
        public float BaselineGreen = 14f;
        public float SecondsPerQueuedVehicle = 1.6f;

        public TrafficOptimizer() { }

        public TrafficOptimizer(float minGreen, float maxGreen, float baselineGreen)
        {
            MinGreen = minGreen;
            MaxGreen = Math.Max(minGreen, maxGreen);
            BaselineGreen = Clamp(baselineGreen, MinGreen, MaxGreen);
        }

        public SignalTimingPlan Baseline() =>
            new SignalTimingPlan { GreenNS = BaselineGreen, GreenEW = BaselineGreen, Reason = "Fixed-time baseline (equal green)" };

        public SignalTimingPlan ComputePlan(float queueNS, float queueEW)
        {
            queueNS = Math.Max(0f, queueNS);
            queueEW = Math.Max(0f, queueEW);
            float total = queueNS + queueEW;
            if (total < 0.5f)
            {
                var b = Baseline();
                b.Reason = "No queues measured - keep baseline split";
                return b;
            }
            float budget = Clamp(1.6f * BaselineGreen + SecondsPerQueuedVehicle * total, 2f * MinGreen, 2f * MaxGreen);
            float shareNS = (queueNS + 0.5f) / (total + 1f);
            float gNS = Clamp(budget * shareNS, MinGreen, MaxGreen);
            float gEW = Clamp(budget - gNS, MinGreen, MaxGreen);
            string dominant = queueNS > queueEW ? "north-south" : (queueEW > queueNS ? "east-west" : "neither");
            return new SignalTimingPlan
            {
                GreenNS = gNS,
                GreenEW = gEW,
                Reason = $"Queues NS={queueNS:0} EW={queueEW:0}: more green to {dominant} (budget {budget:0}s)"
            };
        }

        /// <summary>Actuated gap-out rule: end the current green early if it serves nobody and others wait.</summary>
        public bool ShouldGapOut(float elapsedGreen, float queueServed, float queueWaiting)
        {
            return elapsedGreen >= MinGreen && queueServed < 0.5f && queueWaiting >= 1f;
        }

        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }

    /// <summary>
    /// Deterministic single-intersection queue model (two signal groups). Used by unit tests and by the
    /// documentation to show, with numbers, when the adaptive plan beats the fixed plan.
    /// Vehicles arrive at a constant rate; while green, the queue discharges at the saturation flow.
    /// </summary>
    public static class QueueModel
    {
        public struct Result
        {
            public float AverageQueue;
            public float AverageDelaySeconds;
            public float FinalQueue;
            public int Cycles;
        }

        public static Result Run(TrafficOptimizer optimizer, bool adaptive, float arrivalsNSPerSec, float arrivalsEWPerSec,
            float saturationFlowPerSec, float durationSec, float yellow = 3f, float allRed = 1f, float dt = 0.1f)
        {
            float qNS = 0f, qEW = 0f, t = 0f, queueIntegral = 0f, arrived = 0f;
            int cycles = 0;
            while (t < durationSec)
            {
                var plan = adaptive ? optimizer.ComputePlan(qNS, qEW) : optimizer.Baseline();
                cycles++;
                for (int group = 0; group < 2 && t < durationSec; group++)
                {
                    float green = group == 0 ? plan.GreenNS : plan.GreenEW;
                    float elapsed = 0f;
                    while (elapsed < green && t < durationSec)
                    {
                        Step(ref qNS, ref qEW, ref t, ref queueIntegral, ref arrived, arrivalsNSPerSec, arrivalsEWPerSec, dt);
                        if (group == 0) qNS = Math.Max(0f, qNS - saturationFlowPerSec * dt);
                        else qEW = Math.Max(0f, qEW - saturationFlowPerSec * dt);
                        elapsed += dt;
                        if (adaptive && optimizer.ShouldGapOut(elapsed, group == 0 ? qNS : qEW, group == 0 ? qEW : qNS)) break;
                    }
                    float lost = yellow + allRed;
                    for (float l = 0f; l < lost && t < durationSec; l += dt)
                        Step(ref qNS, ref qEW, ref t, ref queueIntegral, ref arrived, arrivalsNSPerSec, arrivalsEWPerSec, dt);
                }
            }
            return new Result
            {
                AverageQueue = queueIntegral / Math.Max(dt, t),
                AverageDelaySeconds = arrived > 0f ? queueIntegral / arrived : 0f,
                FinalQueue = qNS + qEW,
                Cycles = cycles
            };
        }

        private static void Step(ref float qNS, ref float qEW, ref float t, ref float integral, ref float arrived,
            float aNS, float aEW, float dt)
        {
            qNS += aNS * dt;
            qEW += aEW * dt;
            arrived += (aNS + aEW) * dt;
            integral += (qNS + qEW) * dt;
            t += dt;
        }
    }
}
