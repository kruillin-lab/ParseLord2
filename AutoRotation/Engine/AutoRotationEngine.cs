using System.Collections.Generic;

namespace ParseLord2.AutoRotation.Engine;

internal static class AutoRotationEngine
{
    // Tasks are stateless; build once.
    private static readonly List<IRotationTask> Tasks = new()
    {
        new EmergencyHealingTask(),
        new RezTask(),
        new RegularHealingTask(),
        new DpsTask(),
    };

    internal static void Tick(RotationContext ctx)
    {
        // If healing is needed but we're not allowed to heal yet, suppress everything (RSR-style).
        if (ctx.IsHealer && ctx.NeedsHeal && !ctx.CanHeal)
            return;

        foreach (var task in Tasks)
        {
            if (!task.IsApplicable(ctx))
                continue;

            if (task.TryExecute(ctx))
                return;
        }
    }

    private sealed class EmergencyHealingTask : IRotationTask
    {
        public int Priority => 10;

        public bool IsApplicable(RotationContext ctx) =>
            ctx.IsHealer && ctx.NeedsHeal && ctx.AnyEmergency && ctx.CanHeal;

        public bool TryExecute(RotationContext ctx) =>
            AutoRotationController.ProcessAutoActions(ctx.AutoActions, ref ctx.Temp, true, false);
    }

    private sealed class RezTask : IRotationTask
    {
        public int Priority => 20;

        public bool IsApplicable(RotationContext ctx) =>
            ctx.CanRez && ctx.Config.HealerSettings.AutoRez && !ctx.AnyEmergency;

        public bool TryExecute(RotationContext ctx) =>
            AutoRotationController.RezParty();
    }

    private sealed class RegularHealingTask : IRotationTask
    {
        public int Priority => 30;

        public bool IsApplicable(RotationContext ctx) =>
            ctx.IsHealer && ctx.NeedsHeal && ctx.CanHeal;

        public bool TryExecute(RotationContext ctx) =>
            AutoRotationController.ProcessAutoActions(ctx.AutoActions, ref ctx.Temp, true, false);
    }

    private sealed class DpsTask : IRotationTask
    {
        public int Priority => 40;

        public bool IsApplicable(RotationContext ctx) => true;

        public bool TryExecute(RotationContext ctx)
        {
            AutoRotationController.ProcessAutoActions(ctx.AutoActions, ref ctx.Temp, false, false);
            return true;
        }
    }
}
