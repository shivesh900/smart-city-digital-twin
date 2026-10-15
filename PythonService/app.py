"""
OPTIONAL external forecasting service for the Smart City Digital Twin.

The Unity project does NOT need this. It is off by default
(SimulationSettings > useExternalService = false) and the simulation runs fully offline.

What it does (honestly): ordinary least-squares linear trend on the congestion series
Unity sends, extrapolated `horizon` seconds ahead and clamped to 0-100 %.
It is a transparent statistical baseline, NOT a trained machine-learning model.
If you later train a real model (e.g. scikit-learn), replace `forecast()` and change MODEL_NAME.

Run:
    pip install -r requirements.txt
    python app.py            # serves http://127.0.0.1:5000
Then in Unity: select Assets/ScriptableObjects/SimulationSettings.asset, tick
"Use External Service", press Play. The Decision-support panel shows the service status.
"""
from flask import Flask, jsonify, request

MODEL_NAME = "python-linear-trend (not ML)"
SAMPLE_INTERVAL_S = 1.0  # matches SimulationSettings.sampleInterval default

app = Flask(__name__)


def forecast(series, horizon, sample_interval=SAMPLE_INTERVAL_S, window=30):
    """Least-squares line through the last `window` samples, evaluated `horizon` s past the last one."""
    ys = [float(v) for v in series][-window:]
    if not ys:
        return 0.0
    if len(ys) == 1:
        return max(0.0, min(100.0, ys[0]))
    xs = [i * sample_interval for i in range(len(ys))]
    n = len(ys)
    mx, my = sum(xs) / n, sum(ys) / n
    sxx = sum((x - mx) ** 2 for x in xs)
    slope = sum((x - mx) * (y - my) for x, y in zip(xs, ys)) / sxx if sxx else 0.0
    intercept = my - slope * mx
    value = intercept + slope * (xs[-1] + float(horizon))
    return max(0.0, min(100.0, value))


@app.post("/predict")
def predict():
    body = request.get_json(force=True, silent=True) or {}
    series = body.get("series") or []
    horizon = body.get("horizon", 30)
    return jsonify({"forecast": round(forecast(series, horizon), 2), "model": MODEL_NAME})


@app.get("/health")
def health():
    return jsonify({"ok": True, "model": MODEL_NAME})


if __name__ == "__main__":
    app.run(host="127.0.0.1", port=5000)
