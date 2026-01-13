using Dalamud.Game.ClientState.JobGauge.Types;
using ParseLord2.Combos.PvE;
using static ParseLord2.Combos.PvE.SAM;
using static ParseLord2.CustomComboNS.Functions.CustomComboFunctions;

namespace ParseLord2.AutoRotation.Planner.Samurai;

/// <summary>
/// Deterministic SAM GCD planner ("gold job").
/// Implements Sen-based rotation: Gekko/Kasha branches for buffs, Yukikaze for Setsu,
/// then Iaijutsu (Higanbana @ 1 Sen, Midare @ 3 Sen) with Tsubame follow-ups.
/// </summary>
internal sealed class SamGcdPlanner : IJobGcdPlanner
{
    // Refresh thresholds for Fugetsu (damage buff) and Fuka (speed buff).
    private const float BuffRefreshThreshold = 6.0f;

    // Refresh threshold for Higanbana DoT.
    private const float DotRefreshThreshold = 6.0f;

    public uint NextGcd(PlannerContext ctx)
    {
        // Kaeshi follow-ups are GCD weaponskills that must be executed immediately when available.
        if (ActionReady(TendoKaeshiSetsugekka) && HasStatusEffect(Buffs.TendoKaeshiSetsugekkaReady) && LevelChecked(TendoKaeshiSetsugekka))
            return TendoKaeshiSetsugekka;
        if (ActionReady(KaeshiSetsugekka) && LevelChecked(KaeshiSetsugekka))
            return KaeshiSetsugekka;
        if (ActionReady(KaeshiHiganbana) && LevelChecked(KaeshiHiganbana))
            return KaeshiHiganbana;

        // Check for Iaijutsu usage (Higanbana or Midare).
        uint iaijutsu = TryUseIaijutsu(ctx);
        if (iaijutsu != 0)
            return iaijutsu;

        // Check for Tsubame-gaeshi follow-up after Iaijutsu.
        if (HasStatusEffect(Buffs.TsubameReady) && LevelChecked(TsubameGaeshi))
            return TsubameGaeshi;

        // If combo is active, continue it deterministically.
        if (ctx.ComboTimer > 0.01f)
            return ContinueCombo(ctx);

        // Start a new combo from Hakaze (or Gyofu when Tendo buff active).
        return HasStatusEffect(Buffs.Tendo) ? Gyofu : Hakaze;
    }

    private static uint TryUseIaijutsu(PlannerContext ctx)
    {
        if (!LevelChecked(Iaijutsu))
            return 0;

        var gauge = GetJobGauge<SAMGauge>();
        bool hasGetsu = gauge.HasGetsu;
        bool hasSetsu = gauge.HasSetsu;
        bool hasKa = gauge.HasKa;
        int senCount = (hasGetsu ? 1 : 0) + (hasSetsu ? 1 : 0) + (hasKa ? 1 : 0);

        // Midare / Tendo Setsugekka: 3 Sen finisher (highest priority).
        if (senCount == 3)
        {
            // Don't use if Tsubame is ready (would overwrite it).
            if (HasStatusEffect(Buffs.TsubameReady))
                return 0;

            // When Tendo is active, Midare is replaced by Tendo Setsugekka.
            if (HasStatusEffect(Buffs.Tendo) && LevelChecked(TendoSetsugekka))
                return TendoSetsugekka;

            if (LevelChecked(MidareSetsugekka))
                return MidareSetsugekka;
        }

        // Higanbana: 1 Sen DoT application/refresh.
        // Only use at exactly 1 Sen (don't waste 2-3 Sen on it).
        if (senCount == 1 && LevelChecked(Higanbana))
        {
            float dotRemaining = ctx.TargetStatusRemaining(Debuffs.Higanbana);
            // Apply if missing or needs refresh.
            if (dotRemaining < DotRefreshThreshold)
                return Higanbana;
        }

        // At 2 Sen, don't spend - keep building to 3 for Midare.
        // This prevents getting stuck with 2 Sen when Higanbana is already up.

        return 0;
    }

    private static uint ContinueCombo(PlannerContext ctx)
    {
        uint last = ctx.ComboAction;
        var gauge = GetJobGauge<SAMGauge>();

        // Step 2 decision: after Hakaze/Gyofu, choose branch.
        if (last is Hakaze or Gyofu)
        {
            bool hasGetsu = gauge.HasGetsu;
            bool hasKa = gauge.HasKa;
            bool hasSetsu = gauge.HasSetsu;

            float fugetsuRemaining = ctx.PlayerStatusRemaining(Buffs.Fugetsu);
            float fukaRemaining = ctx.PlayerStatusRemaining(Buffs.Fuka);

            bool needFugetsu = fugetsuRemaining < BuffRefreshThreshold;
            bool needFuka = fukaRemaining < BuffRefreshThreshold;

            // Priority 1: Yukikaze (Setsu) if needed and buffs are good.
            if (!hasSetsu && LevelChecked(Yukikaze) &&
                fugetsuRemaining > 7f && fukaRemaining > 7f)
                return Yukikaze;

            // Priority 2: Refresh Fuka buff via Kasha combo.
            if (needFuka && LevelChecked(Shifu))
                return Shifu;

            // Priority 3: Refresh Fugetsu buff via Gekko combo.
            if (needFugetsu && LevelChecked(Jinpu))
                return Jinpu;

            // Priority 4: Build missing Sen (prefer Kasha > Gekko > Yukikaze).
            // Don't overwrite Sen we already have.
            if (!hasKa && LevelChecked(Shifu))
                return Shifu;

            if (!hasGetsu && LevelChecked(Jinpu))
                return Jinpu;

            if (!hasSetsu && LevelChecked(Yukikaze))
                return Yukikaze;

            // If we have all 3 Sen, just refresh buffs (shouldn't happen - should have spent Sen first).
            // Prioritize the buff that's closest to expiring.
            if (fugetsuRemaining < fukaRemaining && LevelChecked(Jinpu))
                return Jinpu;
            
            if (LevelChecked(Shifu))
                return Shifu;

            // Fallback: Jinpu (Gekko path).
            return LevelChecked(Jinpu) ? Jinpu : (HasStatusEffect(Buffs.Tendo) ? Gyofu : Hakaze);
        }

        // Gekko branch: Jinpu → Gekko (grants Getsu, refreshes Fugetsu).
        if (last == Jinpu && LevelChecked(Gekko))
            return Gekko;

        // Kasha branch: Shifu → Kasha (grants Ka, refreshes Fuka).
        if (last == Shifu && LevelChecked(Kasha))
            return Kasha;

        // After combo finishers (Yukikaze, Gekko, Kasha), restart the combo.
        // Combo timer may still be active, but these actions complete their chains.
        if (last is Yukikaze or Gekko or Kasha)
            return HasStatusEffect(Buffs.Tendo) ? Gyofu : Hakaze;

        // After finisher or unknown, restart.
        return HasStatusEffect(Buffs.Tendo) ? Gyofu : Hakaze;
    }
}