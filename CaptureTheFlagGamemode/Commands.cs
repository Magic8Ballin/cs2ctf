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
        
        // Alias commands to use for easier
        AddCommand("ctf_editor", "Alias function for the same css_* function", OnEditor);
        AddCommand("ctf_addtbase", "Alias function for the same css_* function", OnAddTBase);
        AddCommand("ctf_addctbase", "Alias function for the same css_* function", OnAddCTBase);
    }
}