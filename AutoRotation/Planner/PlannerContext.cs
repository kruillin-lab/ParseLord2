using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using ECommons.ExcelServices;
using static global::ParseLord2.CustomComboNS.Functions.CustomComboFunctions;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Game.ClientState.Statuses;
using static global::ParseLord2.Data.ActionWatching;
using global::ParseLord2.Services;

namespace ParseLord2.AutoRotation.Planner;

/// <summary>
/// Unified snapshot used by planners (GCD now; oGCD scorer later).
/// This is intentionally "ActionId-centric" and explainable.
/// </summary>
internal sealed class PlannerContext
{
    // --- Core / always-available ---
    public uint ComboAction { get; init; }
    public float ComboTimer { get; init; }

    /// <summary> Player buffs: StatusId -> seconds remaining. </summary>
    public IReadOnlyDictionary<ushort, float> PlayerBuffs { get; init; } = new Dictionary<ushort, float>();

    /// <summary> Target debuffs: StatusId -> seconds remaining. </summary>
    public IReadOnlyDictionary<ushort, float> TargetDebuffs { get; init; } = new Dictionary<ushort, float>();

    /// <summary> Cooldowns: ActionId -> seconds remaining (total). Populated lazily when needed. </summary>
    public Dictionary<uint, float> Cooldowns { get; init; } = new();

    public int TargetCountEstimate { get; init; } = 1;

    public BurstPhase BurstPhase { get; init; } = BurstPhase.Neutral;

    public WeaveWindowState WeaveWindow { get; init; } = new(0, 2);

    /// <summary>
    /// True if the engine is currently inside a legal weave window.
    /// This uses the same proven gate as existing combo logic.
    /// </summary>
    public bool IsInLegalWeaveWindow { get; init; }

    // Produced by the GCD planner (filled by the caller / engine)
    public uint PlannedNextGcdActionId { get; set; }

    // --- Convenience ---
    public bool HasPlayerStatus(ushort statusId) => PlayerBuffs.TryGetValue(statusId, out var t) && t > 0.01f;
    public float PlayerStatusRemaining(ushort statusId) => PlayerBuffs.TryGetValue(statusId, out var t) ? t : 0f;

    public bool HasTargetStatus(ushort statusId) => TargetDebuffs.TryGetValue(statusId, out var t) && t > 0.01f;
    public float TargetStatusRemaining(ushort statusId) => TargetDebuffs.TryGetValue(statusId, out var t) ? t : 0f;

    public float CooldownRemaining(uint actionId)
    {
        if (Cooldowns.TryGetValue(actionId, out var cached))
            return cached;

        var v = GetCooldownRemainingTime(actionId);
        Cooldowns[actionId] = v;
        return v;
    }


    private static unsafe float GetRemainingTime(IStatus s)
    {
        var rt = s.RemainingTime;
        if (rt < 0)
            return (-rt) + ActionManager.Instance()->AnimationLock;

        return rt;
    }

    public static PlannerContext Build(IGameObject? explicitTarget = null, int targetCountEstimate = 1)
    {
        var player = Player.Object;
        var target = explicitTarget ?? Player.Object?.TargetObject;

        var buffs = new Dictionary<ushort, float>(32);
        if (player is IBattleChara pc)
        {
            foreach (var s in pc.StatusList)
            {
                if (s is null || s.StatusId == 0) continue;
                buffs[(ushort)s.StatusId] = GetRemainingTime(s);
            }
        }

        var debuffs = new Dictionary<ushort, float>(32);
        if (target is IBattleChara tc)
        {
            foreach (var s in tc.StatusList)
            {
                if (s is null || s.StatusId == 0) continue;
                debuffs[(ushort)s.StatusId] = GetRemainingTime(s);
            }
        }

        // Weave tracking: reuse the existing authoritative weave bookkeeping.
        // (WeaveActions is cleared by the existing action watching logic each GCD window.)
        var slotsUsed = WeaveActions.Count;
        var slotsMax = Service.Configuration.MaximumWeavesPerWindow;

        // Phase 4A/4C: Conservative burst signaling (DRG-specific for now):
        // - InBurst if Battle Litany, Lance Charge, OR Life of the Dragon are active.
        // - PreBurst if one buff (Litany/Lance) is ready and the other will be ready soon (hold-to-align).
        // - PostBurst if recently exited a buff window (cooldown wind-down period).
        // - Neutral otherwise.
        //
        // This is intentionally simple, explainable, and safe to iterate.
        // When expanding to other jobs, this section will become job-specific.
        //
        // Status IDs (DRG):
        //   Battle Litany buff: 786 (duration: 15s, cooldown: 120s)
        //   Lance Charge buff : 1864 (duration: 20s, cooldown: 90s)
        // Action IDs (DRG):
        //   Battle Litany: 3557
        //   Lance Charge : 85
        var litanyBuffUp = buffs.TryGetValue(786, out var litRem) && litRem > 0.1f;
        var lanceBuffUp  = buffs.TryGetValue(1864, out var lcRem) && lcRem > 0.1f;

        var litCd = GetCooldownRemainingTime(3557);
        var lcCd  = GetCooldownRemainingTime(85);

        var litReady = litCd <= 0.01f;
        var lcReady  = lcCd <= 0.01f;

        // Phase 4C: Life of the Dragon is a major DRG burst window.
        // Check gauge to see if LotD is currently active.
        bool lotdActive = false;
        if (Player.Job == Job.DRG)
        {
            var drgGauge = GetJobGauge<Dalamud.Game.ClientState.JobGauge.Types.DRGGauge>();
            lotdActive = drgGauge.IsLOTDActive;
        }

        // Hold window for aligning Litany + Lance Charge when one is ready and the other is about to be.
        const float alignWindowSeconds = 5.0f;

        // Phase 4C: PostBurst detection - recently exited a buff window.
        // Litany: 120s CD, 15s duration → CD is 105s when buff ends → PostBurst window: 95-105s CD
        // Lance:  90s CD, 20s duration → CD is 70s when buff ends → PostBurst window: 60-70s CD
        const float postBurstWindowSeconds = 10.0f;
        var litanyJustEnded = !litanyBuffUp && litCd <= 105f && litCd > 95f;
        var lanceJustEnded = !lanceBuffUp && lcCd <= 70f && lcCd > 60f;

        var burstPhase =
            (litanyBuffUp || lanceBuffUp || lotdActive) ? BurstPhase.InBurst :
            ((litReady && lcCd > 0.01f && lcCd <= alignWindowSeconds) ||
             (lcReady && litCd > 0.01f && litCd <= alignWindowSeconds))
                ? BurstPhase.PreBurst
            : (litanyJustEnded || lanceJustEnded)
                ? BurstPhase.PostBurst
                : BurstPhase.Neutral;


        return new PlannerContext
        {
            ComboAction = global::ParseLord2.CustomComboNS.Functions.CustomComboFunctions.ComboAction,
            ComboTimer = global::ParseLord2.CustomComboNS.Functions.CustomComboFunctions.ComboTimer,
            PlayerBuffs = buffs,
            TargetDebuffs = debuffs,
            TargetCountEstimate = targetCountEstimate,
            BurstPhase = burstPhase,
            WeaveWindow = new WeaveWindowState(slotsUsed, slotsMax),
            IsInLegalWeaveWindow = CanWeave(maxWeaves: slotsMax) || CanDelayedWeave(maxWeaves: slotsMax),
        };
    }
}