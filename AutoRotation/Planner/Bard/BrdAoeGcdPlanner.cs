using ParseLord2.Combos.PvE;
using static ParseLord2.Combos.PvE.BRD;
using static ParseLord2.CustomComboNS.Functions.CustomComboFunctions;

namespace ParseLord2.AutoRotation.Planner.Bard;

/// <summary>
/// Deterministic BRD AoE GCD planner.
/// Goal: never idle, keep AoE filler rolling, and spend Apex/Blast more freely on packs.
/// </summary>
internal sealed class BrdAoeGcdPlanner : IJobGcdPlanner
{
    public uint NextGcd(PlannerContext ctx)
    {
        if (!HasBattleTarget())
            return OriginalHook(QuickNock);

        // Blast Arrow proc.
        if (HasStatusEffect(Buffs.BlastArrowReady) && LevelChecked(BlastArrow))
            return BlastArrow;

        // AoE Apex policy: 80+ on packs.
        if (LevelChecked(ApexArrow) && gauge.SoulVoice >= 80)
            return ApexArrow;

        // Shadowbite proc (if present).
        if (HasStatusEffect(Buffs.ShadowbiteReady) && LevelChecked(Shadowbite))
            return Shadowbite;

        // Default AoE filler (hook handles Ladonsbite upgrade).
        return OriginalHook(QuickNock);
    }
}
