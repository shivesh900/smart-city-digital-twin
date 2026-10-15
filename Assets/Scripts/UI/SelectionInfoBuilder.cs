using System.Text;
using UnityEngine;

namespace SmartCity
{
    /// <summary>Builds the rich-text "Selection details" panel content.</summary>
    public static class SelectionInfoBuilder
    {
        private static string C(Color c, string s) => $"<color=#{UITheme.Hex(c)}>{s}</color>";

        public static string Build(SimulationController sim)
        {
            var sel = sim.selection;
            var net = sim.roadNetwork;
            var sb = new StringBuilder();
            switch (sel.SelectedKind)
            {
                case SelectableKind.Intersection:
                {
                    var j = net.Intersections[sel.SelectedId];
                    var sig = j.Signal;
                    sb.AppendLine($"<b>{j.Name}</b>  <size=12>{C(UITheme.TextDim, "signalised intersection")}</size>");
                    string control = sig.Failed ? C(UITheme.Bad, "FAILED (all-way stop)") : sig.Preempted ? C(UITheme.Accent2, "EMERGENCY PRE-EMPTION")
                        : sim.OptimizationEnabled ? C(UITheme.Good, "Adaptive (queue-based)") : C(UITheme.Warn, "Fixed-time baseline");
                    sb.AppendLine($"Control: {control}");
                    sb.AppendLine($"Phase: {sig.PhaseLabel}{(sig.Failed ? "" : $" - {sig.RemainingInPhase:0.0} s left")}");
                    sb.AppendLine($"Plan: {sig.CurrentPlan}");
                    sb.AppendLine($"<size=12>{C(UITheme.TextDim, sig.CurrentPlan.Reason)}</size>");
                    sb.AppendLine($"Queue  N-S {sig.QueueNS}  |  E-W {sig.QueueEW}   (approaching {sig.ApproachNS} / {sig.ApproachEW})");
                    sb.AppendLine($"Throughput {sig.ThroughputPerMinute:0} veh/min  |  avg wait {sig.AverageWaitSeconds:0.0} s  |  gap-outs {sig.GapOuts}");
                    if (!string.IsNullOrEmpty(sim.routeAdvisor.LastAdvice)) sb.AppendLine(C(UITheme.Good, sim.routeAdvisor.LastAdvice));
                    break;
                }
                case SelectableKind.Road:
                {
                    var s = net.Segments[sel.SelectedId];
                    sb.AppendLine($"<b>{s.Name}</b>  <size=12>{C(UITheme.TextDim, $"{s.Length:0} m, 2 lanes")}</size>");
                    string status = s.Blocked ? C(UITheme.Bad, "BLOCKED") : (s.Forward.CapActive || s.Backward.CapActive) ? C(UITheme.Warn, "ACCIDENT - speed restricted") : C(UITheme.Good, "Open");
                    sb.AppendLine($"Status: {status}");
                    AppendLane(sb, net, s.Forward);
                    AppendLane(sb, net, s.Backward);
                    sb.AppendLine($"<size=12>{C(UITheme.TextDim, "Press ACCIDENT or BLOCK ROAD to create an incident on this road.")}</size>");
                    break;
                }
                case SelectableKind.Building:
                {
                    var b = sim.cityGenerator.Buildings[sel.SelectedId];
                    sb.AppendLine($"<b>{b.Name}</b>  <size=12>{C(UITheme.TextDim, CityLayout.ZoneName(b.Zone) + " zone")}</size>");
                    sb.AppendLine($"Height {b.Size.y:0} m  |  {b.Floors} floors  |  floor area {b.FloorArea:0} m2");
                    sb.AppendLine($"Simulated load: <b>{b.LoadKw:0.0} kW</b>");
                    float aqi = sim.pollution.Aqi[b.CellX, b.CellY];
                    sb.AppendLine($"Block AQI (simulated): {C(PollutionManager.CategoryColor(aqi), $"{aqi:0} {PollutionManager.Category(aqi)}")}");
                    sb.AppendLine($"<size=12>{C(UITheme.TextDim, "Press POLLUTION SPIKE to trigger a spike in this block, or EMERGENCY for a call here.")}</size>");
                    break;
                }
                case SelectableKind.Incident:
                {
                    var inc = sim.incidents.Find(sel.SelectedId);
                    if (inc == null) { sb.AppendLine("Incident no longer exists."); break; }
                    var sev = inc.Severity;
                    sb.AppendLine($"<b>{inc.Title}</b> at {inc.Location(net)}");
                    sb.AppendLine($"Status: <b>{inc.Status}</b>   Severity: {C(UITheme.ForSeverity(sev.Level), $"<b>{sev.Level.ToString().ToUpper()}</b> (score {sev.Score})")}");
                    sb.AppendLine($"<size=12>{C(UITheme.TextDim, string.Join("; ", sev.Reasons))}</size>");
                    if (inc.AssignedUnit != null)
                    {
                        var u = inc.AssignedUnit;
                        string eta = inc.Status == IncidentStatus.Dispatched ? $" - ETA {u.Eta:0} s (simulated)" : "";
                        sb.AppendLine($"Responder: {u.Name} ({u.State}){eta}");
                    }
                    if (inc.ResponseSeconds >= 0) sb.AppendLine($"Response time: <b>{inc.ResponseSeconds:0} s</b>");
                    if (!string.IsNullOrEmpty(inc.RouteNote)) sb.AppendLine(C(new Color(1f, 0.7f, 0.3f), inc.RouteNote));
                    sb.AppendLine($"<b>Suggested:</b> {inc.SuggestedResponse}");
                    break;
                }
                case SelectableKind.EmergencyUnit:
                {
                    var u = sim.dispatch.Units[sel.SelectedId];
                    sb.AppendLine($"<b>{u.Name}</b>  <size=12>{C(UITheme.TextDim, EmergencyDispatchManager.UnitLabel(u.Kind))}</size>");
                    sb.AppendLine($"State: {u.State}");
                    if (u.Target != null) sb.AppendLine($"Assigned: {u.Target.Title}");
                    if (u.State == UnitState.EnRoute) sb.AppendLine($"ETA {u.Eta:0} s (simulated), route {u.PathLength:0} m");
                    sb.AppendLine($"Station junction: {net.NodeName(u.HomeNode)}");
                    break;
                }
                default:
                    sb.AppendLine("<b>Nothing selected</b>");
                    sb.AppendLine("Click a junction, road, building, incident beacon or emergency vehicle in the 3D view.");
                    sb.AppendLine($"<size=12>{C(UITheme.TextDim, "Tip: select a junction and press BLOCK ROAD until every road into it is closed, then press EMERGENCY and DISPATCH to see the honest 'no route' report.")}</size>");
                    break;
            }
            return sb.ToString();
        }

        private static void AppendLane(StringBuilder sb, RoadNetworkManager net, Lane l)
        {
            Color c = RoadNetworkManager.CongestionColor(l.Congestion);
            sb.AppendLine($"{net.NodeName(l.From)} -> {net.NodeName(l.To)}: {l.Vehicles.Count} veh, {l.AverageSpeed * 3.6f:0} km/h, congestion <color=#{UITheme.Hex(c)}>{l.Congestion * 100f:0}%</color>, queue {l.Queue}");
        }
    }
}
