using System;
using ParseLord2.AutoRotation;
using ParseLord2.API.Enum;
using ParseLord2.Services;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

namespace ParseLord2.Window.Tabs;

internal static class AutoRotationTab
{
    internal static void Draw()
    {
        var cfg = Service.Configuration.RotationConfig;
        bool changed = false;

        ImGui.TextWrapped("Auto-Rotation (ParseLord2)");
        ImGui.Separator();

        changed |= ImGui.Checkbox("Enable Auto-Rotation", ref cfg.Enabled);

        ImGui.Separator();
        ImGui.TextUnformatted("Manual Override");
        ImGui.SetNextItemWidth(220);
        int overrideMs = cfg.ManualInputOverrideMs;
        if (ImGui.SliderInt("Manual input override window (ms)", ref overrideMs, 0, 3000))
        {
            cfg.ManualInputOverrideMs = overrideMs;
            changed = true;
        }
        changed |= ImGui.Checkbox("Only in Combat", ref cfg.InCombatOnly);

        int combatDelay = cfg.CombatDelay;
        if (ImGui.SliderInt("Combat Delay (seconds)", ref combatDelay, 0, 10))
        {
            cfg.CombatDelay = combatDelay;
            changed = true;
        }


        int healDelay = cfg.HealerSettings.HealDelay;
        if (ImGui.SliderInt("Heal Delay (seconds)", ref healDelay, 0, 10))
        {
            cfg.HealerSettings.HealDelay = healDelay;
            changed = true;
        }
        ImGui.Separator();
        ImGui.TextUnformatted("Healing Thresholds (RSR-style)");

        int tankHealHpp = cfg.HealerSettings.TankHealHPP;
        if (ImGui.SliderInt("Tank heal threshold (%)", ref tankHealHpp, 1, 100))
        {
            cfg.HealerSettings.TankHealHPP = tankHealHpp;
            changed = true;
        }

        int partyHealHpp = cfg.HealerSettings.PartyHealHPP;
        if (ImGui.SliderInt("Party heal threshold (%)", ref partyHealHpp, 1, 100))
        {
            cfg.HealerSettings.PartyHealHPP = partyHealHpp;
            changed = true;
        }

        int emergencyHealHpp = cfg.HealerSettings.EmergencyHealHPP;
        if (ImGui.SliderInt("Emergency heal threshold (%)", ref emergencyHealHpp, 1, 100))
        {
            cfg.HealerSettings.EmergencyHealHPP = emergencyHealHpp;
            changed = true;
        }

        int aoeMinInjured = cfg.HealerSettings.AoEHealMinInjuredCount;
        if (ImGui.SliderInt("AoE heal: min injured party members", ref aoeMinInjured, 1, 8))
        {
            cfg.HealerSettings.AoEHealMinInjuredCount = aoeMinInjured;
            changed = true;
        }


        changed |= ImGui.Checkbox("Bypass Quest logic", ref cfg.BypassQuest);
        changed |= ImGui.Checkbox("Bypass FATE logic", ref cfg.BypassFATE);
        changed |= ImGui.Checkbox("Bypass Buff logic", ref cfg.BypassBuffs);

        changed |= ImGui.Checkbox("Enable in Instance", ref cfg.EnableInInstance);
        changed |= ImGui.Checkbox("Disable after leaving Instance", ref cfg.DisableAfterInstance);

        int throttler = cfg.Throttler;
        if (ImGui.SliderInt("Engine Throttle (ms)", ref throttler, 10, 500))
        {
            cfg.Throttler = throttler;
            changed = true;
        }

        changed |= ImGui.Checkbox("Orbwalker Integration", ref cfg.OrbwalkerIntegration);

        ImGui.Separator();
        DrawEnumStepper("DPS Rotation Mode", ref cfg.DPSRotationMode, ref changed);
        DrawEnumStepper("Healer Rotation Mode", ref cfg.HealerRotationMode, ref changed);

        ImGui.Separator();
        ImGui.TextUnformatted("DPS Targeting");
        
        changed |= ImGui.Checkbox("Enable Vision Cone", ref cfg.DPSSettings.EnableVisionCone);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Only attack targets within your vision cone (based on where you're facing)");
        
        if (cfg.DPSSettings.EnableVisionCone)
        {
            ImGui.Indent();
            float visionAngle = cfg.DPSSettings.VisionConeAngle;
            if (ImGui.SliderFloat("Vision Cone Angle", ref visionAngle, 30f, 360f, "%.0f°"))
            {
                cfg.DPSSettings.VisionConeAngle = visionAngle;
                changed = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Cone angle in degrees. 120° = narrow cone, 180° = half-circle, 360° = full circle");
            ImGui.Unindent();
        }

        if (changed)
            Service.Configuration.Save();
    }

    private static void DrawEnumStepper<TEnum>(string label, ref TEnum value, ref bool changed)
        where TEnum : unmanaged, Enum
    {
        ImGui.Text(label);
        ImGui.SameLine();

        if (ImGui.Button("<##" + label))
        {
            value = StepEnum(value, -1);
            changed = true;
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(value.ToString());
        ImGui.SameLine();

        if (ImGui.Button(">##" + label))
        {
            value = StepEnum(value, +1);
            changed = true;
        }
    }

    private static TEnum StepEnum<TEnum>(TEnum current, int delta)
        where TEnum : unmanaged, Enum
    {
        var values = (TEnum[])Enum.GetValues(typeof(TEnum));
        int idx = Array.IndexOf(values, current);
        if (idx < 0) idx = 0;

        idx = (idx + delta) % values.Length;
        if (idx < 0) idx += values.Length;

        return values[idx];
    }
}