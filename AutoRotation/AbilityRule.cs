using System;

namespace ParseLord2.AutoRotation;

/// <summary>
/// Per-ability overrides for auto-rotation logic.
/// Kept intentionally small and generic so it can expand over time.
/// </summary>
public class AbilityRule
{
    /// <summary>
    /// If true, use this rule's custom ST HP threshold instead of the global healer setting.
    /// Applies to healing actions that are marked as single-target auto actions.
    /// </summary>
    public bool OverrideSingleTargetHpThreshold { get; set; } = false;

    /// <summary>
    /// The HP% at or below which the target is considered eligible for this single-target heal.
    /// Only used when <see cref="OverrideSingleTargetHpThreshold"/> is true.
    /// </summary>
    public int SingleTargetHpThreshold { get; set; } = 70;

    /// <summary>
    /// If true, use this rule's custom AoE HP threshold instead of the global healer setting.
    /// Applies to healing actions that are marked as AoE auto actions.
    /// </summary>
    public bool OverrideAoEHpThreshold { get; set; } = false;

    /// <summary>
    /// Party member HP% at or below which they count as "injured" for this AoE heal.
    /// Only used when <see cref="OverrideAoEHpThreshold"/> is true.
    /// </summary>
    public int AoEHpThreshold { get; set; } = 80;

    /// <summary>
    /// If true, use this rule's custom AoE injured-count threshold instead of the global healer setting.
    /// </summary>
    public bool OverrideAoEInjuredCount { get; set; } = false;

    /// <summary>
    /// Minimum number of injured members required to allow this AoE heal.
    /// Only used when <see cref="OverrideAoEInjuredCount"/> is true.
    /// </summary>
    public int AoEInjuredCount { get; set; } = 2;

    public void Clamp()
    {
        SingleTargetHpThreshold = Math.Clamp(SingleTargetHpThreshold, 1, 100);
        AoEHpThreshold = Math.Clamp(AoEHpThreshold, 1, 100);
        AoEInjuredCount = Math.Clamp(AoEInjuredCount, 1, 8);
    }
}
