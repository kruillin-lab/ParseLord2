using System;
using System.Collections.Generic;
using ParseLord2.Combos;

namespace ParseLord2.AutoRotation.Engine;

internal sealed class RotationContext
{
    public required AutoRotationConfig Config { get; init; }
    public required Dictionary<Preset, bool> AutoActions { get; init; }

    public uint Temp;
    public bool IsHealer;
    public bool NeedsHeal;
    public bool CanHeal;
    public bool AnyEmergency;
    public bool CanRez;
}
