

#region Dependencies
using Dalamud.Game.ClientState.JobGauge.Enums;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using ParseLord2.Core;
using ParseLord2.CustomComboNS;
using ParseLord2.CustomComboNS.Functions;
using ParseLord2.Extensions;
using static ParseLord2.Combos.PvE.BRD.Config;
using static ParseLord2.CustomComboNS.Functions.CustomComboFunctions;
#endregion

namespace ParseLord2.Combos.PvE;
internal partial class BRD
{
    #region Variables
    internal static readonly FrozenDictionary<uint, ushort> PurpleList = new Dictionary<uint, ushort>
    {
        { VenomousBite, Debuffs.VenomousBite },
        { CausticBite, Debuffs.CausticBite }
    }.ToFrozenDictionary();

    internal static readonly FrozenDictionary<uint, ushort> BlueList = new Dictionary<uint, ushort>
    {
        { Windbite, Debuffs.Windbite },
        { Stormbite, Debuffs.Stormbite }
    }.ToFrozenDictionary();

    // Gauge Stuff
    internal static BRDGauge? gauge = GetJobGauge<BRDGauge>();
    internal static int SongTimerInSeconds => gauge.SongTimer / 1000;
    internal static float SongTimerSeconds => gauge.SongTimer / 1000f;
    internal static bool SongNone => gauge.Song == Song.None;
    internal static bool SongWanderer => gauge.Song == Song.Wanderer;
    internal static bool SongMage => gauge.Song == Song.Mage;
    internal static bool SongArmy => gauge.Song == Song.Army;

    // Advanced BRD minimal trace (optional; gated by BRD_Adv_Debug)
    private static readonly Queue<string> _advTrace = new();
    private static long _advTraceLastMs;
    private static string _advTraceLastMsg = string.Empty;
    private static int _advTraceSuppressed;

    internal static IReadOnlyList<string> AdvTraceSnapshot() => _advTrace.ToArray();

    internal static void AdvTrace(string message)
    {
        if (!BRD_Adv_Debug) return;

        var now = Environment.TickCount64;
        if (message == _advTraceLastMsg && (now - _advTraceLastMs) < 750)
        {
            _advTraceSuppressed++;
            return;
        }

        if (_advTraceSuppressed > 0)
        {
            var summary = $"[BRD.Adv] (suppressed {_advTraceSuppressed} duplicate trace lines)";
            if (_advTrace.Count >= 50) _advTrace.Dequeue();
            _advTrace.Enqueue(summary);
            try { PluginLog.Information(summary); } catch { /* ignore */ }
            _advTraceSuppressed = 0;
        }

        _advTraceLastMs = now;
        _advTraceLastMsg = message;

        var line = $"[BRD.Adv] {message}";
        if (_advTrace.Count >= 50) _advTrace.Dequeue();
        _advTrace.Enqueue(line);
        try { PluginLog.Information(line); } catch { /* ignore */ }
    }

    //Dot Management
    internal static IStatus? Purple => GetStatusEffect(Debuffs.CausticBite, CurrentTarget) ?? GetStatusEffect(Debuffs.VenomousBite, CurrentTarget);
    internal static IStatus? Blue => GetStatusEffect(Debuffs.Stormbite, CurrentTarget) ?? GetStatusEffect(Debuffs.Windbite, CurrentTarget);
    internal static float PurpleRemaining => Purple?.RemainingTime ?? 0;
    internal static float BlueRemaining => Blue?.RemainingTime ?? 0;
    internal static bool DebuffCapCanPurple => CanApplyStatus(CurrentTarget, Debuffs.CausticBite) || CanApplyStatus(CurrentTarget, Debuffs.VenomousBite);
    internal static bool DebuffCapCanBlue => CanApplyStatus(CurrentTarget, Debuffs.Stormbite) || CanApplyStatus(CurrentTarget, Debuffs.Windbite);

    //Useful Bools
    internal static bool BardHasTarget => HasBattleTarget();
    internal static bool CanBardWeave => CanWeave() || CanDelayedWeave();
    internal static bool CanWeaveDelayed => CanDelayedWeave();
    internal static bool CanIronJaws => LevelChecked(IronJaws);
    internal static bool BuffTime => GetCooldownRemainingTime(RagingStrikes) < 2.7;
    internal static bool BuffWindow => HasStatusEffect(Buffs.RagingStrikes) &&
                                       (HasStatusEffect(Buffs.BattleVoice) || !LevelChecked(BattleVoice)) &&
                                       (HasStatusEffect(Buffs.RadiantFinale) || !LevelChecked(RadiantFinale));

    //Buff Tracking
    internal static float RagingCD => GetCooldownRemainingTime(RagingStrikes);
    internal static float BattleVoiceCD => GetCooldownRemainingTime(BattleVoice);
    internal static float EmpyrealCD => GetCooldownRemainingTime(EmpyrealArrow);
    internal static float RadiantCD => GetCooldownRemainingTime(RadiantFinale);
    internal static float RagingStrikesDuration => GetStatusEffectRemainingTime(Buffs.RagingStrikes);
    internal static float RadiantFinaleDuration => GetStatusEffectRemainingTime(Buffs.RadiantFinale);

    // Charge Tracking
    internal static uint RainOfDeathCharges => LevelChecked(RainOfDeath) ? GetRemainingCharges(RainOfDeath) : 0;
    internal static uint BloodletterCharges => GetRemainingCharges(Bloodletter);

    #endregion

    #region Functions

    #region Action Status Helpers
    internal static unsafe bool StrictActionReady(uint actionId)
    {
        uint hookedId = OriginalHook(actionId);
        return ActionManager.Instance()->GetActionStatus(ActionType.Action, hookedId) == 0;
    }

    internal static bool CanUseRadiantFinaleNow()
    {
        if (!LevelChecked(RadiantFinale))
            return false;

        if (!InCombat())
            return false;

        if (!CanWeaveDelayed)
            return false;

        if (HasStatusEffect(Buffs.RadiantEncoreReady) || HasStatusEffect(Buffs.RadiantFinale))
            return false;

        return StrictActionReady(RadiantFinale);
    }
    #endregion

    #region Pooling
    internal static bool UsePooledApex()
    {
        if (gauge.SoulVoice >= 80)
        {
            if (BuffWindow && RagingStrikesDuration < 18 || RagingCD >= 50 && RagingCD <= 62)
            {
                AdvTrace($"UsePooledApex: true (SV={gauge.SoulVoice}, buffWindow={BuffWindow}, RagingDur={RagingStrikesDuration:0.1}s, RagingCD={RagingCD:0.1}s)");
                return true;
            }
        }
        return false;
    }

    internal static bool PitchPerfected()
    {
        if (!LevelChecked(PitchPerfect))
            return false;

        var rep = gauge.Repertoire;
        if (rep >= 3)
        {
            AdvTrace($"PitchPerfected: true (3 stacks)");
            return true;
        }

        if (gauge.Song != Song.Wanderer)
            return false;

        var songRem = SongTimerSeconds;
        var empyRem = LevelChecked(EmpyrealArrow) ? GetCooldown(EmpyrealArrow).CooldownRemaining : 999f;

        if (rep == 2)
        {
            if (BuffWindow)
            {
                AdvTrace($"PitchPerfected: true (2 stacks, buffWindow)");
                return true;
            }

            if (songRem <= 6.0f)
            {
                AdvTrace($"PitchPerfected: true (2 stacks, song ending soon={songRem:0.1}s)");
                return true;
            }

            if (empyRem <= 2.0f)
            {
                AdvTrace($"PitchPerfected: true (2 stacks, Empyreal soon={empyRem:0.1}s)");
                return true;
            }

            return false;
        }

        if (rep == 1)
        {
            if (songRem <= 2.5f)
            {
                AdvTrace($"PitchPerfected: true (1 stack, song ending={songRem:0.1}s)");
                return true;
            }
        }

        return false;
    }

    internal static bool UsePooledSidewinder()
    {
        if (BuffWindow && RagingStrikesDuration < 18 || RagingCD > 30)
        {
            AdvTrace($"UsePooledSidewinder: true (buffWindow={BuffWindow}, RagingDur={RagingStrikesDuration:0.1}s, RagingCD={RagingCD:0.1}s)");
            return true;
        }

        return false;
    }

    internal static bool UsePooledBloodRain()
    {
        if (!LevelChecked(Bloodletter))
            return false;

        var maxCharges = LevelChecked(RainOfDeath) ? 2u : 1u;
        var charges = BloodletterCharges;

        if (charges == 0)
            return false;

        if (charges >= maxCharges)
        {
            AdvTrace($"UsePooledBloodRain: true (at max charges={charges})");
            return true;
        }

        if (gauge.Song == Song.Mage)
        {
            AdvTrace($"UsePooledBloodRain: true (Mage's Ballad, charges={charges})");
            return true;
        }

        if (BuffWindow)
        {
            AdvTrace($"UsePooledBloodRain: true (buffWindow, charges={charges})");
            return true;
        }

        return true;
    }

    #endregion

    #region Dot Management

    // ========================================================================================
    // IRON JAWS HARD GATE: Prevents early DoT clipping (the ~30s refresh bug).
    // This gate is NEVER bypassed by song transitions or any other logic.
    // ========================================================================================

    private static bool AllowIronJawsHardGate(out string reason)
    {
        reason = string.Empty;

        if (!ActionReady(IronJaws))
        {
            reason = "IronJaws not ready";
            return false;
        }

        var hasPurple = Purple is not null;
        var hasBlue = Blue is not null;

        // Missing DoT(s): DO NOT use Iron Jaws. Use Stormbite/Caustic Bite instead.
        if (!hasPurple || !hasBlue)
        {
            reason = "blocked: missing DoT(s) (use bites, not Iron Jaws)";
            return false;
        }

        var purple = PurpleRemaining;
        var blue = BlueRemaining;

        // HARD FORBID: Never refresh early when both DoTs have plenty of time left.
        // This is the gate that prevents the ~30s refresh bug.
        if (purple > 6f && blue > 6f)
        {
            reason = $"BLOCKED EARLY (purple={purple:0.1}s, blue={blue:0.1}s > 6s threshold)";
            AdvTrace($"Iron Jaws {reason}");
            return false;
        }

        // Allow only when DoTs are expiring soon (<=3.5s).
        if (purple <= 3.5f || blue <= 3.5f)
        {
            reason = $"ALLOWED LATE (purple={purple:0.1}s, blue={blue:0.1}s <= 3.5s threshold)";
            AdvTrace($"Iron Jaws {reason}");
            return true;
        }

        // Mid-range (3.5s < DoT <= 6s): still block to avoid early clipping.
        reason = $"BLOCKED MID (purple={purple:0.1}s, blue={blue:0.1}s in 3.5-6s range)";
        return false;
    }

    // Hard gate for multidot retargeting (same logic, different target).
    private static bool AllowIronJawsHardGate(IGameObject? target, ushort blueDebuff, ushort purpleDebuff, out string reason)
    {
        reason = string.Empty;

        if (target is null)
        {
            reason = "blocked: null target";
            return false;
        }

        if (!ActionReady(IronJaws))
        {
            reason = "IronJaws not ready";
            return false;
        }

        var hasBlue = HasStatusEffect(blueDebuff, target);
        var hasPurple = HasStatusEffect(purpleDebuff, target);

        if (!hasBlue || !hasPurple)
        {
            reason = "blocked: missing DoT(s) on target (use bites)";
            return false;
        }

        var blueRem = GetStatusEffectRemainingTime(GetStatusEffect(blueDebuff, target));
        var purpleRem = GetStatusEffectRemainingTime(GetStatusEffect(purpleDebuff, target));

        // HARD FORBID: both DoTs still healthy.
        if (blueRem > 6f && purpleRem > 6f)
        {
            reason = $"BLOCKED EARLY (blue={blueRem:0.1}s, purple={purpleRem:0.1}s > 6s)";
            return false;
        }

        // Allow only late refresh window.
        if (blueRem <= 3.5f || purpleRem <= 3.5f)
        {
            reason = $"ALLOWED LATE (blue={blueRem:0.1}s, purple={purpleRem:0.1}s <= 3.5s)";
            return true;
        }

        reason = $"BLOCKED MID (blue={blueRem:0.1}s, purple={purpleRem:0.1}s in 3.5-6s range)";
        return false;
    }

    internal static bool UseIronJaws()
    {
        if (!InCombat() || CurrentTarget == null)
            return false;

        var allowed = AllowIronJawsHardGate(out var reason);
        return allowed;
    }

    internal static bool ApplyBlueDot()
    {
        var result = ActionReady(Windbite) && DebuffCapCanBlue && (Blue is null || !CanIronJaws && BlueRemaining < 3.2f);
        if (result)
            AdvTrace($"ApplyBlueDot: true (blueRem={BlueRemaining:0.1}s)");
        return result;
    }

    internal static bool ApplyPurpleDot()
    {
        var result = ActionReady(VenomousBite) && DebuffCapCanPurple && (Purple is null || !CanIronJaws && PurpleRemaining < 3.2f);
        if (result)
            AdvTrace($"ApplyPurpleDot: true (purpleRem={PurpleRemaining:0.1}s)");
        return result;
    }

    internal static bool RagingJawsRefresh()
    {
        // Raging Jaws refresh uses the SAME hard gate (no bypass).
        var allowed = AllowIronJawsHardGate(out var reason);
        if (allowed)
            AdvTrace($"RagingJawsRefresh: ALLOWED ({reason})");
        return allowed;
    }
    #endregion

    #region Buff Timing
    internal static bool UseRadiantBuff()
    {
        if (!CanUseRadiantFinaleNow())
            return false;

        if (HasStatusEffect(Buffs.RagingStrikes))
        {
            AdvTrace($"UseRadiantBuff: true (in Raging Strikes)");
            return true;
        }

        if (RagingCD < 8f || ActionReady(RagingStrikes))
        {
            AdvTrace($"UseRadiantBuff: true (RS soon or ready, RagingCD={RagingCD:0.1}s)");
            return true;
        }

        return false;
    }

    internal static bool UseBattleVoiceBuff()
    {
        if (!ActionReady(BattleVoice))
            return false;

        if (HasStatusEffect(Buffs.RagingStrikes) || HasStatusEffect(Buffs.RadiantFinale))
        {
            AdvTrace($"UseBattleVoiceBuff: true (in buffs)");
            return true;
        }

        if (RagingCD < 8f || ActionReady(RagingStrikes))
        {
            AdvTrace($"UseBattleVoiceBuff: true (RS soon, RagingCD={RagingCD:0.1}s)");
            return true;
        }

        return false;
    }

    internal static bool UseRagingStrikesBuff()
    {
        if (!ActionReady(RagingStrikes))
            return false;

        if (LevelChecked(BattleVoice) && ActionReady(BattleVoice))
        {
            AdvTrace($"UseRagingStrikesBuff: false (prefer BV first)");
            return false;
        }

        AdvTrace($"UseRagingStrikesBuff: true");
        return true;
    }

    internal static bool UseBarrageBuff()
    {
        var result = ActionReady(Barrage) && HasStatusEffect(Buffs.RagingStrikes) && !HasStatusEffect(Buffs.ResonantArrowReady);
        if (result)
            AdvTrace($"UseBarrageBuff: true");
        return result;
    }
    #endregion

    #region Songs

    // ========================================================================================
    // SONG ROTATION LOCK: Enforces zero SongNone time and guaranteed song transitions.
    // This is the HIGHEST PRIORITY check in the song section.
    // ========================================================================================

    internal static bool TryGetForcedNextSong(out uint nextSongAction)
    {
        nextSongAction = 0;
        if (gauge == null) return false;

        // ===== ZERO SONGNONE TIME: Start a song immediately if none is active. =====
        if (SongNone)
        {
            if (ActionReady(WanderersMinuet)) nextSongAction = WanderersMinuet;
            else if (ActionReady(MagesBallad)) nextSongAction = MagesBallad;
            else if (ActionReady(ArmysPaeon)) nextSongAction = ArmysPaeon;

            if (nextSongAction != 0)
            {
                AdvTrace($"[SONG LOCK] SongNone -> FORCE START song actionId={nextSongAction}");
                return true;
            }
            return false;
        }

        // ===== SONG ROTATION LOCK: Force next song at end-of-song (<=1.5s). =====
        if (SongTimerSeconds <= 1.5f)
        {
            if (SongWanderer && ActionReady(MagesBallad))
                nextSongAction = MagesBallad;
            else if (SongMage && ActionReady(ArmysPaeon))
                nextSongAction = ArmysPaeon;
            else if (SongArmy && ActionReady(WanderersMinuet))
                nextSongAction = WanderersMinuet;

            if (nextSongAction != 0)
            {
                AdvTrace($"[SONG LOCK] current={gauge.Song} rem={SongTimerSeconds:0.1}s -> FORCE TRANSITION to actionId={nextSongAction}");
                return true;
            }
        }

        return false;
    }

    // ===== Individual song functions: NO redundant timer checks. =====
    // The forced song lock handles all end-of-song transitions.
    // These functions only handle early clips (Wanderer's clips Army's at ~16s).

    internal static bool WandererSong()
    {
        if (!ActionReady(WanderersMinuet))
            return false;

        if (!(CanBardWeave || !BardHasTarget))
            return false;

        // Zero SongNone time: start Wanderer's first when no song is active.
        if (SongNone)
        {
            AdvTrace($"WandererSong: true (SongNone, starting WM)");
            return true;
        }

        // Early clip: Wanderer's clips Army's Paeon at ~16s so WM comes back on CD.
        // WM (t=0) -> MB (t=45) -> AP (t=90). WM recast is 120s, ready at AP ~15s left.
        if (SongArmy && SongTimerSeconds <= 16f)
        {
            AdvTrace($"WandererSong: true (clipping Army's at {SongTimerSeconds:0.1}s)");
            return true;
        }

        return false;
    }

    internal static bool MagesSong()
    {
        if (!ActionReady(MagesBallad))
            return false;

        if (!(CanBardWeave || !BardHasTarget))
            return false;

        // If we have no song and Wanderer's isn't available, use Mage's.
        if (SongNone && !ActionReady(WanderersMinuet))
        {
            AdvTrace($"MagesSong: true (SongNone, WM not ready)");
            return true;
        }

        // Note: End-of-song transition handled by TryGetForcedNextSong.
        return false;
    }

    internal static bool ArmySong()
    {
        if (!ActionReady(ArmysPaeon))
            return false;

        if (!(CanBardWeave || !BardHasTarget))
            return false;

        // Last resort: if we have no song and neither WM nor MB are available.
        if (SongNone && !ActionReady(MagesBallad) && !ActionReady(WanderersMinuet))
        {
            AdvTrace($"ArmySong: true (SongNone, WM/MB not ready)");
            return true;
        }

        // Note: End-of-song transition handled by TryGetForcedNextSong.
        return false;
    }

    // ===== Song transition helpers: dump procs before transitions. =====
    // These do NOT refresh DoTs (explicit decoupling).

    internal static bool SongChangeEmpyreal()
    {
        // Use Empyreal before transitioning to Army if we have time; never override the hard song lock.
        var result = SongMage &&
               SongTimerSeconds <= 3f &&
               SongTimerSeconds > 1.5f &&
               ActionReady(ArmysPaeon) &&
               ActionReady(EmpyrealArrow) &&
               BardHasTarget &&
               CanBardWeave;

        if (result)
            AdvTrace($"SongChangeEmpyreal: true (dumping Empyreal before Army's, songRem={SongTimerSeconds:0.1}s)");

        return result;
    }

    internal static bool SongChangePitchPerfect()
    {
        // Dump Pitch Perfect stacks before transitioning to Mage's if we have time; never override the hard song lock.
        var result = SongWanderer &&
               SongTimerSeconds <= 3f &&
               SongTimerSeconds > 1.5f &&
               gauge.Repertoire > 0 &&
               BardHasTarget &&
               CanBardWeave;

        if (result)
            AdvTrace($"SongChangePitchPerfect: true (dumping PP stacks={gauge.Repertoire} before Mage's, songRem={SongTimerSeconds:0.1}s)");

        return result;
    }
    #endregion

    #region Warden Resolver
    [ActionRetargeting.TargetResolver]
    private static IGameObject? WardenResolver() =>
        GetPartyMembers()
            .Select(member => member.BattleChara)
            .FirstOrDefault(member => member.IsNotThePlayer() && !member.IsDead && member.IsCleansable() && InActionRange(TheWardensPaeon, member));
    #endregion

    #endregion

    #region ID's

    public const uint
        HeavyShot = 97,
        StraightShot = 98,
        VenomousBite = 100,
        RagingStrikes = 101,
        QuickNock = 106,
        Barrage = 107,
        Bloodletter = 110,
        Windbite = 113,
        MagesBallad = 114,
        ArmysPaeon = 116,
        RainOfDeath = 117,
        BattleVoice = 118,
        EmpyrealArrow = 3558,
        WanderersMinuet = 3559,
        IronJaws = 3560,
        TheWardensPaeon = 3561,
        Sidewinder = 3562,
        PitchPerfect = 7404,
        Troubadour = 7405,
        CausticBite = 7406,
        Stormbite = 7407,
        NaturesMinne = 7408,
        RefulgentArrow = 7409,
        BurstShot = 16495,
        ApexArrow = 16496,
        Shadowbite = 16494,
        Ladonsbite = 25783,
        BlastArrow = 25784,
        RadiantFinale = 25785,
        WideVolley = 36974,
        HeartbreakShot = 36975,
        ResonantArrow = 36976,
        RadiantEncore = 36977;

    public static class Buffs
    {
        public const ushort
            RagingStrikes = 125,
            Barrage = 128,
            MagesBallad = 135,
            ArmysPaeon = 137,
            BattleVoice = 141,
            NaturesMinne = 1202,
            WanderersMinuet = 2216,
            Troubadour = 1934,
            BlastArrowReady = 2692,
            RadiantFinale = 2722,
            ShadowbiteReady = 3002,
            HawksEye = 3861,
            ResonantArrowReady = 3862,
            RadiantEncoreReady = 3863;
    }

    public static class Debuffs
    {
        public const ushort
            VenomousBite = 124,
            Windbite = 129,
            CausticBite = 1200,
            Stormbite = 1201;
    }

    internal static class Traits
    {
        internal const ushort
            EnhancedBloodletter = 445;
    }

    #endregion

    #region Openers
    public static BRDStandard Opener1 = new();
    public static BRDAdjusted Opener2 = new();
    public static BRDComfy Opener3 = new();
    internal static WrathOpener Opener()
    {
        if (IsEnabled(Preset.BRD_ST_AdvMode))
        {
            if (BRD_Adv_Opener_Selection == 0 && Opener1.LevelChecked) return Opener1;
            if (BRD_Adv_Opener_Selection == 1 && Opener2.LevelChecked) return Opener2;
            if (BRD_Adv_Opener_Selection == 2 && Opener3.LevelChecked) return Opener3;
        }
        return Opener1.LevelChecked ? Opener1 : WrathOpener.Dummy;
    }

    internal class BRDStandard : WrathOpener
    {
        public override List<uint> OpenerActions { get; set; } =
        [
            Stormbite,
            WanderersMinuet,
            EmpyrealArrow,
            CausticBite,
            BattleVoice,
            BurstShot,
            RadiantFinale,
            RagingStrikes,
            BurstShot,
            RadiantEncore,
            Barrage,
            RefulgentArrow,
            Sidewinder,
            ResonantArrow,
            EmpyrealArrow,
            BurstShot,
            BurstShot,
            IronJaws,
            BurstShot
        ];
        public override List<(int[], uint, Func<bool>)> SubstitutionSteps { get; set; } =
        [
            ([6, 9, 16, 17, 19], RefulgentArrow, () => HasStatusEffect(Buffs.HawksEye))
        ];
        public override List<int> DelayedWeaveSteps { get; set; } =
        [
            5
        ];
        public override int MinOpenerLevel => 100;
        public override int MaxOpenerLevel => 109;

        public override Preset Preset => Preset.BRD_ST_Adv_Balance_Standard;

        internal override UserData ContentCheckConfig => BRD_Balance_Content;
        public override bool HasCooldowns() =>
            IsOffCooldown(WanderersMinuet) &&
            IsOffCooldown(BattleVoice) &&
            IsOffCooldown(RadiantFinale) &&
            IsOffCooldown(RagingStrikes) &&
            IsOffCooldown(Barrage) &&
            IsOffCooldown(Sidewinder);
    }
    internal class BRDAdjusted : WrathOpener
    {
        public override List<uint> OpenerActions { get; set; } =
        [
            HeartbreakShot,
            Stormbite,
            WanderersMinuet,
            EmpyrealArrow,
            CausticBite,
            BattleVoice,
            BurstShot,
            RadiantFinale,
            RagingStrikes,
            BurstShot,
            Barrage,
            RefulgentArrow,
            Sidewinder,
            RadiantEncore,
            ResonantArrow,
            EmpyrealArrow,
            BurstShot,
            BurstShot,
            IronJaws,
            BurstShot
        ];
        public override List<(int[], uint, Func<bool>)> SubstitutionSteps { get; set; } =
        [
            ([7, 10, 17, 18, 20], RefulgentArrow, () => HasStatusEffect(Buffs.HawksEye))
        ];
        public override List<int> DelayedWeaveSteps { get; set; } =
        [
            6
        ];
        public override int MinOpenerLevel => 100;
        public override int MaxOpenerLevel => 109;

        public override Preset Preset => Preset.BRD_ST_Adv_Balance_Standard;

        internal override UserData ContentCheckConfig => BRD_Balance_Content;
        public override bool HasCooldowns() =>
            IsOffCooldown(WanderersMinuet) &&
            IsOffCooldown(BattleVoice) &&
            IsOffCooldown(RadiantFinale) &&
            IsOffCooldown(RagingStrikes) &&
            IsOffCooldown(Barrage) &&
            IsOffCooldown(Sidewinder);
    }
    internal class BRDComfy : WrathOpener
    {
        public override List<uint> OpenerActions { get; set; } =
        [
            Stormbite,
            HeartbreakShot,
            WanderersMinuet,
            CausticBite,
            EmpyrealArrow,
            RadiantFinale,
            BurstShot,
            BattleVoice,
            RagingStrikes,
            BurstShot,
            Barrage,
            RefulgentArrow,
            Sidewinder,
            RadiantEncore,
            ResonantArrow,
            BurstShot,
            EmpyrealArrow,
            BurstShot,
            IronJaws,
            BurstShot
        ];
        public override List<(int[], uint, Func<bool>)> SubstitutionSteps { get; set; } =
        [
            ([7, 10, 16, 18, 20], RefulgentArrow, () => HasStatusEffect(Buffs.HawksEye))
        ];
        public override int MinOpenerLevel => 100;
        public override int MaxOpenerLevel => 109;
        public override Preset Preset => Preset.BRD_ST_Adv_Balance_Standard;
        internal override UserData ContentCheckConfig => BRD_Balance_Content;
        public override bool HasCooldowns() =>
            IsOffCooldown(WanderersMinuet) &&
            IsOffCooldown(BattleVoice) &&
            IsOffCooldown(RadiantFinale) &&
            IsOffCooldown(RagingStrikes) &&
            IsOffCooldown(Barrage) &&
            IsOffCooldown(Sidewinder);
    }
    #endregion
}
