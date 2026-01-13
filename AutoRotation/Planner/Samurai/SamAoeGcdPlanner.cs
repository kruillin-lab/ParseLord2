using Dalamud.Game.ClientState.JobGauge.Types;
using ParseLord2.Combos.PvE;
using static ParseLord2.Combos.PvE.SAM;
using static ParseLord2.CustomComboNS.Functions.CustomComboFunctions;

namespace ParseLord2.AutoRotation.Planner.Samurai;

/// <summary>
/// Deterministic SAM AoE GCD planner ("gold job").
/// Implements Sen-based AoE rotation: Mangetsu/Oka branches for buffs + Sen,
/// then Tenka Goken (2-3 Sen AoE Iaijutsu) with Kaeshi Goken follow-up.
/// </summary>
internal sealed class SamAoeGcdPlanner : IJobGcdPlanner
{
    // Refresh thresholds for Fugetsu (damage buff) and Fuka (speed buff).
    private const float BuffRefreshThreshold = 6.0f;

    public uint NextGcd(PlannerContext ctx)
    {
        // Check for Tenka Goken (AoE Iaijutsu at 2-3 Sen).
        uint iaijutsu = TryUseTenkaGoken(ctx);
        if (iaijutsu != 0)
            return iaijutsu;

        // If combo is active, continue it deterministically.
        if (ctx.ComboTimer > 0.01f)
            return ContinueAoeCombo(ctx);

        // Start a new combo from Fuga (or Fuko when Tendo buff active).
        return HasStatusEffect(Buffs.Tendo) ? Fuko : Fuga;
    }

    private static uint TryUseTenkaGoken(PlannerContext ctx)
    {
        if (!LevelChecked(TenkaGoken))
            return 0;

        var gauge = GetJobGauge<SAMGauge>();
        bool hasGetsu = gauge.HasGetsu;
        bool hasKa = gauge.HasKa;
        int senCount = (hasGetsu ? 1 : 0) + (hasKa ? 1 : 0);

        // Tenka Goken: 2-3 Sen AoE finisher.
        // At 3 Sen, use if Tsubame buffs aren't active (would overwrite).
        // At 2 Sen, use if pre-Midare levels (Tenka is the only option).
        if (senCount >= 2 && !HasStatusEffect(Buffs.TsubameReady) &&
            !HasStatusEffect(Buffs.KaeshiGokenReady) &&
            !HasStatusEffect(Buffs.TendoKaeshiGokenReady))
        {
            // Prefer 3 Sen when possible, but 2 Sen is acceptable for AoE.
            if (senCount == 3 || !LevelChecked(MidareSetsugekka))
                return TenkaGoken;
        }

        return 0;
    }

    private static uint ContinueAoeCombo(PlannerContext ctx)
    {
        uint last = ctx.ComboAction;
        var gauge = GetJobGauge<SAMGauge>();

        // Step 2 decision: after Fuga/Fuko, choose branch.
        if (last is Fuga or Fuko)
        {
            bool hasGetsu = gauge.HasGetsu;
            bool hasKa = gauge.HasKa;

            float fugetsuRemaining = ctx.PlayerStatusRemaining(Buffs.Fugetsu);
            float fukaRemaining = ctx.PlayerStatusRemaining(Buffs.Fuka);

            bool needFugetsu = fugetsuRemaining < BuffRefreshThreshold;
            bool needFuka = fukaRemaining < BuffRefreshThreshold;

            // Priority 1: Refresh Fugetsu buff via Mangetsu.
            if (needFugetsu && LevelChecked(Mangetsu))
                return Mangetsu;

            // Priority 2: Refresh Fuka buff via Oka.
            if (needFuka && LevelChecked(Oka))
                return Oka;

            // Priority 3: Build missing Sen (prefer Mangetsu > Oka).
            // Mangetsu is typically used first in AoE rotation.
            if (!hasGetsu && LevelChecked(Mangetsu))
                return Mangetsu;

            if (!hasKa && LevelChecked(Oka))
                return Oka;

            // Fallback: Mangetsu (default AoE branch).
            return LevelChecked(Mangetsu) ? Mangetsu : (HasStatusEffect(Buffs.Tendo) ? Fuko : Fuga);
        }

        // After finisher or unknown, restart.
        return HasStatusEffect(Buffs.Tendo) ? Fuko : Fuga;
    }
}