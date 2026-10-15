using UnityEngine;

namespace SmartCity
{
    public enum SelectableKind { None, Intersection, Road, Building, Incident, EmergencyUnit }

    /// <summary>Tag component: clicking an object with a collider and this component selects it.</summary>
    public class Selectable : MonoBehaviour
    {
        public SelectableKind kind;
        public int id;
    }
}
