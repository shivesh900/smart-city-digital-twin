using UnityEngine;

namespace SmartCity
{
    public enum ZoneType { Residential, Commercial, Industrial, Park, Hospital, Police, FireStation, Parking }

    /// <summary>
    /// Zoning plan for the city blocks (one block = the cell between four intersections).
    /// Deterministic for a given grid size, so the city looks the same every time it is opened.
    /// </summary>
    public sealed class CityLayout
    {
        public int CellsX { get; private set; }
        public int CellsY { get; private set; }
        public ZoneType[,] Zones { get; private set; }
        /// <summary>Low-emission zone (cleaner vehicle mix assumed: traffic emissions x0.5).</summary>
        public bool[,] LowEmission { get; private set; }
        public Vector2Int HospitalCell { get; private set; }
        public Vector2Int PoliceCell { get; private set; }
        public Vector2Int FireCell { get; private set; }

        public ZoneType ZoneAt(int cx, int cy) => Zones[cx, cy];

        public static CityLayout Create(int columns, int rows)
        {
            var l = new CityLayout { CellsX = columns - 1, CellsY = rows - 1 };
            l.Zones = new ZoneType[l.CellsX, l.CellsY];
            l.LowEmission = new bool[l.CellsX, l.CellsY];
            for (int y = 0; y < l.CellsY; y++)
                for (int x = 0; x < l.CellsX; x++)
                {
                    float u = (x + 0.5f) / l.CellsX, v = (y + 0.5f) / l.CellsY;
                    float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));
                    ZoneType z = ZoneType.Residential;
                    if (d < 0.3f) z = ZoneType.Commercial;
                    else if (v < 0.25f && u < 0.55f) z = ZoneType.Industrial;
                    l.Zones[x, y] = z;
                    l.LowEmission[x, y] = z == ZoneType.Commercial;
                }
            // Central park in the middle of downtown when the grid has a middle cell.
            if (l.CellsX % 2 == 1 && l.CellsY % 2 == 1) l.Zones[l.CellsX / 2, l.CellsY / 2] = ZoneType.Park;

            l.HospitalCell = l.Place(0.85f, 0.65f, ZoneType.Hospital);
            l.PoliceCell = l.Place(0.15f, 0.7f, ZoneType.Police);
            l.FireCell = l.Place(0.65f, 0.12f, ZoneType.FireStation);
            l.Place(0.35f, 0.35f, ZoneType.Parking);
            l.Place(0.88f, 0.15f, ZoneType.Park);
            return l;
        }

        private Vector2Int Place(float u, float v, ZoneType zone)
        {
            Vector2Int best = new Vector2Int(-1, -1);
            float bestD = float.MaxValue;
            for (int y = 0; y < CellsY; y++)
                for (int x = 0; x < CellsX; x++)
                {
                    var z = Zones[x, y];
                    if (z == ZoneType.Hospital || z == ZoneType.Police || z == ZoneType.FireStation || z == ZoneType.Parking) continue;
                    if (z == ZoneType.Park && zone != ZoneType.Park) continue;
                    float cu = (x + 0.5f) / CellsX, cv = (y + 0.5f) / CellsY;
                    float d = (cu - u) * (cu - u) + (cv - v) * (cv - v);
                    if (d < bestD) { bestD = d; best = new Vector2Int(x, y); }
                }
            if (best.x >= 0)
            {
                Zones[best.x, best.y] = zone;
                LowEmission[best.x, best.y] = false;
            }
            return best;
        }

        public static string ZoneName(ZoneType z)
        {
            switch (z)
            {
                case ZoneType.FireStation: return "Fire station";
                case ZoneType.Parking: return "Parking";
                default: return z.ToString();
            }
        }
    }
}
