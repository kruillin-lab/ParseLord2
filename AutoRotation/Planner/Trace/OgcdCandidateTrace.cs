using System;

namespace ParseLord2.AutoRotation.Planner.Trace;

/// <summary>
/// Trace info for a single oGCD candidate, including whether it was allowed and its score.
/// </summary>
internal sealed class OgcdCandidateTrace
{
    public uint ActionId { get; init; }
    public string Label { get; init; } = string.Empty;

    public bool Allowed { get; init; }
    public string BlockReason { get; init; } = string.Empty;

    public float Score { get; init; }

    public override string ToString()
        => $"{Label} ({ActionId}) - {(Allowed ? $"Score {Score:0.0}" : $"Blocked: {BlockReason}")}";
}
