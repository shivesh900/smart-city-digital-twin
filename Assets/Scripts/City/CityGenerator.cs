using System.Collections.Generic;
using UnityEngine;

namespace SmartCity
{
    /// <summary>
    /// Procedurally generates the 3D city from primitives and generated meshes (no paid or imported assets):
    /// ground, roads, markings, crosswalks, sidewalks, zoned buildings, hospital/police/fire stations,
    /// parks with trees, parking, streetlights, road signs and pedestrians.
    /// Static geometry is merged per material to keep draw calls low on laptops.
    /// </summary>
    public class CityGenerator : MonoBehaviour
    {
        public readonly List<BuildingInfo> Buildings = new List<BuildingInfo>();
        public readonly List<MeshRenderer> StreetlightRenderers = new List<MeshRenderer>();
        public int StreetlightCount { get; private set; }
        public int HospitalNode { get; private set; } = -1;
        public int PoliceNode { get; private set; } = -1;
        public int FireNode { get; private set; } = -1;
        public Vector3 HospitalPosition { get; private set; }
        public Vector3 PolicePosition { get; private set; }
        public Vector3 FirePosition { get; private set; }
        public Transform Root { get; private set; }
        public bool Generated { get; private set; }

        private SimulationController _sim;
        private RoadNetworkManager _net;
        private SimulationSettings _s;
        private System.Random _rng;

        private const float SidewalkTop = 0.15f;

        public void Generate(SimulationController sim)
        {
            if (Generated) return;
            _sim = sim; _net = sim.roadNetwork; _s = sim.settings;
            _rng = new System.Random(_s.seed);
            Root = new GameObject("Generated City").transform;
            Root.SetParent(transform, false);

            BuildGround();
            BuildRoads();
            BuildBlocks();
            BuildStreetlights();
            BuildSigns();
            var peds = new GameObject("Pedestrians").AddComponent<PedestrianSystem>();
            peds.transform.SetParent(Root, false);
            peds.Build(sim, _net, 48);
            Generated = true;
        }

        private float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        // ------------------------------------------------------------------ ground & roads

        private void BuildGround()
        {
            var b = new MeshBuilder();
            float size = Mathf.Max(_net.CityBounds.size.x, _net.CityBounds.size.z) + 260f;
            b.AddFlat(0, new Vector3(0, -0.01f, 0), size, size, 0f, true);
            var go = b.Build("Ground", Root, MaterialLibrary.Get("ground"));
            go.GetComponent<MeshRenderer>().receiveShadows = true;
        }

        private void BuildRoads()
        {
            float rw = _net.RoadWidth, S = _net.Spacing;
            var roads = new MeshBuilder();
            var lanes = new MeshBuilder();       // cyan centre dashes
            var edges = new MeshBuilder();       // white edge & stop lines
            var zebra = new MeshBuilder();

            foreach (var seg in _net.Segments)
            {
                Vector3 a = _net.NodePosition(seg.NodeA), c = _net.NodePosition(seg.NodeB);
                Vector3 dir = (c - a).normalized;
                Vector3 right = new Vector3(dir.z, 0, -dir.x);
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                Vector3 mid = (a + c) * 0.5f;
                roads.AddFlat(0, mid + Vector3.up * 0.02f, rw, S, yaw);

                float start = _net.StopDistance, end = S - _net.StopDistance;
                // centre dashed line
                for (float d = start + 1f; d + 3f < end; d += 6f)
                    lanes.AddFlat(0, a + dir * (d + 1.5f) + Vector3.up * 0.035f, 0.18f, 3f, yaw);
                // edge lines
                float len = end - start;
                Vector3 m2 = a + dir * (start + len * 0.5f) + Vector3.up * 0.034f;
                edges.AddFlat(0, m2 + right * (rw * 0.5f - 0.45f), 0.14f, len, yaw);
                edges.AddFlat(0, m2 - right * (rw * 0.5f - 0.45f), 0.14f, len, yaw);
            }

            // intersections: square pad + zebra crossings + stop lines
            foreach (var j in _net.Intersections)
            {
                roads.AddFlat(0, j.Position + Vector3.up * 0.021f, rw, rw, 0f);
                foreach (var lane in j.Outgoing)
                {
                    Vector3 dir = lane.Direction;
                    Vector3 right = new Vector3(dir.z, 0, -dir.x);
                    float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    Vector3 crossCenter = j.Position + dir * (rw * 0.5f + 1.75f) + Vector3.up * 0.04f;
                    for (float o = -rw * 0.5f + 0.8f; o <= rw * 0.5f - 0.6f; o += 1.25f)
                        zebra.AddFlat(0, crossCenter + right * o, 0.6f, 2.6f, yaw);
                }
                foreach (var lane in j.Incoming)
                {
                    Vector3 dir = lane.Direction;
                    float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    Vector3 roadCenterAtStop = j.Position - dir * _net.StopDistance;
                    Vector3 laneCenter = roadCenterAtStop + (lane.End - roadCenterAtStop - Vector3.Project(lane.End - roadCenterAtStop, dir));
                    edges.AddFlat(0, laneCenter + Vector3.up * 0.036f, rw * 0.5f - 0.6f, 0.45f, yaw);
                }
            }

            var roadGo = roads.Build("Road Surface", Root, MaterialLibrary.Get("road"));
            roadGo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Flat(lanes.Build("Lane Markings", Root, MaterialLibrary.Get("laneMarking")));
            Flat(edges.Build("Edge & Stop Lines", Root, MaterialLibrary.Get("edgeMarking")));
            Flat(zebra.Build("Pedestrian Crossings", Root, MaterialLibrary.Get("crosswalk")));
        }

        private static void Flat(GameObject go)
        {
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ blocks, zoning & buildings

        private void BuildBlocks()
        {
            float S = _net.Spacing, rw = _net.RoadWidth, sw = _s.sidewalkWidth;
            float blockSize = S - rw;
            float inner = blockSize - 2f * sw;
            var sidewalks = new MeshBuilder();
            var grass = new MeshBuilder();
            var plaza = new MeshBuilder();
            var layout = _net.Layout;

            for (int cy = 0; cy < layout.CellsY; cy++)
                for (int cx = 0; cx < layout.CellsX; cx++)
                {
                    Vector3 c = _net.CellCenter(cx, cy);
                    sidewalks.AddBox(0, c + Vector3.up * (SidewalkTop * 0.5f), new Vector3(blockSize, SidewalkTop, blockSize));
                    var zone = layout.Zones[cx, cy];
                    Vector3 top = c + Vector3.up * (SidewalkTop + 0.005f);
                    if (zone == ZoneType.Park || zone == ZoneType.Residential) grass.AddFlat(0, top, inner, inner);
                    else plaza.AddFlat(0, top, inner, inner);

                    switch (zone)
                    {
                        case ZoneType.Commercial: BuildCommercial(cx, cy, c, inner); break;
                        case ZoneType.Residential: BuildResidential(cx, cy, c, inner); break;
                        case ZoneType.Industrial: BuildIndustrial(cx, cy, c, inner); break;
                        case ZoneType.Park: BuildPark(cx, cy, c, inner); break;
                        case ZoneType.Hospital: BuildHospital(cx, cy, c, inner); break;
                        case ZoneType.Police: BuildPolice(cx, cy, c, inner); break;
                        case ZoneType.FireStation: BuildFireStation(cx, cy, c, inner); break;
                        case ZoneType.Parking: BuildParking(cx, cy, c, inner); break;
                    }
                }
            sidewalks.Build("Sidewalks", Root, MaterialLibrary.Get("sidewalk"));
            Flat(grass.Build("Green Space", Root, MaterialLibrary.Get("grass")));
            Flat(plaza.Build("Plazas", Root, MaterialLibrary.Get("parkingLot")));
        }

        private BuildingInfo AddBuilding(string name, ZoneType zone, int cx, int cy, Vector3 groundCenter, Vector3 size, string materialKey, bool crown = false, float setback = 0f)
        {
            var b = new MeshBuilder(3);
            Vector2 roofUV = new Vector2(0.004f, 0.004f);
            Vector3 center = new Vector3(0, size.y * 0.5f, 0);
            b.AddBox(0, center, size, 0f, true, false, roofUV, 1);
            float totalHeight = size.y;
            if (setback > 0f)
            {
                var upper = new Vector3(size.x * setback, size.y * 0.35f, size.z * setback);
                b.AddBox(0, new Vector3(0, size.y + upper.y * 0.5f, 0), upper, 0f, true, false, roofUV, 1);
                totalHeight += upper.y;
            }
            if (crown)
            {
                float h = totalHeight + 0.15f;
                Vector3 top = setback > 0f ? new Vector3(size.x * setback, 0, size.z * setback) : size;
                b.AddBox(2, new Vector3(0, h, top.z * 0.5f), new Vector3(top.x, 0.3f, 0.3f));
                b.AddBox(2, new Vector3(0, h, -top.z * 0.5f), new Vector3(top.x, 0.3f, 0.3f));
                b.AddBox(2, new Vector3(top.x * 0.5f, h, 0), new Vector3(0.3f, 0.3f, top.z));
                b.AddBox(2, new Vector3(-top.x * 0.5f, h, 0), new Vector3(0.3f, 0.3f, top.z));
            }
            var go = b.Build(name, Root, MaterialLibrary.Get(materialKey), MaterialLibrary.Get("roof"), MaterialLibrary.Get("roofAccent"));
            go.isStatic = false;
            go.transform.position = groundCenter + Vector3.up * SidewalkTop;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, totalHeight * 0.5f, 0);
            col.size = new Vector3(size.x, totalHeight, size.z);
            var info = new BuildingInfo
            {
                Id = Buildings.Count,
                Name = name,
                Zone = zone,
                CellX = cx,
                CellY = cy,
                Position = go.transform.position,
                Size = new Vector3(size.x, totalHeight, size.z),
                Floors = Mathf.Max(1, Mathf.RoundToInt(totalHeight / 3.2f)),
                Renderer = go.GetComponent<MeshRenderer>()
            };
            info.FloorArea = size.x * size.z * info.Floors;
            info.OriginalMaterials = info.Renderer.sharedMaterials;
            var sel = go.AddComponent<Selectable>();
            sel.kind = SelectableKind.Building; sel.id = info.Id;
            Buildings.Add(info);
            return info;
        }

        private void BuildCommercial(int cx, int cy, Vector3 c, float inner)
        {
            float lot = inner / 2f;
            Vector3 cityCenter = Vector3.zero;
            float maxDist = _net.CityBounds.extents.magnitude;
            for (int i = 0; i < 2; i++)
                for (int k = 0; k < 2; k++)
                {
                    Vector3 p = c + new Vector3((i - 0.5f) * lot, 0, (k - 0.5f) * lot);
                    float centrality = 1f - Mathf.Clamp01(Vector3.Distance(p, cityCenter) / (maxDist * 0.6f));
                    float h = Rand(20f, 36f) + centrality * Rand(20f, 42f);
                    float w = Rand(lot - 4.5f, lot - 2f), d = Rand(lot - 4.5f, lot - 2f);
                    bool tower = h > 45f;
                    AddBuilding($"Office Tower {Buildings.Count + 1}", ZoneType.Commercial, cx, cy, p, new Vector3(w, h, d), "bldCommercial", tower || _rng.NextDouble() < 0.4, tower ? 0.65f : 0f);
                }
        }

        private void BuildResidential(int cx, int cy, Vector3 c, float inner)
        {
            int n = 3;
            float lot = inner / n;
            for (int i = 0; i < n; i++)
                for (int k = 0; k < n; k++)
                {
                    if (i == 1 && k == 1) { Tree(c + new Vector3(Rand(-2, 2), SidewalkTop, Rand(-2, 2)), 1.2f); continue; } // courtyard tree
                    Vector3 p = c + new Vector3((i - 1) * lot, 0, (k - 1) * lot);
                    float h = _rng.NextDouble() < 0.25 ? Rand(14f, 26f) : Rand(5f, 12f);
                    float w = Rand(lot - 3.5f, lot - 1.5f), d = Rand(lot - 3.5f, lot - 1.5f);
                    AddBuilding($"Residence {Buildings.Count + 1}", ZoneType.Residential, cx, cy, p, new Vector3(w, h, d), "bldResidential", false);
                }
        }

        private void BuildIndustrial(int cx, int cy, Vector3 c, float inner)
        {
            var chimneys = new MeshBuilder();
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = c + new Vector3(0, 0, (i - 0.5f) * inner * 0.5f);
                AddBuilding($"Factory {Buildings.Count + 1}", ZoneType.Industrial, cx, cy, p, new Vector3(inner - 3f, Rand(8f, 12f), inner * 0.5f - 3f), "bldIndustrial");
                chimneys.AddCylinder(0, p + new Vector3(inner * 0.3f, SidewalkTop, 0), 1.1f, Rand(20f, 28f), 12);
            }
            chimneys.Build("Chimneys", Root, MaterialLibrary.Get("chimney"));
        }

        private void BuildHospital(int cx, int cy, Vector3 c, float inner)
        {
            var main = AddBuilding("City Hospital", ZoneType.Hospital, cx, cy, c + new Vector3(0, 0, inner * 0.18f), new Vector3(inner - 4f, 20f, inner * 0.5f), "bldHospital", true);
            var wing = AddBuilding("Hospital Wing", ZoneType.Hospital, cx, cy, c + new Vector3(-inner * 0.22f, 0, -inner * 0.25f), new Vector3(inner * 0.42f, 11f, inner * 0.38f), "bldHospital");
            var deco = new MeshBuilder(2);
            Vector3 roof = main.Position + Vector3.up * (main.Size.y + 0.2f);
            deco.AddBox(0, roof, new Vector3(7f, 0.3f, 2.2f));
            deco.AddBox(0, roof, new Vector3(2.2f, 0.3f, 7f));
            Vector3 front = main.Position + new Vector3(0, 12f, -main.Size.z * 0.5f - 0.2f);
            deco.AddBox(0, front, new Vector3(3.5f, 1.1f, 0.3f));
            deco.AddBox(0, front, new Vector3(1.1f, 3.5f, 0.3f));
            deco.AddCylinder(1, wing.Position + Vector3.up * (wing.Size.y + 0.02f), 4.5f, 0.15f, 20);
            deco.Build("Hospital Markings", Root, MaterialLibrary.Get("medicalRed"), MaterialLibrary.Get("helipad"));
            WorldLabel.Create(Root, wing.Position + Vector3.up * (wing.Size.y + 0.4f), "H", Color.white, 3f, false).transform.rotation = Quaternion.Euler(90, 0, 0);
            HospitalPosition = main.Position;
            HospitalNode = _net.NearestNode(main.Position);
            WorldLabel.Create(Root, main.Position + Vector3.up * (main.Size.y + 6f), "CITY HOSPITAL", new Color(1f, 0.4f, 0.45f), 1.6f);
            AmbientParked(VehicleKind.Ambulance, c + new Vector3(inner * 0.3f, 0, -inner * 0.3f), 2);
        }

        private void BuildPolice(int cx, int cy, Vector3 c, float inner)
        {
            var hq = AddBuilding("Police HQ", ZoneType.Police, cx, cy, c + new Vector3(0, 0, inner * 0.15f), new Vector3(inner * 0.7f, 13f, inner * 0.5f), "bldPolice", true);
            PolicePosition = hq.Position;
            PoliceNode = _net.NearestNode(hq.Position);
            WorldLabel.Create(Root, hq.Position + Vector3.up * (hq.Size.y + 6f), "POLICE HQ", new Color(0.5f, 0.75f, 1f), 1.6f);
            AmbientParked(VehicleKind.Police, c + new Vector3(-inner * 0.25f, 0, -inner * 0.3f), 3);
        }

        private void BuildFireStation(int cx, int cy, Vector3 c, float inner)
        {
            var st = AddBuilding("Fire Station", ZoneType.FireStation, cx, cy, c + new Vector3(0, 0, inner * 0.1f), new Vector3(inner * 0.75f, 9f, inner * 0.55f), "bldFire");
            AddBuilding("Drill Tower", ZoneType.FireStation, cx, cy, c + new Vector3(inner * 0.4f, 0, inner * 0.3f), new Vector3(4f, 18f, 4f), "bldFire");
            var doors = new MeshBuilder();
            for (int i = 0; i < 3; i++)
                doors.AddBox(0, st.Position + new Vector3((i - 1) * 6f, 2.6f, -st.Size.z * 0.5f - 0.05f), new Vector3(4.5f, 5f, 0.2f));
            doors.Build("Garage Doors", Root, MaterialLibrary.Get("garageDoor"));
            FirePosition = st.Position;
            FireNode = _net.NearestNode(st.Position);
            WorldLabel.Create(Root, st.Position + Vector3.up * (st.Size.y + 8f), "FIRE STATION", new Color(1f, 0.6f, 0.3f), 1.6f);
        }

        private void BuildParking(int cx, int cy, Vector3 c, float inner)
        {
            var stripes = new MeshBuilder();
            int spots = 7;
            float spot = inner / spots;
            for (int row = 0; row < 2; row++)
            {
                float z = (row == 0 ? -1 : 1) * inner * 0.25f;
                for (int i = 0; i <= spots; i++)
                    stripes.AddFlat(0, c + new Vector3(-inner * 0.5f + i * spot, SidewalkTop + 0.02f, z), 0.15f, inner * 0.35f);
                for (int i = 0; i < spots; i++)
                {
                    if (_rng.NextDouble() < 0.35) continue;
                    var car = VehicleFactory.CreateVisual(VehicleKind.Car, _rng.Next(6), Root);
                    car.name = "Parked Car";
                    car.transform.position = c + new Vector3(-inner * 0.5f + (i + 0.5f) * spot, SidewalkTop, z);
                    car.transform.rotation = Quaternion.Euler(0, row == 0 ? 0 : 180, 0);
                }
            }
            Flat(stripes.Build("Parking Stripes", Root, MaterialLibrary.Get("parkingStripe")));
            WorldLabel.Create(Root, c + Vector3.up * 6f, "P  PARKING", new Color(0.55f, 0.8f, 1f), 1.2f);
        }

        private void AmbientParked(VehicleKind kind, Vector3 p, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var v = VehicleFactory.CreateVisual(kind, 0, Root);
                v.name = "Parked " + kind;
                v.transform.position = p + new Vector3(i * 3f, SidewalkTop, 0);
            }
        }

        private void BuildPark(int cx, int cy, Vector3 c, float inner)
        {
            var paths = new MeshBuilder();
            paths.AddFlat(0, c + Vector3.up * (SidewalkTop + 0.02f), 2.5f, inner);
            paths.AddFlat(0, c + Vector3.up * (SidewalkTop + 0.021f), inner, 2.5f);
            Flat(paths.Build("Park Paths", Root, MaterialLibrary.Get("parkPath")));
            var pond = new MeshBuilder();
            pond.AddCylinder(0, c + new Vector3(inner * 0.22f, SidewalkTop + 0.01f, inner * 0.22f), 4.5f, 0.1f, 24);
            Flat(pond.Build("Pond", Root, MaterialLibrary.Get("water")));
            for (int i = 0; i < 14; i++)
            {
                Vector3 p = c + new Vector3(Rand(-inner * 0.45f, inner * 0.45f), SidewalkTop, Rand(-inner * 0.45f, inner * 0.45f));
                if (Mathf.Abs(p.x - c.x) < 2.2f || Mathf.Abs(p.z - c.z) < 2.2f) continue;
                if (Vector3.Distance(p, c + new Vector3(inner * 0.22f, 0, inner * 0.22f)) < 6f) continue;
                Tree(p, Rand(0.8f, 1.3f));
            }
            var benches = new MeshBuilder();
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f;
                Vector3 off = Quaternion.Euler(0, a, 0) * new Vector3(2.5f, 0, inner * 0.3f);
                benches.AddBox(0, c + off + Vector3.up * (SidewalkTop + 0.45f), new Vector3(0.6f, 0.15f, 2f), a);
            }
            benches.Build("Benches", Root, MaterialLibrary.Get("bench"));
            if (cx == _net.Layout.CellsX / 2 && cy == _net.Layout.CellsY / 2)
                WorldLabel.Create(Root, c + Vector3.up * 9f, "CENTRAL PARK", new Color(0.5f, 1f, 0.6f), 1.4f);
        }

        private MeshBuilder _trees;
        private void Tree(Vector3 p, float scale)
        {
            if (_trees == null) _trees = new MeshBuilder(3);
            _trees.AddCylinder(0, p, 0.25f * scale, 2.2f * scale, 6);
            var sphere = PrimitiveMeshes.Get(PrimitiveType.Sphere);
            _trees.AddMesh(_rng.NextDouble() < 0.5 ? 1 : 2, sphere, Matrix4x4.TRS(p + Vector3.up * 3.4f * scale, Quaternion.identity, new Vector3(3f, 3.4f, 3f) * scale));
        }

        // ------------------------------------------------------------------ street furniture

        private void BuildStreetlights()
        {
            var lights = new MeshBuilder(2);
            float rw = _net.RoadWidth;
            foreach (var seg in _net.Segments)
            {
                Vector3 a = _net.NodePosition(seg.NodeA), c = _net.NodePosition(seg.NodeB);
                Vector3 dir = (c - a).normalized;
                Vector3 right = new Vector3(dir.z, 0, -dir.x);
                for (int side = -1; side <= 1; side += 2)
                {
                    float along = side < 0 ? 0.33f : 0.67f;
                    Vector3 basePos = Vector3.Lerp(a, c, along) + right * side * (rw * 0.5f + 1.0f) + Vector3.up * SidewalkTop;
                    lights.AddCylinder(0, basePos, 0.12f, 7.5f, 6);
                    Vector3 armEnd = basePos + Vector3.up * 7.4f - right * side * 1.6f;
                    float yaw = Mathf.Atan2(right.x, right.z) * Mathf.Rad2Deg;
                    lights.AddBox(0, (basePos + Vector3.up * 7.4f + armEnd) * 0.5f, new Vector3(0.12f, 0.12f, 1.6f), yaw);
                    lights.AddBox(1, armEnd - Vector3.up * 0.1f, new Vector3(0.5f, 0.15f, 0.9f), yaw);
                    StreetlightCount++;
                }
            }
            var go = lights.Build("Streetlights", Root, MaterialLibrary.Get("pole"), MaterialLibrary.Get("lampOn"));
            StreetlightRenderers.Add(go.GetComponent<MeshRenderer>());

            if (_trees != null)
            {
                _trees.Build("Trees", Root, MaterialLibrary.Get("treeTrunk"), MaterialLibrary.Get("treeLeaves"), MaterialLibrary.Get("treeLeaves2"));
                _trees = null;
            }
        }

        private void BuildSigns()
        {
            var poles = new MeshBuilder(3);
            float rw = _net.RoadWidth;
            var disc = PrimitiveMeshes.Get(PrimitiveType.Cylinder);
            int i = 0;
            foreach (var j in _net.Intersections)
            {
                if ((j.GridX + j.GridY) % 2 != 0) continue;
                foreach (var lane in j.Outgoing)
                {
                    if (i++ % 3 != 0) continue;
                    Vector3 dir = lane.Direction;
                    Vector3 right = new Vector3(dir.z, 0, -dir.x);
                    Vector3 side = _s.driveOnLeft ? -right : right;
                    Vector3 p = j.Position + dir * (rw * 0.5f + 6f) + side * (rw * 0.5f + 1.2f) + Vector3.up * SidewalkTop;
                    poles.AddCylinder(0, p, 0.07f, 2.6f, 6);
                    // circular speed-limit sign facing traffic coming out of the intersection
                    poles.AddMesh(1, disc, Matrix4x4.TRS(p + Vector3.up * 2.9f, Quaternion.LookRotation(-dir) * Quaternion.Euler(90, 0, 0), new Vector3(0.9f, 0.03f, 0.9f)));
                    poles.AddMesh(2, disc, Matrix4x4.TRS(p + Vector3.up * 2.9f - dir * 0.04f, Quaternion.LookRotation(-dir) * Quaternion.Euler(90, 0, 0), new Vector3(0.62f, 0.03f, 0.62f)));
                }
            }
            poles.Build("Road Signs", Root, MaterialLibrary.Get("pole"), MaterialLibrary.Get("signRed"), MaterialLibrary.Get("white"));
        }

        /// <summary>Removes the generated city (used by the editor preview).</summary>
        public void Clear()
        {
            if (Root != null)
            {
                if (Application.isPlaying) Destroy(Root.gameObject); else DestroyImmediate(Root.gameObject);
            }
            Buildings.Clear(); StreetlightRenderers.Clear();
            Generated = false;
        }
    }
}
