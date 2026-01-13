using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameFunctions;
namespace ParseLord2.Extensions;

internal static class TargetExtensions
{
    public unsafe static uint GetNameId(this IGameObject t)
    {
        return t.Struct()->GetNameId();
    }
}