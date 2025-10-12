using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace CaptureTheFlagGamemode.Flags;

public class CtFlag : BaseFlag
{
    protected override int[] BaseBeamColor { get; set; } = [60, 80, 103, 124];
    
    public override string Model { get; set; } = "models/ctf/ctf_flag_ct.vmdl";

    public override CsTeam Team { get; set; } = CsTeam.CounterTerrorist;

    public new void Drop(Vector position, QAngle? angle = null)
    {
        base.Drop(position, angle);
        
        Server.PrintToChatAll(CaptureTheFlag.Instance.Localizer["dropped_ct_flag", Carrier!.PlayerName]);
    }
    
    public new void Secure(CCSPlayerController? player)
    {
        base.Secure(player);
        
        Server.PrintToChatAll(CaptureTheFlag.Instance.Localizer["secured_ct_flag", Carrier!.PlayerName]);
    }
}