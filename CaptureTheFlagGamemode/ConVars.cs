using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;

namespace CaptureTheFlagGamemode;

public partial class CaptureTheFlag : BasePlugin
{
    public FakeConVar<int> Scorelimit  = new("mp_scorelimit_ctf", "The amount of flags a team has to secure before the round ends", 2);
    
    public FakeConVar<int> RespawnDelay  = new("mp_respawn_delay_ctf", "The amount of seconds before a player respawns after being killed", 2);

    public FakeConVar<bool> FlagReturnOnTouch = new("mp_flag_return_on_touch", "Defines if the flag is instantly brought back after being touched by the same team", false);
    
    public FakeConVar<int> FlagReturnDelay  = new("mp_flag_return_delay", "If return to touch is disabled, wait this amount of seconds to teleport the flag back to base", 5);
    
    public FakeConVar<bool> FlagBaseHasBeam = new("mp_flag_base_has_beam", "Defines if the flag base does have a beam by default (can be deactivated on maps with proper flag platforms)", true);
    
    public FakeConVar<bool> Enabled = new("mp_ctf_enabled", "Whether or not the CTF mode should be enabled", false);

    // Per-map base positions/orientations (empty by default)
    public FakeConVar<string> CtBasePos  = new("mp_ctf_ct_base_pos", "CT base position as \"x y z\"", "");
    public FakeConVar<string> CtBaseAng  = new("mp_ctf_ct_base_ang", "CT base orientation as \"pitch yaw roll\"", "");
    public FakeConVar<string> TBasePos   = new("mp_ctf_t_base_pos",  "T base position as \"x y z\"", "");
    public FakeConVar<string> TBaseAng   = new("mp_ctf_t_base_ang",  "T base orientation as \"pitch yaw roll\"", "");
}