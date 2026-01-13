namespace ParseLord2.API.Enum;

using System;
using global::ParseLord2.API.Extension;

/// <summary>
/// Minimal compatibility surface for IPC/config code paths that reference ParseLord2.API.*.
/// Keep this file small and additive (no breaking renames).
/// </summary>
public enum DPSRotationMode
{
    Manual = 0,
    Nearest = 1,
    Furthest = 2,
    Tank_Target = 3,
    Highest_Current = 4,
    Lowest_Current = 5,
    Highest_Max = 6,
    Lowest_Max = 7,
}

public enum HealerRotationMode
{
    Manual = 0,
    Lowest_Current = 1,
    Highest_Current = 2,
}

/// <summary>
/// Options exposed over IPC for AutoRotation config.
/// Each member declares the type of value expected via <see cref="ConfigValueTypeAttribute"/>.
/// </summary>
public enum AutoRotationConfigOption
{
    [ConfigValueType(typeof(bool))]
    InCombatOnly = 0,

    [ConfigValueType(typeof(bool))]
    IncludeNPCs = 1,

    [ConfigValueType(typeof(DPSRotationMode))]
    DPSRotationMode = 2,

    [ConfigValueType(typeof(HealerRotationMode))]
    HealerRotationMode = 3,

    [ConfigValueType(typeof(bool))]
    AutoRez = 4,

    [ConfigValueType(typeof(bool))]
    AutoRezDPSJobs = 5,

    // Additional options referenced by IPC providers
    [ConfigValueType(typeof(bool))]
    FATEPriority = 6,

    [ConfigValueType(typeof(bool))]
    QuestPriority = 7,

    [ConfigValueType(typeof(int))]
    SingleTargetHPP = 8,

    [ConfigValueType(typeof(int))]
    AoETargetHPP = 9,

    [ConfigValueType(typeof(int))]
    SingleTargetRegenHPP = 10,

    [ConfigValueType(typeof(int))]
    SingleTargetExcogHPP = 11,

    [ConfigValueType(typeof(bool))]
    ManageKardia = 12,

    [ConfigValueType(typeof(bool))]
    AutoRezDPSJobsHealersOnly = 13,

    [ConfigValueType(typeof(bool))]
    AutoCleanse = 14,

    [ConfigValueType(typeof(bool))]
    OnlyAttackInCombat = 15,

    [ConfigValueType(typeof(bool))]
    OrbwalkerIntegration = 16,

    [ConfigValueType(typeof(bool))]
    AutoRezOutOfParty = 17,

    [ConfigValueType(typeof(int))]
    DPSAoETargets = 18,
}

public enum SetResult
{
    Okay = 0,
    OkayWorking = 1,
    IGNORED = 2,
    Duplicate = 3,
    InvalidLease = 4,
    BlacklistedLease = 5,
    InvalidConfiguration = 6,
    InvalidValue = 7,
    IPCDisabled = 8,
    PlayerNotAvailable = 9,
}

public enum ComboTargetTypeKeys
{
    SingleTarget = 0,
    MultiTarget = 1,
    HealST = 2,
    HealMT = 3,
    Other = 99,
}

public enum ComboSimplicityLevelKeys
{
    Simple = 0,
    Advanced = 1,
    Other = 99,
}

public enum ComboStateKeys
{
    Enabled = 0,
    AutoMode = 1,
}

public enum CancellationReason
{
    JobChanged = 0,
    LeaseeReleased = 1,
    WrathPluginDisabled = 2,
    WrathUserManuallyCancelled = 3,

    // Additive: referenced in some older iterations / forks.
    AllServicesSuspended = 10,
    LeaseePluginDisabled = 11,
}

public enum BailMessage
{
    LiveDisabled = 0,
    InvalidLease = 1,
    BlacklistedLease = 2,
}
