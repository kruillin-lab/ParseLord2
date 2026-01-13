using System;
using System.Collections.Generic;
using System.Linq;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using System.Numerics;
using Lumina.Excel.Sheets;
using ParseLord2.AutoRotation;
using ParseLord2.Core;
using ParseLord2.Services;
using ParseLord2.Window;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using Action = Lumina.Excel.Sheets.Action;

namespace ParseLord2.Window.Tabs;

internal static class AbilitiesTab
{
    private static string Search = string.Empty;
    private static uint SelectedActionId = 0;
    private static ActionViewFilter ViewFilter = ActionViewFilter.All;

    private enum ActionViewFilter
    {
        All = 0,
        Enabled = 1,
        Disabled = 2,
        Modified = 3,
    }

    internal static void Draw()
    {
        var cfg = Service.Configuration;
        bool changed = false;

        ImGui.TextUnformatted("Abilities (RSR-style)");
        ImGui.TextWrapped("Click an action on the left to configure it. Settings apply to that action only.");
        ImGui.Separator();

        // --- Top controls ---
        ImGui.SetNextItemWidth(350);
        ImGui.InputText("Search", ref Search, 200);

        ImGui.SameLine();
        if (ImGui.Button("Clear"))
            Search = string.Empty;

        ImGui.SameLine();
        DrawViewFilter();

        ImGui.SameLine();
        if (ImGui.Button("Enable All (visible)"))
        {
            foreach (var a in EnumerateVisibleActions(Search, ViewFilter))
                SetActionEnabled(cfg, a.RowId, true);

            changed = true;
        }

        ImGui.SameLine();
        if (ImGui.Button("Disable All (visible)"))
        {
            foreach (var a in EnumerateVisibleActions(Search, ViewFilter))
                SetActionEnabled(cfg, a.RowId, false);

            changed = true;
        }

        ImGui.Spacing();

        // --- Two-pane layout ---
        float leftWidth = 360;
        float rightWidth = Math.Max(200, ImGui.GetContentRegionAvail().X - leftWidth - 10);

        ImGui.BeginChild("##ActionsLeft", new(leftWidth, 0), true);
        DrawLeftActionList(cfg, ref changed);
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("##ActionsRight", new(rightWidth, 0), true);
        DrawRightActionEditor(cfg, ref changed);
        ImGui.EndChild();

        if (changed)
            cfg.Save();
    }

    private static void DrawViewFilter()
    {
        ImGui.SetNextItemWidth(140);
        if (ImGui.BeginCombo("View", ViewFilter.ToString()))
        {
            foreach (ActionViewFilter v in Enum.GetValues(typeof(ActionViewFilter)))
            {
                if (ImGui.Selectable(v.ToString(), v == ViewFilter))
                    ViewFilter = v;
            }
            ImGui.EndCombo();
        }
    }

    private static void DrawLeftActionList(Configuration cfg, ref bool changed)
    {
        var actions = EnumerateVisibleActions(Search, ViewFilter).ToList();

        if (actions.Count == 0)
        {
            ImGui.TextDisabled("No matching actions.");
            return;
        }

        foreach (var a in actions)
        {
            var actionId = a.RowId;
            var name = ExcelActionHelper.GetActionName(a, true);

            bool enabled = IsActionEnabled(cfg, actionId);
            bool modified = IsActionModified(cfg, actionId);

            // Icon
            // Use the plugin's icon helper; avoid "ParseLord2.Window" qualifier because ParseLord2 is also a type name in this repo.
            var tex = Icons.GetTextureFromIconId(a.Icon, 0, true);
            if (tex != null)
            {
                ImGui.Image(tex.Handle, new Vector2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight()));
                ImGui.SameLine();
            }

            // Enabled toggle
            bool enabledUi = enabled;
            if (ImGui.Checkbox($"##enable_{actionId}", ref enabledUi))
            {
                SetActionEnabled(cfg, actionId, enabledUi);
                changed = true;
            }

            ImGui.SameLine();

            // Selectable name (+ modified marker)
            string label = modified ? $"{name}  *" : name;
            if (ImGui.Selectable(label, SelectedActionId == actionId))
                SelectedActionId = actionId;

            if (SelectedActionId == actionId && ImGui.IsWindowAppearing())
                ImGui.SetScrollHereY();
        }
    }

    private static void DrawRightActionEditor(Configuration cfg, ref bool changed)
    {
        if (SelectedActionId == 0)
        {
            ImGui.TextDisabled("Select an action on the left.");
            return;
        }

        var sheet = Svc.Data.GetExcelSheet<Action>();
        if (sheet == null)
        {
            ImGui.TextDisabled("Action sheet unavailable.");
            return;
        }

        var a = sheet.GetRow(SelectedActionId);
        if (a.RowId == 0)
        {
            ImGui.TextDisabled("Action not found.");
            return;
        }

        var name = ExcelActionHelper.GetActionName(a, true);

        ImGui.TextUnformatted(name);
        ImGui.Separator();

        // Enabled
        bool enabled = IsActionEnabled(cfg, SelectedActionId);
        bool enabledUi = enabled;
        if (ImGui.Checkbox("Enabled (this action)", ref enabledUi))
        {
            SetActionEnabled(cfg, SelectedActionId, enabledUi);
            changed = true;
        }

        ImGui.SameLine();
        if (ImGui.Button("Reset settings (keep enabled state)"))
        {
            cfg.ActionRules.Remove(SelectedActionId);
            changed = true;
        }

        ImGui.SameLine();
        if (ImGui.Button("Reset + Disable"))
        {
            cfg.ActionRules.Remove(SelectedActionId);
            SetActionEnabled(cfg, SelectedActionId, false);
            changed = true;
        }

        ImGui.Spacing();

        var rule = GetOrCreateActionRule(cfg, SelectedActionId);

        // --- RSR-like toggles (plain + grouped) ---
        bool localChanged = false;

        if (ImGui.Checkbox("Allow action to be intercepted by the intercept system", ref rule.IsIntercepted))
            localChanged = true;

        if (ImGui.Checkbox("Allow action to be restricted by the minimum HP feature", ref rule.MinHPFeature))
            localChanged = true;

        if (ImGui.Checkbox("Prevent this action against a curated list of mobs", ref rule.IsRestrictedDOT))
            localChanged = true;

        if (ImGui.Checkbox("Show on CD window", ref rule.IsOnCooldownWindow))
            localChanged = true;

        ImGui.Separator();

        // Time-to-kill
        float ttk = rule.TimeToKill;
        ImGui.SetNextItemWidth(220);
        if (ImGui.SliderFloat("Time-to-kill threshold (seconds)", ref ttk, 0, 120))
        {
            rule.TimeToKill = (int)ttk;
            localChanged = true;
        }

        // Status checks
        if (ImGui.CollapsingHeader("Should this action check needed status effects", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (ImGui.Checkbox("Check status effects", ref rule.ShouldCheckStatus))
                localChanged = true;

            if (ImGui.Checkbox("Check target status (instead of self)", ref rule.ShouldCheckTargetStatus))
                localChanged = true;

            int gcds = rule.StatusGcdCount;
            ImGui.SetNextItemWidth(220);
            if (ImGui.SliderInt("Number of GCDs before DOT/Status is reapplied", ref gcds, 0, 10))
            {
                rule.StatusGcdCount = gcds;
                localChanged = true;
            }

            float healRatio = rule.AutoHealRatio * 100f;
            ImGui.SetNextItemWidth(220);
            if (ImGui.SliderFloat("HP ratio for automatic healing (healing actions only)", ref healRatio, 0, 100))
            {
                rule.AutoHealRatio = healRatio / 100f;
                localChanged = true;
            }
        }

        if (localChanged)
        {
            CleanupRuleIfDefault(cfg, SelectedActionId);
            changed = true;
        }

        // Optional debug info (mirrors RSR's detail dump)
        if (ImGui.CollapsingHeader("Debug info"))
        {
            ImGui.TextUnformatted($"ID: {SelectedActionId}");
            ImGui.TextUnformatted($"Is PvP Action: {a.IsPvP}");
            ImGui.TextUnformatted($"Cast Time: {a.Cast100ms / 10f:0.0}s");
            ImGui.TextUnformatted($"Level: {a.ClassJobLevel}");
        }
    }

    private static IEnumerable<Action> EnumerateVisibleActions(string search, ActionViewFilter filter)
    {
        var sheet = Svc.Data.GetExcelSheet<Action>();
        if (sheet == null)
            yield break;

        var job = Player.Job;

        foreach (var a in sheet)
        {
            if (!a.IsPlayerAction)
                continue;

            if (a.IsPvP)
                continue;

            if (!a.ClassJobCategory.IsValid || a.ClassJobCategory.RowId == 1)
                continue;

            if (!a.ClassJobCategory.Value.IsJobInCategory(job))
                continue;

            var name = ExcelActionHelper.GetActionName(a, true);
            if (!string.IsNullOrWhiteSpace(search) &&
                !name.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;

            bool enabled = IsActionEnabled(Service.Configuration, a.RowId);
            bool modified = IsActionModified(Service.Configuration, a.RowId);

            if (filter == ActionViewFilter.Enabled && !enabled)
                continue;

            if (filter == ActionViewFilter.Disabled && enabled)
                continue;

            if (filter == ActionViewFilter.Modified && !modified)
                continue;

            yield return a;
        }
    }

    private static bool IsActionEnabled(Configuration cfg, uint actionId)
        => !cfg.ActionEnabled.TryGetValue(actionId, out var enabled) || enabled;

    private static void SetActionEnabled(Configuration cfg, uint actionId, bool enabled)
    {
        // Default = enabled. Keep dictionary sparse.
        if (enabled)
            cfg.ActionEnabled.Remove(actionId);
        else
            cfg.ActionEnabled[actionId] = false;
    }

    private static bool IsActionModified(Configuration cfg, uint actionId)
    {
        if (!cfg.ActionRules.TryGetValue(actionId, out var rule))
            return false;

        return RuleHasAnyNonDefault(rule);
    }

    private static AbilityRuleConfig GetOrCreateActionRule(Configuration cfg, uint actionId)
    {
        if (!cfg.ActionRules.TryGetValue(actionId, out var rule))
        {
            rule = new AbilityRuleConfig();
            cfg.ActionRules[actionId] = rule;
        }

        return rule;
    }

    private static void CleanupRuleIfDefault(Configuration cfg, uint actionId)
    {
        if (cfg.ActionRules.TryGetValue(actionId, out var rule) && !RuleHasAnyNonDefault(rule))
            cfg.ActionRules.Remove(actionId);
    }

    private static bool RuleHasAnyNonDefault(AbilityRuleConfig rule)
        => rule.IsEnabled
           || rule.IsIntercepted
           || rule.IsRestrictedDOT
           || rule.ShouldCheckStatus
           || rule.ShouldCheckTargetStatus
           || rule.StatusGcdCount != 0
           || rule.ShouldCheckCombo
           || rule.AoeCount != 0
           || rule.TimeToKill != 0
           || Math.Abs(rule.AutoHealRatio - 0f) > 0.0001f
           || rule.HealHppWithoutHot != 0
           || rule.HealHppWithHot != 0
           || rule.IsOnCooldownWindow
           || rule.MinHPFeature
           || Math.Abs(rule.MinHPPercent - 0f) > 0.0001f;
}