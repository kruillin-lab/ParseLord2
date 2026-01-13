using System.Collections.Generic;
using ParseLord2.AutoRotation.Planner.Trace;

namespace ParseLord2.AutoRotation.Planner.Ogcd;

internal static class ScoreOgcdPlanner
{
    /// <summary>
    /// Returns the highest-scoring oGCD candidate that passes hard gates.
    /// Returns 0 when no candidate should be used.
    /// </summary>
    public static uint SelectBest(IReadOnlyList<OgcdCandidate> candidates)
        => SelectBest(candidates, out _);

    /// <summary>
    /// Same as <see cref="SelectBest(IReadOnlyList{OgcdCandidate})"/> but also produces
    /// trace data for UI/debugging.
    /// </summary>
    public static uint SelectBest(IReadOnlyList<OgcdCandidate> candidates, out List<OgcdCandidateTrace> trace)
    {
        trace = new List<OgcdCandidateTrace>(candidates.Count);

        uint bestAction = 0;
        float bestScore = float.MinValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (c.ActionId == 0) continue;

            bool allowed;
            float score = float.MinValue;
            string reason = string.Empty;

            try
            {
                allowed = c.HardOk?.Invoke() ?? true;
            }
            catch
            {
                allowed = false;
                reason = "Hard gate exception";
            }

            if (!allowed && string.IsNullOrEmpty(reason))
            {
                try
                {
                    reason = c.BlockReason?.Invoke() ?? "Hard gate failed";
                }
                catch
                {
                    reason = "BlockReason exception";
                }
            }

            if (allowed)
            {
                try
                {
                    score = c.Score?.Invoke() ?? 0f;
                }
                catch
                {
                    allowed = false;
                    reason = "Score exception";
                }
            }

            trace.Add(new OgcdCandidateTrace
            {
                ActionId = c.ActionId,
                Label = c.Label ?? string.Empty,
                Allowed = allowed,
                BlockReason = reason,
                Score = score,
            });

            if (!allowed) continue;

            if (score > bestScore)
            {
                bestScore = score;
                bestAction = c.ActionId;
            }
        }

        return bestAction;
    }
}
