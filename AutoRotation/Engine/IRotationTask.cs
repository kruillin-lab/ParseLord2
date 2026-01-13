namespace ParseLord2.AutoRotation.Engine;

internal interface IRotationTask
{
    /// <summary> Lower value = higher priority. </summary>
    int Priority { get; }

    bool IsApplicable(RotationContext ctx);

    /// <summary> Returns true if the task executed (or intentionally consumed the tick). </summary>
    bool TryExecute(RotationContext ctx);
}
