using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace CaptureTheFlagGamemode.Flags;

public class TFlag : BaseFlag
{
    protected override int[] BaseBeamColor { get; set; } = [30, 197, 180, 121];
    
    public override string Model { get; set; } = "models/ctf/ctf_flag_t.vmdl";
    
    public override CsTeam Team { get; set; } = CsTeam.Terrorist;
    
    public new void Drop(Vector position, QAngle? angle = null)
    {
        base.Drop(position, angle);
        
        Server.PrintToChatAll(CaptureTheFlag.Instance.Localizer["dropped_t_flag", Carrier!.PlayerName]);
    }
    
    public new void Secure(CCSPlayerController? player)
    {
        base.Secure(player);
        
        Server.PrintToChatAll(CaptureTheFlag.Instance.Localizer["secured_t_flag", Carrier!.PlayerName]);
    }
}