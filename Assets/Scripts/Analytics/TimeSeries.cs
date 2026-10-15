using System;

namespace SmartCity
{
    /// <summary>Fixed-capacity ring buffer of floats (oldest at index 0) used for charts and statistics.</summary>
    public sealed class TimeSeries
    {
        private readonly float[] _data;
        private int _start;
        public int Count { get; private set; }
        public int Capacity => _data.Length;

        public TimeSeries(int capacity) { _data = new float[Math.Max(2, capacity)]; }

        public void Add(float value)
        {
            if (Count < _data.Length) { _data[(_start + Count) % _data.Length] = value; Count++; }
            else { _data[_start] = value; _start = (_start + 1) % _data.Length; }
        }

        public float this[int i]
        {
            get
            {
                if (i < 0 || i >= Count) throw new ArgumentOutOfRangeException(nameof(i));
                return _data[(_start + i) % _data.Length];
            }
        }

        public float Latest => Count == 0 ? 0f : this[Count - 1];

        public float Min()
        {
            if (Count == 0) return 0f;
            float m = float.MaxValue;
            for (int i = 0; i < Count; i++) m = Math.Min(m, this[i]);
            return m;
        }

        public float Max()
        {
            if (Count == 0) return 0f;
            float m = float.MinValue;
            for (int i = 0; i < Count; i++) m = Math.Max(m, this[i]);
            return m;
        }

        public float Average()
        {
            if (Count == 0) return 0f;
            float s = 0f;
            for (int i = 0; i < Count; i++) s += this[i];
            return s / Count;
        }

        public void Clear() { _start = 0; Count = 0; }
    }
}
