using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ParseLord2.Core;
using ParseLord2.Services;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using Action = Lumina.Excel.Sheets.Action;

namespace ParseLord2.Window.Tabs;

internal static class StacksTab
{
    private static int SelectedStackIndex = -1;
    private static string NewStackName = "New Stack";
    private static string ActionSearch = string.Empty;
    private static uint ManualAddActionId = 0;
    private static uint MapActionId = 0;

    internal static void Draw()
    {
        var cfg = Service.Configuration;

        cfg.PriorityStacks ??= new List<PriorityStackConfig>();
        cfg.ActionPriorityStackMap ??= new Dictionary<uint, string>();

        ImGui.TextUnformatted("Stacks (ReactionEX-style)");
        ImGui.TextWrapped("A Stack is an ordered list of ActionIds. When an action is mapped to a stack, ParseLord2 will try stack entries from top to bottom and pick the first usable action. No input interception, no targeting automation.");

        using var table = ImRaii.Table("###StacksMainTable", 2, ImGuiTableFlags.SizingStretchProp);
        if (!table) return;

        ImGui.TableSetupColumn("Stacks", ImGuiTableColumnFlags.WidthFixed, 260);
        ImGui.TableSetupColumn("Editor", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextRow();

        DrawLeftStacksList(cfg, ref cfg);
        ImGui.TableNextColumn();
        DrawRightEditor(cfg);
    }

    private static void DrawLeftStacksList(Configuration cfg, ref Configuration _)
    {
        ImGui.TableNextColumn();

        using var leftChild = ImRaii.Child("###StacksLeft", new Vector2(0, 0), true);
        if (!leftChild) return;

        ImGui.TextUnformatted("Stacks");
        ImGui.Separator();

        using (ImRaii.PushIndent(4))
        {
            for (int i = 0; i < cfg.PriorityStacks.Count; i++)
            {
                var s = cfg.PriorityStacks[i];
                var label = string.IsNullOrWhiteSpace(s.Name) ? $"(Unnamed)##stack{i}" : $"{s.Name}##stack{i}";
                bool selected = i == SelectedStackIndex;

                if (ImGui.Selectable(label, selected))
                    SelectedStackIndex = i;

                if (!s.Enabled)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled("(disabled)");
                }
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("###NewStackName", ref NewStackName, 64);

        bool add = ImGui.Button("Add Stack");
        ImGui.SameLine();

        bool remove = ImGui.Button("Remove Selected");
        if (add)
        {
            var name = (NewStackName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = "New Stack";

            // Ensure unique name
            name = MakeUniqueStackName(cfg, name);

            cfg.PriorityStacks.Add(new PriorityStackConfig
            {
                Name = name,
                Enabled = true,
                ActionIds = new List<uint>(),
            });

            SelectedStackIndex = cfg.PriorityStacks.Count - 1;
            cfg.Save();
        }

        if (remove)
        {
            if (SelectedStackIndex >= 0 && SelectedStackIndex < cfg.PriorityStacks.Count)
            {
                var removedName = cfg.PriorityStacks[SelectedStackIndex].Name ?? string.Empty;

                cfg.PriorityStacks.RemoveAt(SelectedStackIndex);
                SelectedStackIndex = Math.Clamp(SelectedStackIndex, -1, cfg.PriorityStacks.Count - 1);

                // Clean up mappings that referenced this stack name
                if (!string.IsNullOrWhiteSpace(removedName))
                {
                    var keys = cfg.ActionPriorityStackMap.Where(kv => string.Equals(kv.Value, removedName, StringComparison.OrdinalIgnoreCase))
                        .Select(kv => kv.Key)
                        .ToList();

                    foreach (var k in keys)
                        cfg.ActionPriorityStackMap.Remove(k);
                }

                cfg.Save();
            }
        }
    }

    private static void DrawRightEditor(Configuration cfg)
    {
        using var rightChild = ImRaii.Child("###StacksRight", new Vector2(0, 0), false);
        if (!rightChild) return;

        if (SelectedStackIndex < 0 || SelectedStackIndex >= cfg.PriorityStacks.Count)
        {
            ImGui.TextDisabled("Select a stack on the left.");
            return;
        }

        var stack = cfg.PriorityStacks[SelectedStackIndex];

        ImGui.TextUnformatted("Stack Editor");
        ImGui.Separator();

        // Name
        var name = stack.Name ?? string.Empty;
        ImGui.SetNextItemWidth(300);
        if (ImGui.InputText("Name", ref name, 64))
        {
            name = name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = "Unnamed";

            // If renaming, ensure uniqueness + update action map references
            var oldName = stack.Name ?? string.Empty;
            var newName = MakeUniqueStackName(cfg, name, allowSameIfMatchesIndex: SelectedStackIndex);

            if (!string.Equals(oldName, newName, StringComparison.Ordinal))
            {
                stack.Name = newName;

                var keys = cfg.ActionPriorityStackMap
                    .Where(kv => string.Equals(kv.Value, oldName, StringComparison.OrdinalIgnoreCase))
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var k in keys)
                    cfg.ActionPriorityStackMap[k] = newName;
            }

            cfg.Save();
        }

        // Enabled
        bool enabled = stack.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            stack.Enabled = enabled;
            cfg.Save();
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Add Actions to Stack");
        ImGui.Separator();

        // Manual add
        ImGui.SetNextItemWidth(200);
        var manualTmp = (int)ManualAddActionId;
        if (ImGui.InputInt("ActionId", ref manualTmp))
        {
            if (manualTmp < 0) manualTmp = 0;
            ManualAddActionId = (uint)manualTmp;
        }

        ImGui.SameLine();
        if (ImGui.Button("Add##ManualAdd"))
        {
            if (ManualAddActionId != 0 && !stack.ActionIds.Contains(ManualAddActionId))
            {
                stack.ActionIds.Add(ManualAddActionId);
                cfg.Save();
            }
        }

        // Search add
        ImGui.SetNextItemWidth(350);
        ImGui.InputText("Search", ref ActionSearch, 64);
        ImGui.SameLine();
        ImGui.TextDisabled("Click a result to add");

        DrawActionSearchResults(cfg, stack);

        ImGui.Spacing();
        ImGui.TextUnformatted("Stack Order (top = highest priority)");
        ImGui.Separator();
        DrawStackActionList(cfg, stack);

        ImGui.Spacing();
        ImGui.TextUnformatted("Map ActionId → Stack (optional)");
        ImGui.Separator();
        DrawActionToStackMapper(cfg);
    }

    private static void DrawActionSearchResults(Configuration cfg, PriorityStackConfig stack)
    {
        if (string.IsNullOrWhiteSpace(ActionSearch) || ActionSearch.Trim().Length < 2)
            return;

        var sheet = Svc.Data.GetExcelSheet<Action>();
        if (sheet == null)
        {
            ImGui.TextDisabled("Action sheet unavailable.");
            return;
        }

        var q = ActionSearch.Trim();
        var results = new List<(uint id, string name, uint icon)>();

        foreach (var a in sheet)
        {
            if (a.RowId == 0)
                continue;

            var name = ExcelActionHelper.GetActionName(a, true);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (!name.Contains(q, StringComparison.OrdinalIgnoreCase))
                continue;

            results.Add(((uint)a.RowId, name, a.Icon));

            if (results.Count >= 30)
                break;
        }

        if (results.Count == 0)
        {
            ImGui.TextDisabled("No matches.");
            return;
        }

        using var child = ImRaii.Child("###StacksSearchResults", new Vector2(0, 140), true);
        if (!child) return;

        foreach (var r in results)
        {
            var label = $"{r.name}  (#{r.id})";
            if (ImGui.Selectable(label))
            {
                if (!stack.ActionIds.Contains(r.id))
                {
                    stack.ActionIds.Add(r.id);
                    cfg.Save();
                }
            }
        }
    }

    private static void DrawStackActionList(Configuration cfg, PriorityStackConfig stack)
    {
        if (stack.ActionIds.Count == 0)
        {
            ImGui.TextDisabled("No actions in this stack yet.");
            return;
        }

        var sheet = Svc.Data.GetExcelSheet<Action>();

        using var table = ImRaii.Table("###StacksActionList", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV);
        if (!table) return;

        ImGui.TableSetupColumn("Order", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Move", ImGuiTableColumnFlags.WidthFixed, 90);
        ImGui.TableSetupColumn("Remove", ImGuiTableColumnFlags.WidthFixed, 70);
        ImGui.TableHeadersRow();

        for (int i = 0; i < stack.ActionIds.Count; i++)
        {
            uint id = stack.ActionIds[i];
            string name = id.ToString();

            if (sheet != null)
            {
                var a = sheet.GetRow(id);
                if (a.RowId != 0)
                {
                    var n = ExcelActionHelper.GetActionName(a, true);
                    if (!string.IsNullOrWhiteSpace(n))
                        name = n;
                }
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted((i + 1).ToString());

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{name}  (#{id})");

            ImGui.TableNextColumn();
            bool moved = false;

            ImGui.BeginDisabled(i == 0);
            if (ImGui.Button($"Up##stack{i}"))
            {
                (stack.ActionIds[i - 1], stack.ActionIds[i]) = (stack.ActionIds[i], stack.ActionIds[i - 1]);
                moved = true;
            }
            ImGui.EndDisabled();

            ImGui.SameLine();
            ImGui.BeginDisabled(i == stack.ActionIds.Count - 1);
            if (ImGui.Button($"Down##stack{i}"))
            {
                (stack.ActionIds[i + 1], stack.ActionIds[i]) = (stack.ActionIds[i], stack.ActionIds[i + 1]);
                moved = true;
            }
            ImGui.EndDisabled();

            ImGui.TableNextColumn();
            if (ImGui.Button($"Remove##stackRemove{i}"))
            {
                stack.ActionIds.RemoveAt(i);
                cfg.Save();
                return;
            }

            if (moved)
                cfg.Save();
        }
    }

    private static void DrawActionToStackMapper(Configuration cfg)
    {
        // Lightweight mapper: you can type an ActionId and assign it to the selected stack name
        var mapTmp = (int)MapActionId;
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputInt("###MapActionId", ref mapTmp))
        {
            if (mapTmp < 0) mapTmp = 0;
            MapActionId = (uint)mapTmp;
        }

        var actionId = MapActionId;
        ImGui.SameLine();
        if (ImGui.Button("Assign to Selected Stack"))
        {
            if (actionId != 0 && SelectedStackIndex >= 0 && SelectedStackIndex < cfg.PriorityStacks.Count)
            {
                var name = cfg.PriorityStacks[SelectedStackIndex].Name ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    cfg.ActionPriorityStackMap[actionId] = name;
                    cfg.Save();
                }
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear Mapping"))
        {
            if (actionId != 0 && cfg.ActionPriorityStackMap.Remove(actionId))
                cfg.Save();
        }

        // Show mappings count
        ImGui.Spacing();
        ImGui.TextDisabled($"Mappings: {cfg.ActionPriorityStackMap.Count}");
    }

    private static string MakeUniqueStackName(Configuration cfg, string desired, int? allowSameIfMatchesIndex = null)
    {
        desired = desired.Trim();
        if (string.IsNullOrWhiteSpace(desired))
            desired = "New Stack";

        bool NameTaken(string n)
        {
            for (int i = 0; i < cfg.PriorityStacks.Count; i++)
            {
                if (allowSameIfMatchesIndex.HasValue && i == allowSameIfMatchesIndex.Value)
                    continue;

                var existing = cfg.PriorityStacks[i].Name ?? string.Empty;
                if (string.Equals(existing, n, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        if (!NameTaken(desired))
            return desired;

        for (int i = 2; i < 1000; i++)
        {
            var candidate = $"{desired} ({i})";
            if (!NameTaken(candidate))
                return candidate;
        }

        // Fallback
        return $"{desired} ({Guid.NewGuid().ToString()[..8]})";
    }
}
