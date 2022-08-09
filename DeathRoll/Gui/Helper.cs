using System;
using System.Numerics;
using System.Text.Json;
using Dalamud.Logging;
using DeathRoll.Data;
using ImGuiNET;

namespace DeathRoll.Gui;

public static class Helper
{
    private const ImGuiWindowFlags Flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize;

    // https://www.programcreek.com/cpp/?code=kswaldemar%2Frewind-viewer%2Frewind-viewer-master%2Fsrc%2Fimgui_impl%2Fimgui_widgets.cpp
    public static void ShowHelpMarker(string desc) {
        ImGui.TextDisabled("(?)");
        if (!ImGui.IsItemHovered()) return;
        
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(450.0f);
        ImGui.TextUnformatted(desc);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    public static void ErrorWindow(ref string msg)
    {
        if (ImGui.Begin("Error##popup_helper_error", Flags))
        {
            ImGui.Text(msg);
                
            ImGui.Spacing();
            ImGui.NextColumn();

            ImGui.Columns(1);
            ImGui.Separator();

            ImGui.NewLine();

            ImGui.SameLine(120);
            //click ok when finished adjusting
            if (ImGui.Button("OK", new Vector2(100, 0))) {
                msg = string.Empty;
            }

            ImGui.End();
        }
    }
    
    public static bool PlayerListRender(string title, Participants participants, ImGuiTreeNodeFlags flags)
    {
        if (participants.PList.Count == 0) return false;
        
        try
        {
            var deletion = "";
            if (!ImGui.CollapsingHeader(title, flags)) return false;
            
            foreach (var playerName in participants.PlayerNameList)
            {
                var participant = participants.FindPlayer(playerName);
                var name = participant.GetDisplayName();
                ImGui.Selectable($"{name}");
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right) && ImGui.GetIO().KeyShift)
                    deletion = participant.name;

                if (!ImGui.IsItemHovered()) continue;
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted("Hold Shift and right-click to delete.");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
            
            if (deletion != "") participants.DeleteEntry(deletion);
            return true;
        }
        catch (NullReferenceException e)
        {
            PluginLog.Error(e.Message);
            foreach (var pname in participants.PlayerNameList) PluginLog.Information($"Name in cause: {pname}");
            foreach (var participant in participants.PList) PluginLog.Information($"Participants: {JsonSerializer.Serialize(participant)}");
            Plugin.SwitchState(GameState.NotRunning);
            return false;
        }
    }
}