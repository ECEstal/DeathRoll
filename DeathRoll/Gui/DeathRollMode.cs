using System.Linq;
using System.Numerics;
using ImGuiNET;

namespace DeathRoll.Gui;

public class DeathRollMode
{
    private readonly Vector4 _redColor = new(0.980f, 0.245f, 0.245f, 1.0f);
    private readonly Configuration configuration;
    private readonly Participants participants;

    private readonly PluginUI pluginUi;
    public Timers Timers;

    public DeathRollMode(PluginUI pluginUi)
    {
        this.pluginUi = pluginUi;
        configuration = pluginUi.Configuration;
        participants = pluginUi.Participants;
        Timers = new Timers(configuration);
    }

    public void MainRender()
    {
        RenderControlPanel();

        if (participants.RoundDone)
        {
            ImGui.Dummy(new Vector2(0.0f, 10.0f));
            RenderWinnerPanel();  
        }
        
        ImGui.Dummy(new Vector2(0.0f, 10.0f));
        ParticipantRender();

        if (participants.PList.Count == 0) return;
        
        ImGui.Dummy(new Vector2(0.0f, 10.0f));
        OrderListRender();
    }

    private void RenderWinnerPanel()
    {
        var loser = participants.PList.Last();
        ImGui.TextColored(_redColor, $"{loser.name} lost!!!");
    }

    public void RenderControlPanel()
    {
        if (ImGui.Button("Show Settings")) pluginUi.SettingsVisible = true;
        
        var spacing = ImGui.GetScrollMaxY() == 0 ? 80.0f : 95.0f;
        ImGui.SameLine(ImGui.GetWindowWidth() - spacing);

        if (ImGui.Button("New Round"))
        {
            configuration.ActiveRound = true;
            configuration.AcceptNewPlayers = true;
            participants.Reset();
        }
        
        var activeRound = configuration.ActiveRound;
        if (ImGui.Checkbox("Active Round", ref activeRound))
        {
            configuration.ActiveRound = activeRound;
            configuration.Save();
        }        
        ImGui.SameLine();
        
        var acceptNewPlayers = configuration.AcceptNewPlayers;
        if (ImGui.Checkbox("Accept New Players", ref acceptNewPlayers))
        {
            configuration.AcceptNewPlayers = acceptNewPlayers;
            configuration.Save();
        }
    }
    
    public void ParticipantRender()
    {
        if (!ImGui.BeginTable("##rolls", 3)) return;
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, 3.0f);
        ImGui.TableSetupColumn("Roll");
        ImGui.TableSetupColumn("Out Of");

        ImGui.TableHeadersRow();
        foreach (var (participant, idx) in participants.PList.Select((value, i) => (value, i)))
        {
            var name = participant.GetUsedName(configuration.DebugRandomPn);

            ImGui.TableNextColumn();
            ImGui.Text(name);

            ImGui.TableNextColumn();
            ImGui.Text(participant.roll.ToString());
            
            ImGui.TableNextColumn();
            ImGui.Text(participant.outOf.ToString());
        }

        ImGui.EndTable();
    }
    
    public void OrderListRender()
    {
        var deletion = "";
        if (ImGui.CollapsingHeader("Player List"))
            foreach (var playerName in participants.PlayerNameList)
            {
                var participant = participants.FindPlayer(playerName);
                var name = participant.GetUsedName(configuration.DebugRandomPn);
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
    }
}