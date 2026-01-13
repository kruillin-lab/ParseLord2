using ParseLord2.API.Enum;

namespace ParseLord2.AutoRotation;

public class AutoRotationConfig
{
    public bool Enabled;
    public bool InCombatOnly;
    public bool BypassQuest;
    public bool BypassFATE;
    public bool BypassBuffs;
    public int CombatDelay = 1;
    public bool EnableInInstance;
    public bool DisableAfterInstance;
    public DPSRotationMode DPSRotationMode;
    public HealerRotationMode HealerRotationMode;
    public HealerSettings HealerSettings = new();
    public DPSSettings DPSSettings = new();
    public int Throttler = 50;
    public int ManualInputOverrideMs = 1000; // pause autorotation for this many ms after any user input (0 = disabled)
    public bool OrbwalkerIntegration;
}

public class DPSSettings
{
    public bool FATEPriority = false;
    public bool QuestPriority = false;
    public int? DPSAoETargets = 3;
    public bool PreferNonCombat = false;
    public bool OnlyAttackInCombat = false;
    public bool DPSAlwaysHardTarget = false;
    public float MaxDistance = 25;
    public bool AoEIgnoreManual = false;
    public bool UnTargetAndDisableForPenalty = false;
    
    // Vision Cone Settings
    public bool EnableVisionCone = false;
    public float VisionConeAngle = 120f; // Cone angle in degrees (0-360). 180 = half-circle, 360 = full circle
}

public class HealerSettings
{
    public int SingleTargetHPP = 70;
    public int AoETargetHPP = 80;
    public int SingleTargetRegenHPP = 60;
    public int SingleTargetExcogHPP = 50;
    public int? AoEHealTargetCount = 2;
    public int HealDelay = 1;
    public bool ManageKardia = false;
    public bool KardiaTanksOnly = false;
    public bool AutoRez = false;
    public bool AutoRezRequireSwift = false;
    public bool AutoRezDPSJobs = false;
    public bool AutoRezDPSJobsHealersOnly = false;
    public bool AutoRezOutOfParty = false;
    public bool AutoCleanse = false;
    public bool PreEmptiveHoT = false;
    public bool IncludeNPCs = false;
    public bool HealerAlwaysHardTarget = false;

    // RSR-style healing thresholds (used by healer override phase)
    public int TankHealHPP = 90; // if any party tank HP% <= this, healing mode engages
    public int PartyHealHPP = 80; // if any party member HP% <= this, healing mode engages
    public int TankHealHPPWithRegen = 80; // if party tank already has your HoT, use this lower threshold
    public int PartyHealHPPWithRegen = 70; // if party member already has your HoT, use this lower threshold
    public int EmergencyHealHPP = 50; // if any party member HP% <= this, emergency healing engages
    public int AoEHealMinInjuredCount = 3; // prefer AoE heals if this many members are below PartyHealHPP

}