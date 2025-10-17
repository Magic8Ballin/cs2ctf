using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
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

        foreach (var team in teams)
        {
            if (team.Score >= Scorelimit.Value)
            {
                foreach (CCSPlayerController player in Utilities.GetPlayers())
                {
                    if (team.TeamNum == (int) CsTeam.Terrorist)
                    {
                        Server.PrintToChatAll(Localizer["t_won"]);
                    }
                    else
                    {
                        Server.PrintToChatAll(Localizer["ct_won"]);
                    }
                }
                
                Server.ExecuteCommand("mp_restartgame 5");
            }
        }
    }


    private void AddMvp(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return;
        
        player.MVPs += 1;
                
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iMVPs");
    }
}