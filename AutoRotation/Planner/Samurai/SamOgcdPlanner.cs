using System.Collections.Generic;
using Dalamud.Game.ClientState.JobGauge.Types;
using ParseLord2.AutoRotation.Planner;
using ParseLord2.AutoRotation.Planner.Ogcd;
using ParseLord2.AutoRotation.Planner.Trace;
using static ParseLord2.CustomComboNS.Functions.CustomComboFunctions;

using SAM = global::ParseLord2.Combos.PvE.SAM;

namespace ParseLord2.AutoRotation.Planner.Samurai;

/// <summary>
/// Score-based oGCD selection for SAM (Gold Planner).
/// Manages Kenki spenders, Meikyo Shisui, Ikishoten, and burst abilities.
/// </summary>
internal sealed class SamOgcdPlanner
{
    private static readonly List<OgcdCandidate> Candidates = new(16);

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
            // Most SAM oGCDs are targetless or don't need range check.
            // Senei and Guren need target range.
            if (id is SAM.Senei or SAM.Guren)
                return ActionReady(id) && InActionRange(id);

            return ActionReady(id);
        }

        // Read SAM gauge for Kenki and Meditation stacks.
        var gauge = GetJobGauge<SAMGauge>();
        byte kenki = gauge.Kenki;
        byte meditationStacks = gauge.MeditationStacks;

        const byte KenkiSpendThreshold = 65;
        const byte KenkiCapDumpThreshold = 80;
        bool kenkiMetaUrgent = kenki >= KenkiSpendThreshold;
        bool kenkiAtRiskOfCap = kenki >= KenkiCapDumpThreshold;


        // -----------------------------
        // Burst Abilities
        // -----------------------------

        // Ikishoten: Grants 50 Kenki and Ogi Namikiri Ready buff.
        // Use during burst or when Kenki is low.
        Candidates.Add(new OgcdCandidate
        {
            ActionId = SAM.Ikishoten,
            Label = nameof(SAM.Ikishoten),
            HardOk = () =>
                LevelChecked(SAM.Ikishoten) &&
                Ready(SAM.Ikishoten),
            BlockReason = () => "Not ready",
            Score = () =>
            {
                float score = 60f - ctx.CooldownRemaining(SAM.Ikishoten);
                // Boost during burst.
                if (ctx.BurstPhase is BurstPhase.InBurst)
                    score += 30f;
                // Boost if Kenki is low (prevents overcap).
                if (kenki < 50)
                    score += 20f;
                return score;
            },
        });

        // Senei: High-potency Kenki spender (25 Kenki, 120s CD).
        // Strong burst ability.
        Candidates.Add(new OgcdCandidate
        {
            ActionId = SAM.Senei,
            Label = nameof(SAM.Senei),
            HardOk = () =>
                LevelChecked(SAM.Senei) &&
                Ready(SAM.Senei) &&
                kenki >= KenkiSpendThreshold,
            BlockReason = () =>
                kenki < 25 ? "Not enough Kenki (need 65)" : "Not ready",
            Score = () =>
            {
                float score = 70f - ctx.CooldownRemaining(SAM.Senei);
                if (ctx.BurstPhase is BurstPhase.PreBurst or BurstPhase.InBurst)
                    score += 40f;
                return score;
            },
        });

        // Guren: AoE Kenki spender (25 Kenki, 120s CD) - lower level than Senei.
        // Use if Senei not available.
        Candidates.Add(new OgcdCandidate
        {
            ActionId = SAM.Guren,
            Label = nameof(SAM.Guren),
            HardOk = () =>
                !LevelChecked(SAM.Senei) &&
                LevelChecked(SAM.Guren) &&
                Ready(SAM.Guren) &&
                kenki >= KenkiSpendThreshold,
            BlockReason = () =>
                LevelChecked(SAM.Senei) ? "Senei learned (use that instead)" :
                kenki < 25 ? "Not enough Kenki (need 65)" : "Not ready",
            Score = () => 70f - ctx.CooldownRemaining(SAM.Guren),
        });

        // -----------------------------
        // Utility Abilities
        // -----------------------------

        // Shoha: Meditation stack spender (max 3 stacks).
        // High priority when at cap to prevent overcap.
        Candidates.Add(new OgcdCandidate
        {
            ActionId = SAM.Shoha,
            Label = nameof(SAM.Shoha),
            HardOk = () =>
                LevelChecked(SAM.Shoha) &&
                Ready(SAM.Shoha) &&
                meditationStacks == 3,
            BlockReason = () =>
                meditationStacks < 3 ? "Meditation not capped" : "Not ready",
            Score = () => 65f, // High priority to prevent overcap.
        });

        // Meikyo Shisui: Grants 3 free Sen (no combo required).
        // Don't use during PreBurst (save weave slots), unless preventing overcap.
        Candidates.Add(new OgcdCandidate
        {
            ActionId = SAM.MeikyoShisui,
            Label = nameof(SAM.MeikyoShisui),
            HardOk = () =>
                LevelChecked(SAM.MeikyoShisui) &&
                Ready(SAM.MeikyoShisui) &&
                ctx.BurstPhase != BurstPhase.PreBurst,
            BlockReason = () =>
                ctx.BurstPhase == BurstPhase.PreBurst
                    ? "Held for upcoming burst (no urgency)"
                    : "Not ready",
            Score = () => 55f - ctx.CooldownRemaining(SAM.MeikyoShisui),
        });

        // -----------------------------
        // Kenki Spenders (Filler)
        // -----------------------------

        
        // Kyuten: AoE Kenki spender (25 Kenki). Prefer over Shinten in AoE contexts.
        Candidates.Add(new OgcdCandidate
        {
            ActionId = SAM.Kyuten,
            Label = nameof(SAM.Kyuten),
            HardOk = () =>
                ctx.TargetCountEstimate >= 3 &&
                LevelChecked(SAM.Kyuten) &&
                Ready(SAM.Kyuten) &&
                kenki >= 25,
            BlockReason = () =>
                ctx.TargetCountEstimate < 3 ? "ST context (prefer Shinten)" :
                kenki < 25 ? "Not enough Kenki (need 25)" : "Not ready",
            Score = () =>
            {
                float score = 36f + (kenki - 25f) * 0.5f;
                if (ctx.BurstPhase is BurstPhase.InBurst)
                    score += 10f;
                if (kenkiAtRiskOfCap)
                    score += 20f;
                return score;
            },
        });

// Shinten: Basic Kenki spender (25 Kenki).
        // Use to prevent Kenki overcap, but hold during PreBurst unless urgent.
        Candidates.Add(new OgcdCandidate
        {
            ActionId = SAM.Shinten,
            Label = nameof(SAM.Shinten),
            HardOk = () =>
            {
                // Prefer Kyuten in AoE contexts.
                if (ctx.TargetCountEstimate >= 3 && LevelChecked(SAM.Kyuten) && Ready(SAM.Kyuten) && kenki >= 25)
                    return false;

                if (!LevelChecked(SAM.Shinten) || !Ready(SAM.Shinten) || kenki < 25)
                    return false;

                // Always allow during Neutral, InBurst, PostBurst.
                if (ctx.BurstPhase != BurstPhase.PreBurst)
                    return true;

                // During PreBurst, only allow if Kenki is high (prevent overcap).
                return kenkiMetaUrgent || kenkiAtRiskOfCap;
            },
            BlockReason = () =>
            {
                if (ctx.TargetCountEstimate >= 3 && LevelChecked(SAM.Kyuten) && Ready(SAM.Kyuten) && kenki >= 25)
                    return "Prefer Kyuten in AoE";
                if (kenki < 25)
                    return "Not enough Kenki (need 25)";
                if (ctx.BurstPhase == BurstPhase.PreBurst && !kenkiMetaUrgent)
                    return "Held for upcoming burst (Kenki below meta threshold)";
                return "Not ready";
            },
            Score = () =>
            {
                // Base score + urgency from Kenki level.
                float score = 30f + (kenki - 25f) * 0.5f;
                // Boost if we're at risk of overcapping.
                if (kenkiAtRiskOfCap)
                    score += 25f;
                return score;
            },
        });

        return ScoreOgcdPlanner.SelectBest(Candidates, out trace);
    }
}
