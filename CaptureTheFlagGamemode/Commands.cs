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

        // Save/clear/load/start (console + chat)
        AddCommand("css_ctf_save", "Saves CTF bases for the current map", OnSaveCtf);
        AddCommand("css_ctf_clear", "Clears saved CTF config for the current map", OnClearCtf);
        AddCommand("css_ctf_load", "Loads CTF config for the current map", OnLoadCtf);
        AddCommand("css_ctf_start", "Starts the CTF game loop", OnStartCtf);
        
        // Aliases for easier access
        AddCommand("ctf_editor", "Toggles the CTF editor to adjust spawns", OnEditor);
        AddCommand("ctf_addtbase", "Sets the CTF home base for terrorists", OnAddTBase);
        AddCommand("ctf_addctbase", "Sets the CTF home base for counter terrorists", OnAddCTBase);
        AddCommand("ctf", "Toggles the CTF game mode. Requires a map restart to take effect.", ToggleGamemode);
        AddCommand("ctfmap", "Changes a map with CTF game mode enabled", ChangeMapCtf);
        AddCommand("ctf_save", "Saves CTF bases for the current map", OnSaveCtf);
        AddCommand("ctf_clear", "Clears saved CTF config for the current map", OnClearCtf);
        AddCommand("ctf_load", "Loads CTF config for the current map", OnLoadCtf);
        AddCommand("ctf_start", "Starts the CTF game loop", OnStartCtf);

        // Entity-delivery config commands (chat + console)
        AddCommand("css_ctf_mode", "Sets delivery mode: radius|entity", OnCmdSetDeliveryMode);
        AddCommand("css_ctf_ct_ent", "Sets CT delivery trigger targetname", OnCmdSetCtDeliveryEnt);
        AddCommand("css_ctf_t_ent", "Sets T delivery trigger targetname", OnCmdSetTDeliveryEnt);
        AddCommand("css_ctf_delivery_missing", "Sets missing-trigger behavior: radius|block", OnCmdSetDeliveryMissing);
        AddCommand("css_ctf_require_home_flag", "Sets require-home-flag: 0|1", OnCmdSetRequireHomeFlag);

        AddCommand("ctf_mode", "Sets delivery mode: radius|entity", OnCmdSetDeliveryMode);
        AddCommand("ctf_ct_ent", "Sets CT delivery trigger targetname", OnCmdSetCtDeliveryEnt);
        AddCommand("ctf_t_ent", "Sets T delivery trigger targetname", OnCmdSetTDeliveryEnt);
        AddCommand("ctf_delivery_missing", "Sets missing-trigger behavior: radius|block", OnCmdSetDeliveryMissing);
        AddCommand("ctf_require_home_flag", "Sets require-home-flag: 0|1", OnCmdSetRequireHomeFlag);
    }
}