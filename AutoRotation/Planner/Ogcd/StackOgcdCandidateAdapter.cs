using System;
using System.Collections.Generic;
using static global::ParseLord2.CustomComboNS.Functions.CustomComboFunctions;

namespace ParseLord2.AutoRotation.Planner.Ogcd;

/// <summary>
/// Converts a simple ordered ActionId stack into scored oGCD candidates.
/// This matches the current ParseLord2 stack model (stack = list of ActionIds).
/// Job-specific hard gates (e.g., LotD buffs) should be applied by the job oGCD planner,
/// not here.
/// </summary>
internal static class StackOgcdCandidateAdapter
{
    internal static List<OgcdCandidate> BuildFromActionIds(PlannerContext ctx, IReadOnlyList<uint> actionIds, Func<uint, Func<bool>?>? extraHardGate = null)
    {
        var list = new List<OgcdCandidate>(actionIds.Count);

        for (int i = 0; i < actionIds.Count; i++)
        {
            var id = actionIds[i];
            if (id == 0)
                continue;

            // Higher in list = higher base score.
            var baseScore = (actionIds.Count - i) * 10f;

            list.Add(new OgcdCandidate
            {
                ActionId = id,
                Label = $"Stack#{i}",
                HardOk = () =>
                {
                    if (!ctx.IsInLegalWeaveWindow || ctx.WeaveWindow.SlotsUsed >= ctx.WeaveWindow.SlotsMax)
                        return false;

                    if (!LevelChecked(id))
                        return false;

                    if (!ActionReady(id) || !InActionRange(id))
                        return false;

                    var extra = extraHardGate?.Invoke(id);
                    if (extra is not null && !extra())
                        return false;

                    return true;
                },
                Score = () =>
                {
                    // Very simple scoring for now (Phase 3C will tune this):
                    // - stack priority
                    // - slight burst preference
                    // - slight "use soon" preference based on cooldown remaining
                    var score = baseScore;

                    if (ctx.BurstPhase == BurstPhase.InBurst)
                        score += 50f;

                    var cd = ctx.CooldownRemaining(id);
                    if (cd <= 0.01f)
                        score += 25f;
                    else
                        score += Math.Max(0f, 10f - cd);

                    return score;
                }
            });
        }

        return list;
    }
}
