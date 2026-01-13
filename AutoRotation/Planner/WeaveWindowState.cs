namespace ParseLord2.AutoRotation.Planner;

internal readonly record struct WeaveWindowState(int SlotsUsed, int SlotsMax)
{
    public bool HasCapacity => SlotsUsed < SlotsMax;
    public int SlotsRemaining => SlotsMax - SlotsUsed;

    public static WeaveWindowState None => new(0, 0);
}
