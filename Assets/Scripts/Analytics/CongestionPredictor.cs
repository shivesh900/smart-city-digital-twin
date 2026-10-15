using System;
using System.Collections.Generic;

namespace SmartCity
{
    /// <summary>
    /// Transparent baseline congestion forecaster. NOT machine learning.
    ///   movingAverage = mean of the last N samples
    ///   trend         = least-squares slope of the last N samples (per second)
    ///   forecast(h)   = clamp01( 0.7 * (a + b * (tLast + h)) + 0.3 * movingAverage )
    /// where a + b*t is the least-squares line. Every forecast is stored and compared with the value
    /// actually simulated h seconds later, so the panel can report an honest mean absolute error.
    /// </summary>
    public sealed class CongestionPredictor
    {
        private readonly int _window;
        private readonly Queue<KeyValuePair<float, float>> _samples = new Queue<KeyValuePair<float, float>>();
        private readonly Queue<KeyValuePair<float, float>> _pending = new Queue<KeyValuePair<float, float>>();
        private readonly Queue<float> _errors = new Queue<float>();
        private const int MaxErrors = 60;

        public float HorizonSeconds { get; }
        public float MovingAverage { get; private set; }
        public float SlopePerSecond { get; private set; }
        public float Intercept { get; private set; }
        public float LastForecast { get; private set; }
        public float LastTime { get; private set; }
        public int SampleCount => _samples.Count;
        public int EvaluatedForecasts => _errors.Count;
        public float MeanAbsoluteError
        {
            get
            {
                if (_errors.Count == 0) return float.NaN;
                float s = 0f; foreach (var e in _errors) s += e; return s / _errors.Count;
            }
        }

        public CongestionPredictor(int window = 30, float horizonSeconds = 30f)
        {
            _window = Math.Max(3, window);
            HorizonSeconds = Math.Max(1f, horizonSeconds);
        }

        public void AddSample(float time, float value)
        {
            value = Clamp01(value);
            // Score earlier forecasts whose target time has now been reached.
            while (_pending.Count > 0 && _pending.Peek().Key <= time + 1e-3f)
            {
                var p = _pending.Dequeue();
                _errors.Enqueue(Math.Abs(p.Value - value));
                if (_errors.Count > MaxErrors) _errors.Dequeue();
            }

            _samples.Enqueue(new KeyValuePair<float, float>(time, value));
            while (_samples.Count > _window) _samples.Dequeue();
            LastTime = time;
            Fit();
            LastForecast = Predict(HorizonSeconds);
            if (_samples.Count >= 3) _pending.Enqueue(new KeyValuePair<float, float>(time + HorizonSeconds, LastForecast));
        }

        public float Predict(float horizonSeconds)
        {
            if (_samples.Count == 0) return 0f;
            if (_samples.Count < 3) return MovingAverage;
            float line = Intercept + SlopePerSecond * (LastTime + horizonSeconds);
            return Clamp01(0.7f * line + 0.3f * MovingAverage);
        }

        /// <summary>"Rising", "Falling" or "Stable" using a +/-0.1 percentage-point-per-second band.</summary>
        public string TrendLabel()
        {
            if (_samples.Count < 3) return "Collecting data";
            if (SlopePerSecond > 0.001f) return "Rising";
            if (SlopePerSecond < -0.001f) return "Falling";
            return "Stable";
        }

        public void Reset()
        {
            _samples.Clear(); _pending.Clear(); _errors.Clear();
            MovingAverage = SlopePerSecond = Intercept = LastForecast = LastTime = 0f;
        }

        private void Fit()
        {
            int n = _samples.Count;
            double sx = 0, sy = 0, sxx = 0, sxy = 0;
            foreach (var s in _samples)
            {
                sx += s.Key; sy += s.Value; sxx += (double)s.Key * s.Key; sxy += (double)s.Key * s.Value;
            }
            MovingAverage = (float)(sy / n);
            double denom = n * sxx - sx * sx;
            if (n < 2 || Math.Abs(denom) < 1e-9)
            {
                SlopePerSecond = 0f;
                Intercept = MovingAverage;
                return;
            }
            double b = (n * sxy - sx * sy) / denom;
            double a = (sy - b * sx) / n;
            SlopePerSecond = (float)b;
            Intercept = (float)a;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
