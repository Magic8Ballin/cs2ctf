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
        CaptureTheFlag.Instance.PrintToAllCenter(CaptureTheFlag.Instance.Localizer["dropped_ct_flag", Carrier!.PlayerName]);
        
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
            CaptureTheFlag.Instance.PrintToAllCenter(CaptureTheFlag.Instance.Localizer["pickedup_ct_flag", Carrier!.PlayerName]);
        }
    }

    public new void Return()
    {
        CaptureTheFlag.Instance.PrintToAllCenter(CaptureTheFlag.Instance.Localizer["ct_flag_returned"]);
        
        base.Return();
    }
    
    public new void Secure(CCSPlayerController? player)
    {
        CaptureTheFlag.Instance.PrintToAllCenter(CaptureTheFlag.Instance.Localizer["secured_ct_flag", Carrier!.PlayerName]);
        
        base.Secure(player);
    }
}