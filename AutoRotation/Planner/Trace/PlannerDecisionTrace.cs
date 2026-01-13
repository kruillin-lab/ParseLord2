using System;
using System.Collections.Generic;
using ParseLord2.AutoRotation.Planner;

namespace ParseLord2.AutoRotation.Planner.Trace;

/// <summary>
/// A lightweight, last-tick snapshot of what the Hybrid Planner decided, for UI/debugging.
/// Phase 3A: Minimum viable Decision Trace UI.
/// </summary>
internal sealed class PlannerDecisionTrace
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public string JobLabel { get; init; } = string.Empty;

    public uint PlannedNextGcdActionId { get; init; }
    public uint SelectedOgcdActionId { get; init; }

    public int TargetCountEstimate { get; init; }

    public BurstPhase BurstPhase { get; init; }

    public int WeaveSlotsUsed { get; init; }
    public int WeaveSlotsMax { get; init; }
    public bool IsInLegalWeaveWindow { get; init; }

    public IReadOnlyList<OgcdCandidateTrace> OgcdCandidates { get; init; } = Array.Empty<OgcdCandidateTrace>();
}
