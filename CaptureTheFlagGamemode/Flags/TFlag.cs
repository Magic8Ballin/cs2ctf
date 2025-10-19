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
        CaptureTheFlag.Instance.PrintToAllCenter(CaptureTheFlag.Instance.Localizer["dropped_t_flag", Carrier!.PlayerName]);
        
        base.Drop(position, angle);
        
        if (!CaptureTheFlag.Instance.FlagReturnOnTouch.Value)
        {
            CaptureTheFlag.Instance.AddTimer(CaptureTheFlag.Instance.FlagReturnDelay.Value, Return);
        }
    }
    
    public new void Pickup(CCSPlayerController? player)
    {
        base.Pickup(player);

        if (Carrier != null)
        {
            CaptureTheFlag.Instance.PrintToAllCenter(CaptureTheFlag.Instance.Localizer["pickedup_t_flag", Carrier!.PlayerName]);
        }
    }
    
    public new void Return()
    {
        CaptureTheFlag.Instance.PrintToAllCenter(CaptureTheFlag.Instance.Localizer["t_flag_returned"]);
        
        base.Return();
    }
    
    public new void Secure(CCSPlayerController? player)
    {
        CaptureTheFlag.Instance.PrintToAllCenter(CaptureTheFlag.Instance.Localizer["secured_t_flag", Carrier!.PlayerName]);
        
        base.Secure(player);
    }
}