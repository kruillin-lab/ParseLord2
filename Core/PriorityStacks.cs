using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using ParseLord2.CustomComboNS.Functions;

namespace ParseLord2.Core;

/// <summary>
/// ReactionEX-style "priority stack" concept, implemented internally for ParseLord2:
/// - A stack is an ordered list of ActionIds.
/// - If an ActionId has a stack assigned, ParseLord2 will try the actions in order and pick the first usable one.
/// 
/// Non-goals:
/// - No targeting automation
/// - No input interception / forced overrides
/// - No macro-queue behavior
/// </summary>
[Serializable]
public sealed class PriorityStackConfig
{
    [JsonProperty]
    public string Name = "New Stack";

    [JsonProperty]
    public bool Enabled = true;

    /// <summary>
    /// Ordered list of candidate ActionIds (top = highest priority).
    /// </summary>
    [JsonProperty]
    public List<uint> ActionIds = new();
}

internal static class PriorityStackResolver
{
    internal static uint Resolve(Configuration cfg, uint requestedActionId)
    {
        if (requestedActionId == 0)
            return 0;

        if (cfg.ActionPriorityStackMap is null || cfg.PriorityStacks is null)
            return requestedActionId;

        if (!cfg.ActionPriorityStackMap.TryGetValue(requestedActionId, out var stackName))
            return requestedActionId;

        if (string.IsNullOrWhiteSpace(stackName))
            return requestedActionId;

        var stack = cfg.PriorityStacks.FirstOrDefault(s =>
            s is not null &&
            s.Enabled &&
            string.Equals(s.Name, stackName, StringComparison.OrdinalIgnoreCase));

        if (stack?.ActionIds is null || stack.ActionIds.Count == 0)
            return requestedActionId;

        foreach (var candidate in stack.ActionIds)
        {
            if (candidate == 0)
                continue;

            if (CustomComboFunctions.ActionReady(candidate))
                return candidate;
        }

        return requestedActionId;
    }
}
