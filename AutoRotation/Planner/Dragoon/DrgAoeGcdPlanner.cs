using ParseLord2.Combos.PvE;
using static ParseLord2.Combos.PvE.DRG;
using static ParseLord2.CustomComboNS.Functions.CustomComboFunctions;

namespace ParseLord2.AutoRotation.Planner.Dragoon;

/// <summary>
/// Deterministic DRG AoE GCD planner ("gold job").
/// Implements a strict 3-GCD AoE combo: DoomSpike -> SonicThrust -> CoerthanTorment.
/// </summary>
internal sealed class DrgAoeGcdPlanner : IJobGcdPlanner
{
    public uint NextGcd(PlannerContext ctx)
    {
        // If combo is active, continue it deterministically.
        if (ctx.ComboTimer > 0.01f)
            return ContinueAoeCombo(ctx);

        // Start new combo: DoomSpike or DraconianFury (when Draconian Fire buff is active).
        return HasStatusEffect(Buffs.DraconianFire) ? DraconianFury : DoomSpike;
    }

    private static uint ContinueAoeCombo(PlannerContext ctx)
    {
        uint last = ctx.ComboAction;

        // Step 2: After DoomSpike/DraconianFury -> SonicThrust
        if (last is DoomSpike or DraconianFury)
        {
            if (LevelChecked(SonicThrust))
                return SonicThrust;

            // Pre-SonicThrust levels: fallback to single-target Power Surge maintenance
            // This is a leveling consideration for completeness.
            return HasStatusEffect(Buffs.DraconianFire) ? DraconianFury : DoomSpike;
        }

        // Step 3: After SonicThrust -> CoerthanTorment (finisher)
        if (last == SonicThrust)
        {
            if (LevelChecked(CoerthanTorment))
                return CoerthanTorment;

            // Pre-CoerthanTorment: restart combo
            return HasStatusEffect(Buffs.DraconianFire) ? DraconianFury : DoomSpike;
        }

        // After finisher (CoerthanTorment) or unknown state, restart combo.
        return HasStatusEffect(Buffs.DraconianFire) ? DraconianFury : DoomSpike;
    }
}
