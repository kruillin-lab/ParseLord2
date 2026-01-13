using System;
using System.Collections.Concurrent;

namespace ParseLord2.AutoRotation.Planner.Trace;

/// <summary>
/// Stores the latest planner decision trace per job/preset so the UI can display it.
/// This is intentionally minimal: last-value cache only.
/// </summary>
internal static class PlannerTraceStore
{
    private static readonly ConcurrentDictionary<string, PlannerDecisionTrace> Last = new();

    public static void Set(string key, PlannerDecisionTrace trace)
    {
        if (string.IsNullOrEmpty(key) || trace is null) return;
        Last[key] = trace;
    }

    public static PlannerDecisionTrace? Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        return Last.TryGetValue(key, out var v) ? v : null;
    }

    public static PlannerDecisionTrace? GetLatest()
    {
        PlannerDecisionTrace? best = null;
        foreach (var kv in Last)
        {
            if (best is null || kv.Value.TimestampUtc > best.TimestampUtc)
                best = kv.Value;
        }
        return best;
    }
}
