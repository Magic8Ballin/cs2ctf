using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace CaptureTheFlagGamemode;

public partial class CaptureTheFlag : BasePlugin
{
    private void RegisterCommands()
    {
        AddCommand("killme", "Kill myself for respawn test", OnKillme);
        
        AddCommand("css_ctf_editor", "Toggles the CTF editor to adjust spawns", OnEditor);
        AddCommand("css_ctf_addtbase", "Sets the CTF home base for terrorists", OnAddTBase);
        AddCommand("css_ctf_addctbase", "Sets the CTF home base for counter terrorists", OnAddCTBase);
        AddCommand("css_ctf", "Toggles the CTF game mode. Requires a map restart to take effect.", ToggleGamemode);
        AddCommand("css_ctfmap", "Changes a map with CTF game mode enabled", ChangeMapCtf);
        
        // Alias commands to use for easier access in console
        AddCommand("ctf_editor", "Toggles the CTF editor to adjust spawns", OnEditor);
        AddCommand("ctf_addtbase", "Sets the CTF home base for terrorists", OnAddTBase);
        AddCommand("ctf_addctbase", "Sets the CTF home base for counter terrorists", OnAddCTBase);
        AddCommand("ctf", "Toggles the CTF game mode. Requires a map restart to take effect.", ToggleGamemode);
        AddCommand("ctfmap", "Changes a map with CTF game mode enabled", ChangeMapCtf);
    }
}