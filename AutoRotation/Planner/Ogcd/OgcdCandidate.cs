using System;

namespace ParseLord2.AutoRotation.Planner.Ogcd;

/// <summary>
/// A single oGCD candidate plus its constraints and scoring hooks.
/// ActionId-centric and designed for explainable trace output.
/// </summary>
internal sealed class OgcdCandidate
{
    public required uint ActionId { get; init; }

    /// <summary>Hard gate. If false, candidate is blocked and cannot be selected.</summary>
    public Func<bool>? HardOk { get; init; }

    /// <summary>
    /// Optional reason string when <see cref="HardOk"/> evaluates false.
    /// Only used for trace/debugging.
    /// </summary>
    public Func<string>? BlockReason { get; init; }

    /// <summary>Soft scoring. Higher wins. If null, defaults to 0.</summary>
    public Func<float>? Score { get; init; }

    /// <summary>Optional debug label for trace UI.</summary>
    public string? Label { get; init; }
}
