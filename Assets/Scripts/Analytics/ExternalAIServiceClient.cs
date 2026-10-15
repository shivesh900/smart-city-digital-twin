using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SmartCity
{
    /// <summary>
    /// OPTIONAL: talks to the Python/Flask stub in /PythonService. Disabled by default
    /// (SimulationSettings.useExternalService = false); the simulation never depends on it.
    /// When enabled it posts the recent congestion series and shows the service's forecast as a second opinion.
    /// </summary>
    public class ExternalAIServiceClient : MonoBehaviour
    {
        private SimulationController _sim;
        public string LastResponse { get; private set; } = "";

        [System.Serializable]
        private class PredictRequest { public float[] series; public float horizon; }

        [System.Serializable]
        private class PredictResponse { public float forecast; public string model; }

        public void Initialize(SimulationController sim)
        {
            _sim = sim;
            if (sim.settings.useExternalService) StartCoroutine(Loop());
            else sim.analytics.ExternalServiceStatus = "Not used (offline mode)";
        }

        private IEnumerator Loop()
        {
            var wait = new WaitForSecondsRealtime(10f);
            while (true)
            {
                yield return wait;
                var series = _sim.analytics.CongestionSeries;
                var req = new PredictRequest { series = new float[series.Count], horizon = _sim.settings.forecastHorizonSeconds };
                for (int i = 0; i < series.Count; i++) req.series[i] = series[i];
                string url = _sim.settings.externalServiceUrl.TrimEnd('/') + "/predict";
                using (var www = new UnityWebRequest(url, "POST"))
                {
                    www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(req)));
                    www.downloadHandler = new DownloadHandlerBuffer();
                    www.SetRequestHeader("Content-Type", "application/json");
                    www.timeout = 3;
                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        var res = JsonUtility.FromJson<PredictResponse>(www.downloadHandler.text);
                        LastResponse = $"{res.forecast:0.0}% ({res.model})";
                        _sim.analytics.ExternalServiceStatus = "External service forecast: " + LastResponse;
                    }
                    else
                    {
                        _sim.analytics.ExternalServiceStatus = "External service offline - using built-in model (" + www.error + ")";
                    }
                }
            }
        }
    }
}
