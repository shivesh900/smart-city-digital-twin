using UnityEngine;

namespace SmartCity
{
    /// <summary>
    /// All tunable parameters in one ScriptableObject (Assets/ScriptableObjects/SimulationSettings.asset).
    /// Change values in the Inspector and press Play - no code changes needed.
    /// </summary>
    [CreateAssetMenu(menuName = "Smart City/Simulation Settings", fileName = "SimulationSettings")]
    public class SimulationSettings : ScriptableObject
    {
        [Header("City layout")]
        [Range(3, 9)] public int gridColumns = 6;
        [Range(3, 9)] public int gridRows = 6;
        [Tooltip("Distance between neighbouring intersections (metres)")]
        [Range(36f, 80f)] public float blockSpacing = 48f;
        [Range(8f, 16f)] public float roadWidth = 12f;
        [Range(1.5f, 5f)] public float sidewalkWidth = 3f;
        [Tooltip("Lane centre distance from the road centre line")]
        public float laneOffset = 2.4f;
        [Tooltip("India and the UK drive on the left")]
        public bool driveOnLeft = true;
        [Tooltip("Random seed: the same seed always produces the same city and the same traffic")]
        public int seed = 2027;

        [Header("Traffic")]
        [Range(0, 300)] public int initialVehicles = 80;
        [Range(10, 400)] public int maxVehicles = 180;
        [Range(1, 50)] public int spawnBatch = 10;
        public float carSpeedKmh = 50f;
        public float busSpeedKmh = 40f;
        public float truckSpeedKmh = 40f;
        public float emergencySpeedKmh = 72f;
        [Range(0f, 0.4f)] public float busShare = 0.1f;
        [Range(0f, 0.4f)] public float truckShare = 0.08f;
        [Tooltip("Share of trips that head to the commercial centre (creates realistic, uneven demand)")]
        [Range(0f, 1f)] public float downtownTripShare = 0.55f;
        [Tooltip("Fixed simulation step in seconds (deterministic)")]
        public float simulationStep = 0.05f;
        [Range(0.25f, 8f)] public float defaultSimSpeed = 1f;
        public float maxSimSpeed = 8f;

        [Header("Signals")]
        public float baselineGreen = 14f;
        public float yellowTime = 3f;
        public float allRedTime = 1f;
        public float minGreen = 6f;
        public float maxGreen = 30f;

        [Header("Environment (simulated values)")]
        [Tooltip("Alert when simulated AQI crosses this value")]
        public float aqiWarningThreshold = 200f;
        public float aqiCriticalThreshold = 300f;
        public bool timeOfDayEnabled = true;
        [Tooltip("Simulated hour when the simulation starts")]
        [Range(0f, 24f)] public float startHour = 17.5f;
        [Tooltip("Real minutes for one simulated day at 1x speed")]
        public float dayLengthMinutes = 12f;

        [Header("Emergency services")]
        [Range(1, 4)] public int ambulances = 2;
        [Range(1, 4)] public int fireTrucks = 2;
        [Range(1, 4)] public int policeCars = 2;
        public float onSceneSeconds = 12f;
        [Tooltip("Resolve incidents automatically after responders finish on scene")]
        public bool autoResolveAfterResponse = false;

        [Header("Analytics")]
        public float sampleInterval = 1f;
        [Range(30, 300)] public int chartPoints = 120;
        public float forecastHorizonSeconds = 30f;
        [Tooltip("Simulated seconds measured per scenario in the automatic A/B test")]
        public float abTestSeconds = 120f;
        public float abWarmupSeconds = 25f;
        public float abTestSpeed = 8f;

        [Header("Optional external AI service (off by default)")]
        public bool useExternalService = false;
        public string externalServiceUrl = "http://127.0.0.1:5000";

        public float CarSpeed => carSpeedKmh / 3.6f;
        public float EmergencySpeed => emergencySpeedKmh / 3.6f;

        public static SimulationSettings CreateDefault()
        {
            var s = CreateInstance<SimulationSettings>();
            s.name = "SimulationSettings (runtime default)";
            return s;
        }

        private void OnValidate()
        {
            if (maxVehicles < initialVehicles) maxVehicles = initialVehicles;
            if (maxGreen < minGreen) maxGreen = minGreen;
            simulationStep = Mathf.Clamp(simulationStep, 0.02f, 0.2f);
            dayLengthMinutes = Mathf.Max(1f, dayLengthMinutes);
        }
    }
}
