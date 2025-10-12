using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;

namespace CaptureTheFlagGamemode;

public partial class CaptureTheFlag : BasePlugin
{
    public FakeConVar<int> Scorelimit  = new("mp_scorelimit_ctf", "The amount of flags a team has to secure before the round ends", 1);
    
    public FakeConVar<int> RespawnDelay  = new("mp_respawn_delay_ctf", "The amount of seconds before a player respawns after being killed", 3);

    public FakeConVar<bool> FlagReturnOnTouch = new("mp_flag_return_on_touch", "Defines if the flag is instantly brought back after being touched by the same team", false);
    
    public FakeConVar<int> FlagReturnDelay  = new("mp_flag_return_delay", "If return to touch is disabled, wait this amount of seconds to teleport the flag back to base", 8);
    
    public FakeConVar<bool> FlagBaseHasBeam = new("mp_flag_base_has_beam", "Defines if the flag base does have a beam by default (can be deactivated on maps with proper flag platforms)", true);
}