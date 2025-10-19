using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace CaptureTheFlagGamemode;

public partial class CaptureTheFlag : BasePlugin
{
    private void AddTeamScore(CsTeam playerTeam, int score)
    {
        var teams = Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager");

        foreach (var team in teams)
        {
            if ((int) playerTeam != team.TeamNum) continue;
            team.Score += 1;
            Utilities.SetStateChanged(team, "CTeam", "m_iScore");
        }
    }
    
    public void CheckScorelimit()
    {
        var teams = Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager");
        var restartTime = 30f;
        
        foreach (var team in teams)
        {
            if (team.Score >= Scorelimit.Value)
            {
                PrintToAllCenter(Localizer["restarting_in", restartTime]);
        
                var winPanelEvent = new EventCsWinPanelMatch(true);
                winPanelEvent.FireEvent(false);
                
                var endMatchRestartEvent = new EventCsMatchEndRestart(true);

                AddTimer(restartTime, () =>
                {
                    endMatchRestartEvent.FireEvent(false);
                    Server.ExecuteCommand("map " + Server.MapName);
                });
                
            }
        }
    }

    public void AddMvp(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return;
        
        player.MVPs += 1;
                
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iMVPs");
    }

    public void PrintToAllCenter(string message)
    {
        foreach (CCSPlayerController player in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false }))
        {
            player.PrintToCenter(message);
        }
    }
        
    private float VectorDistance(Vector a, Vector b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private bool VectorEquals(Vector? a, Vector? b)
    {    
        if (a is null || b is null) return false;
        return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    }


}