#region

using Dalamud.Game.ClientState.Objects.Types;
using ParseLord2.Core;
using ParseLord2.CustomComboNS;
using ParseLord2.Services;
using EZ = ECommons.Throttlers.EzThrottler;
using TS = System.TimeSpan;

// ReSharper disable CheckNamespace

#endregion
namespace ParseLord2.Combos.PvE;

/// <summary>
///     Should be the same as <see cref="UIntExtensions" />, but with checking
///     the <see cref="Configuration.RetargetHealingActionsToStack" /> setting,
///     and automatically setting the target to the
///     <see cref="SimpleTarget.Stack.AllyToHeal">Heal Stack</see>.
/// </summary>
public static class HealRetargeting
{
    /// Just a buffer for checking the
    /// <see cref="Configuration.RetargetHealingActionsToStack" />
    /// setting.
    internal static bool RetargetSettingOn
    {
        get
        {
            if (!EZ.Throttle("healRetargetingConfig", TS.FromSeconds(0.9)))
                return field;

            field = Service.Configuration.RetargetHealingActionsToStack;
            return field;
        }
    }

    /// Just a shorter reference to
    /// <see cref="SimpleTarget.Stack.AllyToEsuna" />
    /// .
    private static IGameObject? EsunaStack => SimpleTarget.Stack.AllyToEsuna;

    /// Just a shorter reference to
    /// <see cref="SimpleTarget.Stack.AllyToHeal" />
    /// .
    private static IGameObject? HealStack => SimpleTarget.Stack.AllyToHeal;

    /// <summary>
    ///     Retargets the action if the
    ///     <see cref="Configuration.RetargetHealingActionsToStack">
    ///         option to do so
    ///     </see>
    ///     is enabled.
    /// </summary>
    /// <seealso cref="UIntExtensions.Retarget(uint,IGameObject?,bool)" />
    public static uint RetargetIfEnabled
    (this uint actionID,
        IGameObject? optionalTarget)
    {
        if (!RetargetSettingOn) return actionID;
        if (optionalTarget is null)
            return actionID.Retarget(actionID == RoleActions.Healer.Esuna ? EsunaStack : HealStack);
        else
            return actionID.Retarget(optionalTarget);
    }

    /// <summary>
    ///     Retargets the action if the
    ///     <see cref="Configuration.RetargetHealingActionsToStack">
    ///         option to do so
    ///     </see>
    ///     is enabled.
    /// </summary>
    /// <seealso cref="UIntExtensions.Retarget(uint,uint,IGameObject?,bool)" />
    public static uint RetargetIfEnabled
    (this uint actionID,
        IGameObject? optionalTarget,
        uint replaced)
    {
        if (!RetargetSettingOn) return actionID;
        if (optionalTarget is null)
            return actionID.Retarget(replaced, actionID == RoleActions.Healer.Esuna ? EsunaStack : HealStack);
        else
            return actionID.Retarget(replaced, optionalTarget);
    }

    /// <summary>
    ///     Retargets the action if the
    ///     <see cref="Configuration.RetargetHealingActionsToStack">
    ///         option to do so
    ///     </see>
    ///     is enabled.
    /// </summary>
    /// <seealso cref="UIntExtensions.Retarget(uint,uint[],IGameObject?,bool)" />
    public static uint RetargetIfEnabled
    (this uint actionID,
        IGameObject? optionalTarget,
        uint[] replaced)
    {
        if (!RetargetSettingOn) return actionID;
        if (optionalTarget is null)
            return actionID.Retarget(replaced, actionID == RoleActions.Healer.Esuna ? EsunaStack : HealStack);
        else
            return actionID.Retarget(replaced, optionalTarget);
    }
}
