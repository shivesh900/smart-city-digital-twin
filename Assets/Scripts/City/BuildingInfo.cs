using UnityEngine;

namespace SmartCity
{
    /// <summary>Registry entry for one generated building (used by energy, selection and layers).</summary>
    public sealed class BuildingInfo
    {
        public int Id;
        public string Name;
        public ZoneType Zone;
        public int CellX;
        public int CellY;
        public Vector3 Position;
        public Vector3 Size;
        public int Floors;
        public float FloorArea;
        public MeshRenderer Renderer;
        public Material[] OriginalMaterials;
        /// <summary>Current simulated electrical load in kW (EnergyManager).</summary>
        public float LoadKw;
    }
}
