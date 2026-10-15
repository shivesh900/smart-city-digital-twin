import types, sys
# allow running without Flask installed
try:
    import flask  # noqa: F401
except ImportError:
    fake = types.ModuleType("flask")
    class _App:
        def __init__(self, *a, **k): pass
        def post(self, *a, **k): return lambda f: f
        def get(self, *a, **k): return lambda f: f
    fake.Flask = _App; fake.jsonify = lambda *a, **k: None; fake.request = None
    sys.modules["flask"] = fake
from app import forecast

assert forecast([], 30) == 0.0
assert abs(forecast([10, 10, 10], 30) - 10) < 1e-9
assert abs(forecast([10, 11, 12, 13], 2) - 15) < 1e-9   # slope 1 %/s
assert forecast([90, 95, 99], 60) == 100.0               # clamped
assert forecast([20, 10, 0], 60) == 0.0                  # clamped
print("PythonService forecast tests: 5 passed")
