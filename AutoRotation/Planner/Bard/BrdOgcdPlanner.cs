using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.JobGauge.Enums;
using Dalamud.Game.ClientState.JobGauge.Types;
using ParseLord2.AutoRotation.Planner;
using ParseLord2.AutoRotation.Planner.Ogcd;
using ParseLord2.AutoRotation.Planner.Trace;
using static ParseLord2.CustomComboNS.Functions.CustomComboFunctions;

using BRD = global::ParseLord2.Combos.PvE.BRD;

namespace ParseLord2.AutoRotation.Planner.Bard;

/// <summary>
/// Score-based oGCD selection for BRD (Gold Planner).
/// Explainable: buffs first, then high-frequency oGCDs, then songs (to avoid drift).
/// </summary>
internal sealed class BrdOgcdPlanner
{
    private static long _lastRadiantAttemptMs;

    private static bool RecentlyAttemptedRadiant(long nowMs)
        => nowMs - _lastRadiantAttemptMs < 900; // avoid repeated failed attempts starving other oGCDs

    private readonly List<OgcdCandidate> Candidates = new();

    private static int GetCodaCount()
    {
        // Dalamud API: BRDGauge.Coda is Song[] of active Codas.
        // Radiant Finale is only usable with >= 1 Coda.
        var g = BRD.gauge ?? GetJobGauge<BRDGauge>();
        if (g is null) return 0;

        var codas = g.Coda;
        if (codas is null || codas.Length == 0) return 0;

        var count = 0;
        for (var i = 0; i < codas.Length; i++)
            if (codas[i] != Song.None) count++;

        return count;
    }

    public uint TrySelectOgcd(PlannerContext ctx, out List<OgcdCandidateTrace> trace)
    {
        Candidates.Clear();

        if (!ctx.IsInLegalWeaveWindow)
        {
            trace = new List<OgcdCandidateTrace>();
            return 0;
        }

        // --- Buffs / 2-min core ---
        Candidates.Add(new OgcdCandidate
        {
            ActionId = BRD.RadiantFinale,
            Label = nameof(BRD.RadiantFinale),
            // Radiant Finale requires at least one Coda (earned from songs).
            // ActionReady() may still return true when you have 0 Coda, causing the planner to spam-cast it and starve other oGCDs.
            HardOk = () =>
                InCombat() &&
                ActionReady(BRD.RadiantFinale) &&
                GetCodaCount() >= 1 &&
                !HasStatusEffect(BRD.Buffs.RadiantFinale) &&
                !RecentlyAttemptedRadiant(Environment.TickCount64),
            BlockReason = () =>
            {
                if (!InCombat()) return "Not in combat";
                if (!ActionReady(BRD.RadiantFinale)) return "Radiant Finale not ready";
                if (GetCodaCount() <= 0) return "No Coda (Radiant Finale unusable)";
                if (HasStatusEffect(BRD.Buffs.RadiantFinale)) return "Radiant Finale buff already active";
                return "Blocked";
            },
            Score = () =>
            {
                var coda = GetCodaCount();
                // Prefer Radiant Finale inside burst or with full (3) coda, but don't let it starve higher-frequency oGCDs.
                var inBurst = ctx.BurstPhase == BurstPhase.InBurst || ctx.BurstPhase == BurstPhase.PreBurst;
                if (coda >= 3) return inBurst ? 92f : 82f;
                return inBurst ? 78f : 62f;
            },
        });

        Candidates.Add(new OgcdCandidate
        {
            ActionId = BRD.BattleVoice,
            Label = nameof(BRD.BattleVoice),
            HardOk = () => ActionReady(BRD.BattleVoice) && InCombat(),
            BlockReason = () => !InCombat() ? "Not in combat" : "Battle Voice not ready",
            Score = () => 95f,
        });

        Candidates.Add(new OgcdCandidate
        {
            ActionId = BRD.RagingStrikes,
            Label = nameof(BRD.RagingStrikes),
            HardOk = () => ActionReady(BRD.RagingStrikes) && InCombat(),
            BlockReason = () => !InCombat() ? "Not in combat" : "Raging Strikes not ready",
            Score = () => 92f,
        });

        Candidates.Add(new OgcdCandidate
        {
            ActionId = BRD.Barrage,
            Label = nameof(BRD.Barrage),
            HardOk = () => ActionReady(BRD.Barrage) && InCombat(),
            BlockReason = () => !InCombat() ? "Not in combat" : "Barrage not ready",
            Score = () =>
            {
                var inBuffs =
                    HasStatusEffect(BRD.Buffs.RagingStrikes) ||
                    HasStatusEffect(BRD.Buffs.BattleVoice) ||
                    HasStatusEffect(BRD.Buffs.RadiantFinale);

                var hasProc = HasStatusEffect(BRD.Buffs.HawksEye);

                if (hasProc) return inBuffs ? 90f : 80f;
                return inBuffs ? 70f : 55f;
            },
        });

        // --- High-frequency oGCDs ---
        Candidates.Add(new OgcdCandidate
        {
            ActionId = BRD.EmpyrealArrow,
            Label = nameof(BRD.EmpyrealArrow),
            HardOk = () => ActionReady(BRD.EmpyrealArrow),
            BlockReason = () => "Empyreal Arrow not ready",
            Score = () => 65f,
        });

        var bloodOrRain = LevelChecked(BRD.RainOfDeath) ? BRD.RainOfDeath : BRD.Bloodletter;
        Candidates.Add(new OgcdCandidate
        {
            ActionId = bloodOrRain,
            Label = bloodOrRain == BRD.RainOfDeath ? nameof(BRD.RainOfDeath) : nameof(BRD.Bloodletter),
            HardOk = () => ActionReady(bloodOrRain),
            BlockReason = () => "Blood/Rain not ready",
            Score = () =>
            {
                var charges = bloodOrRain == BRD.RainOfDeath ? BRD.RainOfDeathCharges : BRD.BloodletterCharges;
                return charges >= 2 ? 75f : 58f;
            },
        });

        Candidates.Add(new OgcdCandidate
        {
            ActionId = BRD.Sidewinder,
            Label = nameof(BRD.Sidewinder),
            HardOk = () => ActionReady(BRD.Sidewinder),
            BlockReason = () => "Sidewinder not ready",
            Score = () => 55f,
        });

        // --- Songs (avoid drift) ---
        Candidates.Add(new OgcdCandidate
        {
            ActionId = BRD.WanderersMinuet,
            Label = nameof(BRD.WanderersMinuet),
            HardOk = () => ActionReady(BRD.WanderersMinuet) && InCombat() && (BRD.SongNone || (BRD.SongArmy && BRD.SongTimerInSeconds <= 1)),
            BlockReason = () => "Song not ready / not time",
            Score = () => 52f,
        });

        Candidates.Add(new OgcdCandidate
        {
            ActionId = BRD.MagesBallad,
            Label = nameof(BRD.MagesBallad),
            HardOk = () => ActionReady(BRD.MagesBallad) && InCombat() && (BRD.SongWanderer && BRD.SongTimerInSeconds <= 1),
            BlockReason = () => "Song not ready / not time",
            Score = () => 51f,
        });

        Candidates.Add(new OgcdCandidate
        {
            ActionId = BRD.ArmysPaeon,
            Label = nameof(BRD.ArmysPaeon),
            HardOk = () => ActionReady(BRD.ArmysPaeon) && InCombat() && (BRD.SongMage && BRD.SongTimerInSeconds <= 1),
            BlockReason = () => "Song not ready / not time",
            Score = () => 50f,
        });

        var selected = ScoreOgcdPlanner.SelectBest(Candidates, out trace);
        if (selected == BRD.RadiantFinale)
        {
            _lastRadiantAttemptMs = Environment.TickCount64;
        }

        return selected;
    }
}
