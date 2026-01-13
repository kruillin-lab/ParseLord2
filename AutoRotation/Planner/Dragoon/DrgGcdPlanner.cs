using ParseLord2.Combos.PvE;
using static ParseLord2.Combos.PvE.DRG;
using static ParseLord2.CustomComboNS.Functions.CustomComboFunctions;

namespace ParseLord2.AutoRotation.Planner.Dragoon;

/// <summary>
/// Deterministic DRG GCD planner ("gold job").
/// Implements a strict 10-GCD spine: Chaotic Spring combo <-> Heavens' Thrust combo,
/// with forced refresh overrides when DoT / Power Surge are near expiry.
/// </summary>
internal sealed class DrgGcdPlanner : IJobGcdPlanner
{
    // Conservative refresh thresholds (seconds remaining).
    // Tune later via config + Decision Trace UI; keep deterministic now.
    private const float DotRefreshThreshold = 6.0f;
    private const float BuffRefreshThreshold = 6.0f;

    public uint NextGcd(PlannerContext ctx)
    {
        // If combo is active, continue it deterministically.
        if (ctx.ComboTimer > 0.01f)
            return ContinueCombo(ctx);

        // Otherwise start a new combo. Decide which side (Chaotic Spring vs Heavens') based on refresh needs.
        bool needDot = ctx.TargetStatusRemaining(Debuffs.ChaoticSpring) < DotRefreshThreshold;
        bool needBuff = ctx.PlayerStatusRemaining(Buffs.PowerSurge) < BuffRefreshThreshold;

        // Start with Raiden if available, otherwise True. Branching happens at step 2.
        return HasStatusEffect(Buffs.DraconianFire) ? RaidenThrust : TrueThrust;
    }

    private static uint ContinueCombo(PlannerContext ctx)
    {
        // Normalize starter action (Raiden counts like True for routing).
        uint last = ctx.ComboAction;

        // Step 2 decision: after True/Raiden, choose Power Surge path (Disembowel/Spiral Blow) vs Heavens path (Vorpal/Lance Barrage).
        if (last is TrueThrust or RaidenThrust)
        {
            bool needDot = ctx.TargetStatusRemaining(Debuffs.ChaoticSpring) < DotRefreshThreshold;
            bool needBuff = ctx.PlayerStatusRemaining(Buffs.PowerSurge) < BuffRefreshThreshold;

            if (needDot || needBuff)
            {
                // Spiral Blow is the direct upgrade at higher levels.
                return LevelChecked(SpiralBlow) ? SpiralBlow : Disembowel;
            }

            // Lance Barrage is the direct upgrade at higher levels.
            return LevelChecked(LanceBarrage) ? LanceBarrage : VorpalThrust;
        }

        // Chaotic Spring branch: Disembowel/Spiral Blow -> Chaotic Spring/Chaos Thrust
        if (last is Disembowel or SpiralBlow)
        {
            // Prefer Chaotic Spring if learned, else Chaos Thrust.
            return LevelChecked(ChaoticSpring) ? ChaoticSpring : ChaosThrust;
        }

        // Heavens' branch: Vorpal/Lance Barrage -> Heavens' Thrust/Full Thrust
        if (last is VorpalThrust or LanceBarrage)
        {
            return LevelChecked(HeavensThrust) ? HeavensThrust : FullThrust;
        }

        // Step 4: after the 3rd step, the valid follow-up depends on which branch we are on.
        if (last is ChaoticSpring or ChaosThrust)
        {
            // Chaotic Spring combo -> Wheeling Thrust
            return WheelingThrust;
        }

        if (last is HeavensThrust or FullThrust)
        {
            // Heavens' Thrust combo -> Fang and Claw
            return FangAndClaw;
        }

        // Step 5: after 4th step, go to Drakesbane if learned, else restart.
        if (last is WheelingThrust or FangAndClaw)
            return LevelChecked(Drakesbane) ? Drakesbane : (HasStatusEffect(Buffs.DraconianFire) ? RaidenThrust : TrueThrust);

        // After finisher or unknown, restart.
        return HasStatusEffect(Buffs.DraconianFire) ? RaidenThrust : TrueThrust;
    }
}
