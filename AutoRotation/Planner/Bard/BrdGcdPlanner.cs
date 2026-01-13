using ParseLord2.Combos.PvE;
using static ParseLord2.Combos.PvE.BRD;
using static ParseLord2.CustomComboNS.Functions.CustomComboFunctions;

namespace ParseLord2.AutoRotation.Planner.Bard;

/// <summary>
/// Deterministic BRD GCD planner ("gold job").
/// Goal: never idle, keep DoTs up tightly, and spend key GCD resources (Apex/Blast) without blocking GCD flow.
/// </summary>
internal sealed class BrdGcdPlanner : IJobGcdPlanner
{
    // Meta-ish refresh threshold (seconds remaining).
    // Tighten later via config; keep deterministic & safe now.
    private const float DotRefreshThreshold = 3.0f;

    public uint NextGcd(PlannerContext ctx)
    {
        // If no target, fall back to primary filler.
        if (!HasBattleTarget())
            return OriginalHook(BurstShot);

        // DoT application / refresh (Stormbite + Causticbite via hooks, Iron Jaws refresh).
        // If either DoT missing, apply missing DoT first.
        if (DebuffCapCanBlue && BlueRemaining <= 0.1f && LevelChecked(Windbite))
            return OriginalHook(Windbite);

        if (DebuffCapCanPurple && PurpleRemaining <= 0.1f && LevelChecked(VenomousBite))
            return OriginalHook(VenomousBite);

        // Refresh both DoTs via Iron Jaws when close to expiry.
        if (LevelChecked(IronJaws) && (BlueRemaining > 0.1f || PurpleRemaining > 0.1f) &&
            (BlueRemaining <= DotRefreshThreshold || PurpleRemaining <= DotRefreshThreshold))
            return IronJaws;

        // Burst-proc / high value GCDs.
        if (HasStatusEffect(Buffs.Barrage))
            return OriginalHook(StraightShot);

        if (HasStatusEffect(Buffs.BlastArrowReady) && LevelChecked(BlastArrow))
            return BlastArrow;

        // Spend Apex at 90+ in burst, or once-per-cycle during Mage's Ballad (non-burst).
        // Apex is never allowed to block the GCD spine; if conditions aren't met, fall through to fillers.
        if (LevelChecked(ApexArrow) && gauge.SoulVoice >= 90)
        {
            var inBurst = HasStatusEffect(Buffs.RagingStrikes) || HasStatusEffect(Buffs.BattleVoice) ||
                         HasStatusEffect(Buffs.RadiantFinale);

            if (inBurst || SongMage)
                return ApexArrow;
        }

        if (HasStatusEffect(Buffs.ResonantArrowReady) && LevelChecked(ResonantArrow))
            return ResonantArrow;

        if (HasStatusEffect(Buffs.RadiantEncoreReady) && LevelChecked(RadiantEncore))
            return OriginalHook(RadiantEncore);

        // Standard proc (Hawk's Eye / Straight Shot Ready) -> Straight Shot / Refulgent.
        if (HasStatusEffect(Buffs.HawksEye))
            return OriginalHook(StraightShot);

        // Default filler.
        return OriginalHook(BurstShot);
    }
}
