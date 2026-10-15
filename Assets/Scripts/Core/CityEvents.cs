using System;
using UnityEngine;

namespace SmartCity
{
    public enum AlertLevel { Info, Success, Warning, Critical }

    /// <summary>Lightweight event hub so systems do not need hard references to the UI.</summary>
    public static class CityEvents
    {
        public static event Action<string, AlertLevel> Alert;
        public static event Action IncidentsChanged;
        public static event Action SimulationReset;
        public static event Action<SimulationState> StateChanged;

        public static void RaiseAlert(string message, AlertLevel level = AlertLevel.Info)
        {
            if (level == AlertLevel.Critical) Debug.LogWarning("[SmartCity] " + message);
            else Debug.Log("[SmartCity] " + message);
            Alert?.Invoke(message, level);
        }

        public static void RaiseIncidentsChanged() => IncidentsChanged?.Invoke();
        public static void RaiseSimulationReset() => SimulationReset?.Invoke();
        public static void RaiseStateChanged(SimulationState s) => StateChanged?.Invoke(s);

        // Static events survive between Play sessions when "Enter Play Mode Options" skip domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Alert = null; IncidentsChanged = null; SimulationReset = null; StateChanged = null;
        }
    }
}
