using System.Collections.Generic;
using Dalamud.Game.ClientState.JobGauge.Types;
using ParseLord2.AutoRotation.Planner;
using ParseLord2.AutoRotation.Planner.Ogcd;
using ParseLord2.AutoRotation.Planner.Trace;
using static ParseLord2.CustomComboNS.Functions.CustomComboFunctions;

using DRG = global::ParseLord2.Combos.PvE.DRG;

namespace ParseLord2.AutoRotation.Planner.Dragoon;

/// <summary>
/// Score-based oGCD selection for DRG (Gold Planner).
/// Phase 2+ : explicit candidate list for DRG first; later fed by Priority Stacks.
/// </summary>
internal sealed class DrgOgcdPlanner
{
    private static readonly List<OgcdCandidate> Candidates = new(24);

    public uint TrySelectOgcd(PlannerContext ctx, out List<OgcdCandidateTrace> trace)
    {
        Candidates.Clear();

        // Two-pass model: only consider oGCDs inside a legal weave window.
        if (!ctx.IsInLegalWeaveWindow)
        {
            trace = new List<OgcdCandidateTrace>();
            return 0;
        }

        static bool Ready(uint id)
        {
            // Target-less buffs (Litany/Lance/Life Surge) should not be range-gated.
            if (id is DRG.BattleLitany or DRG.LanceCharge or DRG.LifeSurge)
                return ActionReady(id);

            return ActionReady(id) && InActionRange(id);
        }

        // Read DRG gauge directly (we do NOT depend on private DRG helper members).
        var gauge = GetJobGauge<DRGGauge>();
        bool lotd = gauge.IsLOTDActive;
        byte focus = gauge.FirstmindsFocusCount;

        // -----------------------------
        // LotD-limited actions (high priority while active)
        // -----------------------------

        // Nastrond: replaces Geirskogul while in Life of the Dragon.
        // We gate it on the Nastrond Ready buff (granted when entering LotD).
        // Phase 4B: This is URGENT - LotD window is time-limited (~20s).
        Candidates.Add(new OgcdCandidate
        {
            ActionId = DRG.Nastrond,
            Label = nameof(DRG.Nastrond),
            HardOk = () =>
                lotd &&
                LevelChecked(DRG.Nastrond) &&
                Ready(DRG.Nastrond) &&
                HasStatusEffect(DRG.Buffs.NastrondReady),
            BlockReason = () =>
            {
                if (!lotd) return "Not in Life of the Dragon";
                if (!HasStatusEffect(DRG.Buffs.NastrondReady)) return "Nastrond Ready buff not active";
                return "Not ready";
            },
            // High priority while available; slight anti-drift pressure from LotD being time-limited.
            Score = () => 90f,
        });

        // Stardiver: only executable during Life of the Dragon.
        // Phase 4B: This is URGENT - LotD window is time-limited.
        Candidates.Add(new OgcdCandidate
        {
            ActionId = DRG.Stardiver,
            Label = nameof(DRG.Stardiver),
            HardOk = () =>
                lotd &&
                LevelChecked(DRG.Stardiver) &&
                Ready(DRG.Stardiver) &&
                !HasWeavedAction(DRG.Stardiver),
            BlockReason = () =>
            {
                if (!lotd) return "Not in Life of the Dragon";
                if (HasWeavedAction(DRG.Stardiver)) return "Already used this weave window";
                return "Not ready";
            },
            Score = () => 88f - ctx.CooldownRemaining(DRG.Stardiver),
        });

        // Starcross: proc follow-up to Stardiver (Starcross Ready).
        // Phase 4B: This is URGENT - proc buff has limited duration.
        Candidates.Add(new OgcdCandidate
        {
            ActionId = DRG.Starcross,
            Label = nameof(DRG.Starcross),
            HardOk = () =>
                LevelChecked(DRG.Starcross) &&
                Ready(DRG.Starcross) &&
                HasStatusEffect(DRG.Buffs.StarcrossReady),
            BlockReason = () =>
                !HasStatusEffect(DRG.Buffs.StarcrossReady) ? "Starcross Ready buff not active" : "Not ready",
            Score = () => 95f,
        });

        // Rise of the Dragon: proc follow-up in the LotD package (Dragon's Flight).
        // Phase 4B: This is URGENT - proc buff has limited duration.
        Candidates.Add(new OgcdCandidate
        {
            ActionId = DRG.RiseOfTheDragon,
            Label = nameof(DRG.RiseOfTheDragon),
            HardOk = () =>
                LevelChecked(DRG.RiseOfTheDragon) &&
                Ready(DRG.RiseOfTheDragon) &&
                HasStatusEffect(DRG.Buffs.DragonsFlight),
            BlockReason = () =>
                !HasStatusEffect(DRG.Buffs.DragonsFlight) ? "Dragon's Flight buff not active" : "Not ready",
            Score = () => 92f,
        });

        // -----------------------------
        // Core oGCD kit
        // -----------------------------

        // Wyrmwind Thrust (when Firstminds is capped)
        // Phase 4B: This is URGENT when at 2 stacks (overcap prevention).
        // Always allowed when at cap, even during PreBurst.
        Candidates.Add(new OgcdCandidate
        {
            ActionId = DRG.WyrmwindThrust,
            Label = nameof(DRG.WyrmwindThrust),
            HardOk = () =>
                LevelChecked(DRG.WyrmwindThrust) &&
                Ready(DRG.WyrmwindThrust) &&
                focus == 2,
            BlockReason = () =>
                focus < 2 ? "Firstminds not capped" : "Not ready",
            Score = () => 70f - ctx.CooldownRemaining(DRG.WyrmwindThrust),
        });

        // Geirskogul (only when not in Life of the Dragon)
        // Phase 4B: Hold during PreBurst to preserve weave slots.
        // Phase 4C: Allow during PostBurst (wind-down after burst ends).
        // Not urgent: 30s cooldown, no overcap risk.
        Candidates.Add(new OgcdCandidate
        {
            ActionId = DRG.Geirskogul,
            Label = nameof(DRG.Geirskogul),
            HardOk = () =>
                !lotd &&
                LevelChecked(DRG.Geirskogul) &&
                Ready(DRG.Geirskogul) &&
                ctx.BurstPhase != BurstPhase.PreBurst,
            BlockReason = () =>
            {
                if (lotd) return "In Life of the Dragon";
                if (ctx.BurstPhase == BurstPhase.PreBurst)
                    return "Held for upcoming burst (no urgency)";
                return "Not ready";
            },
            Score = () => 80f - ctx.CooldownRemaining(DRG.Geirskogul),
        });

        // Lance Charge
        Candidates.Add(new OgcdCandidate
        {
            ActionId = DRG.LanceCharge,
            Label = nameof(DRG.LanceCharge),
            HardOk = () =>
                LevelChecked(DRG.LanceCharge) &&
                Ready(DRG.LanceCharge) &&
                // Hold Lance Charge briefly if Litany is about to come up, to align buffs.
                !(ctx.CooldownRemaining(DRG.BattleLitany) > 0.01f && ctx.CooldownRemaining(DRG.BattleLitany) <= 5.0f),
            BlockReason = () =>
            {
                if (ctx.CooldownRemaining(DRG.BattleLitany) > 0.01f && ctx.CooldownRemaining(DRG.BattleLitany) <= 5.0f)
                    return "Holding to align with Battle Litany";
                return "Not ready";
            },
            Score = () =>
                (60f - ctx.CooldownRemaining(DRG.LanceCharge)) +
                (ctx.BurstPhase is BurstPhase.PreBurst or BurstPhase.InBurst ? 40f : 10f) +
                (ctx.CooldownRemaining(DRG.BattleLitany) <= 1.0f ? 15f : 0f),
        });

        // Battle Litany
        Candidates.Add(new OgcdCandidate
        {
            ActionId = DRG.BattleLitany,
            Label = nameof(DRG.BattleLitany),
            HardOk = () =>
                LevelChecked(DRG.BattleLitany) &&
                Ready(DRG.BattleLitany) &&
                // Hold Battle Litany briefly if Lance Charge is about to come up, to align buffs.
                !(ctx.CooldownRemaining(DRG.LanceCharge) > 0.01f && ctx.CooldownRemaining(DRG.LanceCharge) <= 5.0f),
            BlockReason = () =>
            {
                if (ctx.CooldownRemaining(DRG.LanceCharge) > 0.01f && ctx.CooldownRemaining(DRG.LanceCharge) <= 5.0f)
                    return "Holding to align with Lance Charge";
                return "Not ready";
            },
            Score = () =>
                (58f - ctx.CooldownRemaining(DRG.BattleLitany)) +
                (ctx.BurstPhase is BurstPhase.PreBurst or BurstPhase.InBurst ? 42f : 12f) +
                (ctx.CooldownRemaining(DRG.LanceCharge) <= 1.0f ? 15f : 0f),
        });

        // Dragonfire Dive
        // Phase 4B: Hold during PreBurst to preserve weave slots for upcoming burst.
        // Phase 4C: Allow during PostBurst (wind-down after burst ends).
        // No urgency: cannot overcap, no proc/buff to maintain.
        Candidates.Add(new OgcdCandidate
        {
            ActionId = DRG.DragonfireDive,
            Label = nameof(DRG.DragonfireDive),
            HardOk = () =>
                LevelChecked(DRG.DragonfireDive) &&
                Ready(DRG.DragonfireDive) &&
                ctx.BurstPhase != BurstPhase.PreBurst,
            BlockReason = () =>
                ctx.BurstPhase == BurstPhase.PreBurst
                    ? "Held for upcoming burst (no urgency)"
                    : "Not ready",
            Score = () => 55f - ctx.CooldownRemaining(DRG.DragonfireDive),
        });

        // High Jump
        // Phase 4B: Hold during PreBurst UNLESS Mirage Dive buff is about to expire
        // (meaning we need to generate a new Dive Ready proc soon).
        // Dive Ready lasts 15 seconds, so if it's under ~3s we're at risk of losing it.
        Candidates.Add(new OgcdCandidate
        {
            ActionId = DRG.HighJump,
            Label = nameof(DRG.HighJump),
            HardOk = () =>
            {
                if (!LevelChecked(DRG.HighJump) || !Ready(DRG.HighJump))
                    return false;

                // Always allow during Neutral or InBurst.
                if (ctx.BurstPhase != BurstPhase.PreBurst)
                    return true;

                // During PreBurst, only allow if Dive Ready buff is about to expire
                // (urgent to refresh before we lose the proc).
                var diveReadyRemaining = ctx.PlayerStatusRemaining(DRG.Buffs.DiveReady);
                return diveReadyRemaining > 0.01f && diveReadyRemaining < 3.0f;
            },
            BlockReason = () =>
            {
                if (ctx.BurstPhase == BurstPhase.PreBurst)
                {
                    var diveReadyRemaining = ctx.PlayerStatusRemaining(DRG.Buffs.DiveReady);
                    if (diveReadyRemaining > 3.0f)
                        return "Held for upcoming burst (Dive Ready not urgent)";
                }
                return "Not ready";
            },
            Score = () => 52f - ctx.CooldownRemaining(DRG.HighJump),
        });

        // Mirage Dive (follow-up to Jump/High Jump via Dive Ready)
        // Phase 4B: This is URGENT - the Dive Ready buff has limited duration (~15s).
        // Always allowed when proc is available, even during PreBurst.
        Candidates.Add(new OgcdCandidate
        {
            ActionId = DRG.MirageDive,
            Label = nameof(DRG.MirageDive),
            HardOk = () =>
                LevelChecked(DRG.MirageDive) &&
                Ready(DRG.MirageDive) &&
                HasStatusEffect(DRG.Buffs.DiveReady),
            BlockReason = () =>
                !HasStatusEffect(DRG.Buffs.DiveReady) ? "Dive Ready buff not active" : "Not ready",
            Score = () => 75f,
        });

        // Life Surge (only when it will actually apply)
        // Phase 4B: This is conditionally URGENT - when next GCD is worthy AND we don't have the buff.
        // Not urgent during PreBurst if next GCD isn't worthy.
        Candidates.Add(new OgcdCandidate
        {
            ActionId = DRG.LifeSurge,
            Label = nameof(DRG.LifeSurge),
            HardOk = () =>
                LevelChecked(DRG.LifeSurge) &&
                Ready(DRG.LifeSurge) &&
                !HasStatusEffect(DRG.Buffs.LifeSurge) &&
                IsLifeSurgeWorthyNextGcd(ctx.PlannedNextGcdActionId),
            BlockReason = () =>
            {
                if (HasStatusEffect(DRG.Buffs.LifeSurge)) return "Life Surge buff already active";
                if (!IsLifeSurgeWorthyNextGcd(ctx.PlannedNextGcdActionId)) return "Next GCD not worthy of Life Surge";
                return "Not ready";
            },
            Score = () => 50f - ctx.CooldownRemaining(DRG.LifeSurge),
        });

        return ScoreOgcdPlanner.SelectBest(Candidates, out trace);
    }

    private static bool IsLifeSurgeWorthyNextGcd(uint plannedGcd)
    {
        // Conservative: only buff big hits.
        return plannedGcd is DRG.HeavensThrust or DRG.ChaoticSpring or DRG.Drakesbane or DRG.CoerthanTorment;
    }
}
