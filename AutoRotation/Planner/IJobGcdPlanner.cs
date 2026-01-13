namespace ParseLord2.AutoRotation.Planner;

internal interface IJobGcdPlanner
{
    /// <summary>
    /// Determine the next GCD action (ActionId) based on the provided context.
    /// </summary>
    uint NextGcd(PlannerContext ctx);
}
