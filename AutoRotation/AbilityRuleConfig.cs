using System;

namespace ParseLord2.AutoRotation;

/// <summary>
/// Per-ability (per AutoAction) configuration modeled after RotationSolverReborn's ActionConfig.
///
/// Design goals:
/// - Stable serialization surface (avoid config/UI/model drift)
/// - Generic "modules" that can apply to any ability
/// - Defaults mean "no extra gating" unless explicitly configured
/// </summary>
[Serializable]
public sealed class AbilityRuleConfig
{
    // ----- Core toggles -----
    public bool IsEnabled = true;

    // RSR parity toggles (may be unused by engine initially, but kept for UI + future work)
    public bool IsIntercepted = false;
    public bool IsRestrictedDOT = false;

    // ----- Status gating -----
    /// <summary>If true, ability is treated like a "maintenance" action (refresh status early).</summary>
    public bool ShouldCheckStatus = false;

    /// <summary>If true, status check is applied to target status instead of self status (engine-defined).</summary>
    public bool ShouldCheckTargetStatus = false;

    /// <summary>
    /// How early to refresh (in GCDs). 0 disables the refresh window.
    /// Interpretation is engine-defined.
    /// </summary>
    public int StatusGcdCount = 0;

    // ----- Combo gating -----
    public bool ShouldCheckCombo = false;

    // ----- AoE gating -----
    /// <summary>Minimum targets to treat this ability as eligible AoE. 0 disables AoE gating.</summary>
    public int AoeCount = 0;

    // ----- Time-to-kill gating -----
    /// <summary>Seconds. 0 disables time-to-kill gating.</summary>
    public int TimeToKill = 0;

    // ----- Healing thresholds -----
    /// <summary>
    /// 0..1 ratio. 0 disables heal gating. Intended for healer-override phases.
    /// </summary>
    public float AutoHealRatio = 0f;

    /// <summary>
    /// Optional RSR-style "with HoT" vs "without HoT" thresholds.
    /// Use 0 to disable either field.
    /// Interpretation is engine-defined.
    /// </summary>
    public int HealHppWithoutHot = 0;
    public int HealHppWithHot = 0;

    // ----- Cooldown / burst gating -----
    public bool IsOnCooldownWindow = false;

    // ----- Safety / min-hp feature -----
    public bool MinHPFeature = false;
    public float MinHPPercent = 0f;
}
