using System;
using System.Globalization;
using System.IO;
using System.Text;
using CaptureTheFlagGamemode.Flags;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace CaptureTheFlagGamemode;

public partial class CaptureTheFlag : BasePlugin
{
    public override string ModuleName => "Capture The Flag";
    public override string ModuleAuthor => "Astinox";
    public override string ModuleVersion => "0.5.1";
    public override string ModuleDescription => "Adds the Capture the Flag game mode to Counter Strike 2";
    
    private readonly TFlag _tFlag = new();
    private readonly CtFlag _ctFlag = new();

    private Dictionary<CCSPlayerController, Timer> _respawnTimers = new();
    private Dictionary<CCSPlayerController, int> _respawnTimes = new();

    private bool _isEditing = false;

    private Timer? _gameObjectiveTimer;

    // Remember last angles used when setting bases (for saving)
    private QAngle? _ctBaseAngle;
    private QAngle? _tBaseAngle;

    // ----- Entity delivery mode settings (convars without mp_) -----
    public FakeConVar<string> CtfDeliveryMode = new("ctf_delivery_mode", "Delivery mode: radius or entity", "radius");
    public FakeConVar<string> CtfCtDeliveryEnt = new("ctf_ct_delivery_ent", "CT delivery trigger targetname", "deliver_ct");
    public FakeConVar<string> CtfTDeliveryEnt  = new("ctf_t_delivery_ent",  "T delivery trigger targetname", "deliver_t");
    public FakeConVar<string> CtfDeliveryMissing = new("ctf_delivery_missing", "Behavior if delivery triggers are missing: radius | block", "radius");
    public FakeConVar<int> CtfRequireHomeFlag = new("ctf_require_home_flag", "Require your flag to be home to capture (0/1)", 0);

    // NEW: pickup hold requirement (seconds)
    public FakeConVar<float> CtfPickupHoldSeconds = new("ctf_pickup_hold_seconds", "Seconds required to be inside the enemy flag radius to pick it up", 3.0f);

    // NEW: optional toggle to bypass name matching while testing
    public FakeConVar<bool> CtfDeliveryStrictNames = new("ctf_delivery_strict_names", "Require trigger names to match deliver_ct/deliver_t (1=strict, 0=any trigger_multiple)", true);

    // NEW: sound event for capture start (defuse-like beep). Empty to disable.
    public FakeConVar<string> CtfPickupStartSound = new("ctf_pickup_start_sound", "Sound event played when starting flag capture (e.g., C4.DisarmStart). Empty to disable.", "C4.DisarmStart");

    private bool _useEntityDelivery = false;
    private string _ctDeliveryName = "deliver_ct";
    private string _tDeliveryName = "deliver_t";
    private string _deliveryMissingBehavior = "radius";
    
    public static CaptureTheFlag Instance { get; private set; } = null!;

    // --- Hold-to-pickup channel state ---
    private sealed class PickupChannel
    {
        public BaseFlag Flag;
        public Vector FlagPosSnapshot;
        public float Accumulated;
        public float HudCooldown;
        public PickupChannel(BaseFlag flag, Vector posSnap)
        {
            Flag = flag;
            FlagPosSnapshot = posSnap;
            Accumulated = 0f;
            HudCooldown = 0f;
        }
    }

    private readonly Dictionary<CCSPlayerController, PickupChannel> _pickupChannels = new();

    private void StartPickupChannel(CCSPlayerController player, BaseFlag flag)
    {
        if (!player.IsValid || player.PlayerPawn.Value is null || flag.Position is null)
            return;

        // If already channeling this flag, do nothing
        if (_pickupChannels.TryGetValue(player, out var existing) && ReferenceEquals(existing.Flag, flag))
            return;

        // Replace any existing channel (race allowed per player only)
        var posSnap = flag.Position;
        if (posSnap is null) return;
        _pickupChannels[player] = new PickupChannel(flag, posSnap);

        // Beep on start: add a sound emit here if you have a known-safe UI beep event on clients
        TryPlayStartBeep(player);
        player.PrintToCenter("[CTF] Capturing flag...");
    }

    private void CancelPickupChannel(CCSPlayerController? player, string? reason = null)
    {
        if (player is null) return;
        if (_pickupChannels.Remove(player) && player.IsValid && !string.IsNullOrEmpty(reason))
            player.PrintToCenter(reason);
    }

    // Update: countdown HUD now updates every 0.1s
    private void TickPickupChannels(float deltaSeconds = 0.1f)
    {
        if (_pickupChannels.Count == 0)
            return;

        var toClear = new List<CCSPlayerController>(_pickupChannels.Count);
        foreach (var kvp in _pickupChannels)
        {
            var player = kvp.Key;
            var ch = kvp.Value;

            if (player is null || !player.IsValid || !player.PawnIsAlive || player.PlayerPawn.Value is null)
            {
                toClear.Add(player!);
                continue;
            }

            // Flag must still be at the same place (not picked/returned/moved)
            var flagPos = ch.Flag.Position;
            if (flagPos is null || !VectorEquals(flagPos, ch.FlagPosSnapshot))
            {
                player.PrintToCenter("[CTF] Capture cancelled.");
                toClear.Add(player!);
                continue;
            }

            var pawn = player.PlayerPawn.Value;
            var playerPos = pawn.AbsOrigin;
            if (playerPos is null)
            {
                toClear.Add(player!);
                continue;
            }

            // Must stay inside flag radius continuously
            var radius = ch.Flag.FlagRadius;
            if (VectorDistance(playerPos!, flagPos!) > radius)
            {
                player.PrintToCenter("[CTF] Capture cancelled.");
                toClear.Add(player!);
                continue;
            }

            // Progress channel
            ch.Accumulated += deltaSeconds;

            // HUD every ~0.1s
            ch.HudCooldown -= deltaSeconds;
            if (ch.HudCooldown <= 0f)
            {
                var need = MathF.Max(0.1f, CtfPickupHoldSeconds.Value);
                var remain = MathF.Max(0f, need - ch.Accumulated);
                player.PrintToCenter($"[CTF] Capturing flag... {remain:0.0}s");
                ch.HudCooldown = 0.1f;
            }

            // Complete
            var required = MathF.Max(0.1f, CtfPickupHoldSeconds.Value);
            if (ch.Accumulated >= required)
            {
                if (ch.Flag is TFlag)
                    _tFlag.Pickup(player);
                else if (ch.Flag is CtFlag)
                    _ctFlag.Pickup(player);

                toClear.Add(player!);
            }
        }

        foreach (var p in toClear)
            _pickupChannels.Remove(p);
    }

    public override void Load(bool hotReload)
    {
        Instance = this;
            
        // We need these even if CTF is disabled
        RegisterCommands();
        RegisterListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
        RegisterFakeConVars(typeof(FakeConVar<>));
        
        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            // If for whatever reason we have old entities left, remove them
            _tFlag?.RemoveEntities();
            _ctFlag?.RemoveEntities();
            
            // If CTF is disabled, skip these steps
            if (!Enabled.Value) return;
            
            // Execute this with a slight delay to execute it after the normal configs have been loaded
            AddTimer(0.5f, () => Server.ExecuteCommand("exec gamemode_ctf"));

            // Then exec map-specific CTF cfg if present, and apply bases from convars
            AddTimer(0.6f, () => Server.ExecuteCommand($"exec {GetMapCfgExecPath()}"));
            AddTimer(0.7f, ApplyBasesFromConVars);
            
            // CTF objective position tracking loop
            _gameObjectiveTimer = AddTimer(0.1f, CheckIfPlayersEnterObjectives, TimerFlags.REPEAT);
            
            Console.WriteLine("Capture The Flag initialized");
        });
        
        RegisterListener<Listeners.OnMapEnd>(() =>
        {
            _tFlag?.RemoveEntities();
            _ctFlag?.RemoveEntities();
            _gameObjectiveTimer?.Kill();
        });
    }

    public void Unload()
    {
        _gameObjectiveTimer?.Kill();
        _tFlag.RemoveEntities();
        _ctFlag.RemoveEntities();
    }

    private void OnEditor(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid) return;
        
        if (_isEditing == false)
        {
            _isEditing = true;
            player.PrintToCenter(Localizer["editor_enabled"]);
        }
        else
        {
            _isEditing = false;
            player.PrintToCenter(Localizer["editor_disabled"]);
        }
    }

    private void ToggleGamemode(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid) return;

        if (Enabled.Value)
        {
            Server.ExecuteCommand("mp_ctf_enabled 0");
            player.PrintToCenter(Localizer["ctf_disabled"]);
        }
        else
        {
            Server.ExecuteCommand("mp_ctf_enabled 1");
            player.PrintToCenter(Localizer["ctf_enabled"]);
        }
    }

    private void ChangeMapCtf(CCSPlayerController? player, CommandInfo command)
    {
        // If no map was provided, we reload the current map with CTF enabled
        var map = Server.MapName;
        
        if (command.ArgCount > 1)
        {
            map = command.ArgByIndex(1);
        }
        
        Server.ExecuteCommand("mp_ctf_enabled 1");
        Server.ExecuteCommand("map " + map);
    }
    
    private void OnServerPrecacheResources(ResourceManifest manifest)
    {
        manifest.AddResource("models/ctf/ctf_flag_t.vmdl");
        manifest.AddResource("models/ctf/ctf_flag_ct.vmdl");

        manifest.AddResource("soundevents/soundevents_ctf_gamemode.vsndevts");
    }
    
    private static void OnKillme(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;
        player.CommitSuicide(true,true);
    }
    
    private void OnAddCTBase(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || player.PlayerPawn.Value == null || !player.PlayerPawn.Value.IsValid) return;
        
        CCSPlayerPawn playerPawn = player.PlayerPawn.Value;
        if (playerPawn.AbsOrigin is null) return;

        _ctBaseAngle = playerPawn.EyeAngles;
        _ctFlag.SetBase(playerPawn.AbsOrigin, _ctBaseAngle);
    }
    
    private void OnAddTBase(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || player.PlayerPawn.Value == null || !player.PlayerPawn.Value.IsValid) return;
        
        CCSPlayerPawn playerPawn = player.PlayerPawn.Value;
        if (playerPawn.AbsOrigin is null) return;

        _tBaseAngle = playerPawn.EyeAngles;
        _tFlag.SetBase(playerPawn.AbsOrigin, _tBaseAngle);
    }

    // Explicit save command: writes cfg/ctf/<map>.cfg with only what's set; overwrites existing file.
    [ConsoleCommand("ctf_save")]
    private void OnSaveCtf(CCSPlayerController? player, CommandInfo command)
    {
        try
        {
            var filePath = GetMapCfgFilePath();
            var fullPath = Path.GetFullPath(filePath);
            var dir = Path.GetDirectoryName(fullPath)!;
            Directory.CreateDirectory(dir);

            var sb = new StringBuilder();

            // Only write entries that are set
            if (_ctFlag.BasePosition is not null && _ctBaseAngle is not null)
            {
                sb.AppendLine($@"mp_ctf_ct_base_pos ""{FormatVector(_ctFlag.BasePosition!)}""");
                sb.AppendLine($@"mp_ctf_ct_base_ang ""{FormatAngle(_ctBaseAngle)}""");
            }

            if (_tFlag.BasePosition is not null && _tBaseAngle is not null)
            {
                sb.AppendLine($@"mp_ctf_t_base_pos ""{FormatVector(_tFlag.BasePosition!)}""");
                sb.AppendLine($@"mp_ctf_t_base_ang ""{FormatAngle(_tBaseAngle)}""");
            }

            // Persist entity delivery settings if they differ from defaults
            if (!string.Equals(CtfDeliveryMode.Value, "radius", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($@"ctf_delivery_mode ""{CtfDeliveryMode.Value}""");
            if (!string.Equals(CtfCtDeliveryEnt.Value, "deliver_ct", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($@"ctf_ct_delivery_ent ""{CtfCtDeliveryEnt.Value}""");
            if (!string.Equals(CtfTDeliveryEnt.Value, "deliver_t", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($@"ctf_t_delivery_ent ""{CtfTDeliveryEnt.Value}""");
            if (!string.Equals(CtfDeliveryMissing.Value, "radius", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($@"ctf_delivery_missing ""{CtfDeliveryMissing.Value}""");
            if (CtfRequireHomeFlag.Value != 0)
                sb.AppendLine($@"ctf_require_home_flag ""{CtfRequireHomeFlag.Value}""");

            if (sb.Length == 0)
            {
                if (player != null)
                    player.PrintToChat("[CTF] Nothing to save. Set at least one base.");
                Console.WriteLine("[CTF] ctf_save: nothing to save (no bases set).");
                Console.WriteLine($"[CTF] Working dir: {Directory.GetCurrentDirectory()}");
                Console.WriteLine($"[CTF] Intended path: {fullPath}");
                return;
            }

            Console.WriteLine($"[CTF] Saving to: {fullPath}");
            Console.WriteLine($"[CTF] Working dir: {Directory.GetCurrentDirectory()}");

            File.WriteAllText(fullPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            // Update convars to reflect latest saved values
            if (_ctFlag.BasePosition is not null && _ctBaseAngle is not null)
            {
                CtBasePos.Value = FormatVector(_ctFlag.BasePosition!);
                CtBaseAng.Value = FormatAngle(_ctBaseAngle);
            }
            if (_tFlag.BasePosition is not null && _tBaseAngle is not null)
            {
                TBasePos.Value = FormatVector(_tFlag.BasePosition!);
                TBaseAng.Value = FormatAngle(_tBaseAngle);
            }

            if (player != null)
                player.PrintToChat($"[CTF] Saved CTF config for map '{Server.MapName}' to ctf/{Server.MapName}.cfg");
            Console.WriteLine($"[CTF] Saved CTF config to {fullPath}");
        }
        catch (Exception ex)
        {
            if (player != null)
                player.PrintToChat("[CTF] Error saving config. See server console for details.");
            Console.WriteLine($"[CTF] Error saving config for map '{Server.MapName}': {ex}");
        }
    }

    // Explicit clear command: deletes cfg/ctf/<map>.cfg and clears the four convars.
    [ConsoleCommand("ctf_clear")]
    private void OnClearCtf(CCSPlayerController? player, CommandInfo command)
    {
        try
        {
            var filePath = GetMapCfgFilePath();
            bool deleted = false;

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                deleted = true;
            }

            CtBasePos.Value = "";
            CtBaseAng.Value = "";
            TBasePos.Value = "";
            TBaseAng.Value = "";

            if (player != null)
                player.PrintToChat(deleted
                    ? $"[CTF] Cleared saved config for '{Server.MapName}'."
                    : $"[CTF] No saved config found for '{Server.MapName}'. ConVars cleared.");

            Console.WriteLine(deleted
                ? $"[CTF] Deleted {filePath} and cleared ConVars."
                : $"[CTF] No file at {filePath}. Cleared ConVars.");
        }
        catch (Exception ex)
        {
            if (player != null)
                player.PrintToChat("[CTF] Error clearing config. See server console for details.");
            Console.WriteLine($"[CTF] Error clearing config for map '{Server.MapName}': {ex}");
        }
    }
    
    public void OnPlayerTouchTFlag(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return;
        if (!player.PawnIsAlive) return;

        // Same-team return behavior unchanged
        if (FlagReturnOnTouch.Value && !VectorEquals(_tFlag.BasePosition, _tFlag.Position) && player.Team == CsTeam.Terrorist)
        {
            _tFlag.Return();
        }
        
        // Enemy must hold inside radius to pick up
        if (player.Team == CsTeam.Terrorist) return; // T can't pick up T flag
        StartPickupChannel(player, _tFlag);
    }
    
    public void OnPlayerTouchCTFlag(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return;
        if (!player.PawnIsAlive) return;
        
        // Same-team return behavior unchanged
        if (FlagReturnOnTouch.Value && !VectorEquals(_ctFlag.BasePosition, _ctFlag.Position) && player.Team == CsTeam.CounterTerrorist)
        {
            _ctFlag.Return();
        }
        
        // Enemy must hold inside radius to pick up
        if (player.Team == CsTeam.CounterTerrorist) return; // CT can't pick up CT flag
        StartPickupChannel(player, _ctFlag);
    }
    
    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player == null || !player.IsValid) return HookResult.Continue;

        _respawnTimers.Add(player,
            AddTimer(1.0f, () => HandleRespawnTimer(player), TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE));
        _respawnTimes.Add(player, RespawnDelay.Value);

        player.InGameMoneyServices!.Account = 16000;

        // Cancel capture channel on death
        CancelPickupChannel(player);
        
        if(_tFlag.Carrier == null && _ctFlag.Carrier == null) return HookResult.Continue;

        if (player.UserId == _ctFlag.Carrier?.UserId)
        {
            Vector deathPosition = player.PlayerPawn.Value?.AbsOrigin!;
            _ctFlag.Drop(new Vector(deathPosition.X + 1f, deathPosition.Y, deathPosition.Z), player.PlayerPawn.Value?.EyeAngles);
        }
        
        if (player.UserId == _tFlag.Carrier?.UserId)
        {
            Vector deathPosition = player.PlayerPawn.Value?.AbsOrigin!;
            _tFlag.Drop(new Vector(deathPosition.X + 1f, deathPosition.Y, deathPosition.Z), player.PlayerPawn.Value?.EyeAngles);
        }

        return HookResult.Continue;
    }
    
    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player == null || !player.IsValid) return HookResult.Continue;

        if (_respawnTimers.ContainsKey(player) && _respawnTimes.ContainsKey(player))
        {
            _respawnTimers.Remove(player);
            _respawnTimes.Remove(player);
        }

        // Cancel any active pickup channel
        CancelPickupChannel(player);

        return HookResult.Continue;
    }
        
    [GameEventHandler]
    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player == null || !player.IsValid) return HookResult.Continue;

        if(!@event.Disconnect)
        {
            // Cancel pickup channel on team change
            CancelPickupChannel(player);
            player.Respawn();
        }

        return HookResult.Continue;
    }

    public void OnPlayerEnterCtBase(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return;

        // In entity delivery mode, radius capture is disabled
        if (_useEntityDelivery) return;

        if (player.Team == CsTeam.Terrorist) return;

        if (!_tFlag.IsTaken()) return;
        
        if (_tFlag.Carrier?.UserId != player.UserId) return;

        // Optional: if require-home-flag is enabled, block if our flag is stolen
        if (CtfRequireHomeFlag.Value != 0 && _ctFlag.IsTaken())
        {
            player.PrintToCenter("[CTF] Your flag must be home to capture.");
            return;
        }

        _tFlag.Secure(player);
        
        AddMvp(player);
        
        AddTeamScore(player.Team, 1);

        CheckScorelimit();
    }
    
    public void OnPlayerEnterTBase(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return;

        // In entity delivery mode, radius capture is disabled
        if (_useEntityDelivery) return;

        if (player.Team == CsTeam.CounterTerrorist) return;
        
        if (!_ctFlag.IsTaken()) return;

        if (_ctFlag.Carrier?.UserId != player.UserId) return;

        // Optional: if require-home-flag is enabled, block if our flag is stolen
        if (CtfRequireHomeFlag.Value != 0 && _tFlag.IsTaken())
        {
            player.PrintToCenter("[CTF] Your flag must be home to capture.");
            return;
        }
        
        _ctFlag.Secure(player);

        AddMvp(player);
        
        AddTeamScore(player.Team, 1);

        CheckScorelimit();
    }

    private void CheckIfPlayersEnterObjectives()
    {
        // If somebody enabled the server-wide editor, disregard all CTF objective events
        if (IsInEditor()) return;

        // Advance any active pickup channels (0.1s tick)
        TickPickupChannels(0.1f);
            
        foreach (CCSPlayerController player in Utilities.GetPlayers())
        {
            if (!player.IsValid || player.PlayerPawn.Value == null)
                continue;

            var pawn = player.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid)
                continue;

            var playerPos = pawn.AbsOrigin;
            if (playerPos == null) continue;

            // In entity delivery mode, we don't do base radius checks
            if (!_useEntityDelivery)
            {
                // Nearby CT base
                if (_ctFlag.BasePosition != null)
                {
                    if (VectorDistance(playerPos, _ctFlag.BasePosition) <= _ctFlag.BaseRadius)
                    {
                        OnPlayerEnterCtBase(player);
                    }
                }
                
                // Nearby T base
                if (_tFlag.BasePosition != null)
                {
                    if (VectorDistance(playerPos, _tFlag.BasePosition) <= _tFlag.BaseRadius)
                    {
                        OnPlayerEnterTBase(player);
                    }
                }
            }
            
            // Nearby T flag
            if (_tFlag.Position != null)
            {
                if (VectorDistance(playerPos, _tFlag.Position) <= _tFlag.FlagRadius)
                {
                    OnPlayerTouchTFlag(player);
                }
            }
            
            // Nearby CT flag
            if (_ctFlag.Position != null)
            {
                if (VectorDistance(playerPos, _ctFlag.Position) <= _ctFlag.FlagRadius)
                {
                    OnPlayerTouchCTFlag(player);
                }
            }
        }
    }
    
    private void HandleRespawnTimer(CCSPlayerController player)
    {
        if (!_respawnTimers.TryGetValue(player, out Timer? timer))
            return;
        
        if (!player.IsValid)
        {
            timer.Kill();
            return;
        }

        if (_respawnTimes[player] > 0)
        {
            player.PrintToCenter(_respawnTimes[player] > 1
                ? Localizer["respawning_seconds", _respawnTimes[player]]
                : Localizer["respawning_second", _respawnTimes[player]]);
            
            _respawnTimes[player]--;
        }
        else
        {
            _respawnTimers[player].Kill();

            if (_respawnTimers.ContainsKey(player) && _respawnTimes.ContainsKey(player))
            {
                _respawnTimers.Remove(player);
                _respawnTimes.Remove(player);   
            }
            
            player.Respawn();
            player.PrintToCenter(Localizer["respawned"]);
        }
    }
    
    private bool IsInEditor()
    {
        return _isEditing;
    }

    // --- Entity delivery via trigger_multiple: OnTrigger output hook ---
    // Helper: read the trigger's targetname (Hammer name) from identity; fallback to designer name
    private static string GetTriggerTargetName(CEntityInstance? ent)
    {
        try
        {
            var name = ent?.Entity?.Name;
            if (!string.IsNullOrWhiteSpace(name))
                return name!;
        }
        catch
        {
            // ignore
        }

        // Fallback: class designer name (e.g., "trigger_multiple")
        return ent?.DesignerName ?? string.Empty;
    }

    // Common handler
    private HookResult TryHandleDelivery(CEntityInstance? activator, CEntityInstance? caller, string srcOutput)
    {
        if (!_useEntityDelivery || IsInEditor()) return HookResult.Continue;

        // Try resolve controller (preferred path)
        var controller = activator?.As<CCSPlayerController>()
                        ?? activator?.As<CCSPlayerPawn>()?.Controller?.Value?.As<CCSPlayerController>()
                        ?? activator?.As<CCSPlayerPawn>()?.OriginalController?.Value?.As<CCSPlayerController>();

        // Resolve the trigger name properly
        var triggerName = GetTriggerTargetName(caller);

        if (controller == null || !controller.IsValid || !controller.PawnIsAlive)
        {
            // If strict naming is enabled, only react to our delivery triggers
            if (CtfDeliveryStrictNames.Value)
            {
                if (!triggerName.Equals(_ctDeliveryName, StringComparison.OrdinalIgnoreCase) &&
                    !triggerName.Equals(_tDeliveryName, StringComparison.OrdinalIgnoreCase))
                    return HookResult.Continue;
            }

            // deliver_ct => CT zone (CT carrying T flag)
            if (triggerName.Equals(_ctDeliveryName, StringComparison.OrdinalIgnoreCase) || !CtfDeliveryStrictNames.Value)
            {
                var ctCarrier = _tFlag.Carrier; // CT carrier when T flag is taken
                if (ctCarrier != null && _tFlag.IsTaken())
                {
                    if (CtfRequireHomeFlag.Value != 0 && _ctFlag.IsTaken())
                    {
                        ctCarrier.PrintToCenter("[CTF] Your flag must be home to capture.");
                        return HookResult.Continue;
                    }

                    _tFlag.Secure(ctCarrier);
                    AddMvp(ctCarrier);
                    AddTeamScore(ctCarrier.Team, 1);
                    CheckScorelimit();
                    return HookResult.Continue;
                }
            }

            // deliver_t => T zone (T carrying CT flag)
            if (triggerName.Equals(_tDeliveryName, StringComparison.OrdinalIgnoreCase) || !CtfDeliveryStrictNames.Value)
            {
                var tCarrier = _ctFlag.Carrier; // T carrier when CT flag is taken
                if (tCarrier != null && _ctFlag.IsTaken())
                {
                    if (CtfRequireHomeFlag.Value != 0 && _tFlag.IsTaken())
                    {
                        tCarrier.PrintToCenter("[CTF] Your flag must be home to capture.");
                        return HookResult.Continue;
                    }

                    _ctFlag.Secure(tCarrier);
                    AddMvp(tCarrier);
                    AddTeamScore(tCarrier.Team, 1);
                    CheckScorelimit();
                    return HookResult.Continue;
                }
            }

            return HookResult.Continue;
        }

        if (CtfDeliveryStrictNames.Value)
        {
            if (!triggerName.Equals(_ctDeliveryName, StringComparison.OrdinalIgnoreCase) &&
                !triggerName.Equals(_tDeliveryName, StringComparison.OrdinalIgnoreCase))
                return HookResult.Continue;
        }

        if (controller.Team == CsTeam.CounterTerrorist &&
            (!CtfDeliveryStrictNames.Value || triggerName.Equals(_ctDeliveryName, StringComparison.OrdinalIgnoreCase)))
        {
            if (_tFlag.IsTaken() && _tFlag.Carrier?.UserId == controller.UserId)
            {
                if (CtfRequireHomeFlag.Value != 0 && _ctFlag.IsTaken())
                {
                    controller.PrintToCenter("[CTF] Your flag must be home to capture.");
                    return HookResult.Continue;
                }
                _tFlag.Secure(controller);
                AddMvp(controller);
                AddTeamScore(controller.Team, 1);
                CheckScorelimit();
            }
            return HookResult.Continue;
        }

        if (controller.Team == CsTeam.Terrorist &&
            (!CtfDeliveryStrictNames.Value || triggerName.Equals(_tDeliveryName, StringComparison.OrdinalIgnoreCase)))
        {
            if (_ctFlag.IsTaken() && _ctFlag.Carrier?.UserId == controller.UserId)
            {
                if (CtfRequireHomeFlag.Value != 0 && _tFlag.IsTaken())
                {
                    controller.PrintToCenter("[CTF] Your flag must be home to capture.");
                    return HookResult.Continue;
                }
                _ctFlag.Secure(controller);
                AddMvp(controller);
                AddTeamScore(controller.Team, 1);
                CheckScorelimit();
            }
            return HookResult.Continue;
        }

        return HookResult.Continue;
    }

    // Existing hook now logs + delegates
    [EntityOutputHook("trigger_multiple", "OnTrigger")]
    public HookResult OnDeliveryTrigger(CEntityIOOutput output, string outputName, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
    {
        var trigName = caller?.DesignerName ?? "<null>";
        var actName = activator?.DesignerName ?? "<null>";
        var actClass = activator?.GetType()?.Name ?? "<null>";
        Console.WriteLine($"[CTF][HOOK] {outputName}: trigger='{trigName}', activator='{actName}', activatorClass='{actClass}'");

        return TryHandleDelivery(activator, caller, "OnTrigger");
    }

    // Also log OnStartTouch
    [EntityOutputHook("trigger_multiple", "OnStartTouch")]
    public HookResult OnDeliveryStartTouch(CEntityIOOutput output, string outputName, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
    {
        var trigName = caller?.DesignerName ?? "<null>";
        var actName = activator?.DesignerName ?? "<null>";
        var actClass = activator?.GetType()?.Name ?? "<null>";
        Console.WriteLine($"[CTF][HOOK] {outputName}: trigger='{trigName}', activator='{actName}', activatorClass='{actClass}'");

        return TryHandleDelivery(activator, caller, "OnStartTouch");
    }

    // --- Load helpers ---

    private void ApplyBasesFromConVars()
    {
        try
        {
            // CT
            if (!string.IsNullOrWhiteSpace(CtBasePos.Value) && !string.IsNullOrWhiteSpace(CtBaseAng.Value))
            {
                if (TryParseVector(CtBasePos.Value, out var pos) && TryParseQAngle(CtBaseAng.Value, out var ang))
                {
                    _ctBaseAngle = ang;
                    _ctFlag.SetBase(pos, ang);
                }
                else
                {
                    Console.WriteLine($"[CTF] Could not parse CT base from convars: '{CtBasePos.Value}' / '{CtBaseAng.Value}'");
                }
            }

            // T
            if (!string.IsNullOrWhiteSpace(TBasePos.Value) && !string.IsNullOrWhiteSpace(TBaseAng.Value))
            {
                if (TryParseVector(TBasePos.Value, out var pos) && TryParseQAngle(TBaseAng.Value, out var ang))
                {
                    _tBaseAngle = ang;
                    _tFlag.SetBase(pos, ang);
                }
                else
                {
                    Console.WriteLine($"[CTF] Could not parse T base from convars: '{TBasePos.Value}' / '{TBaseAng.Value}'");
                }
            }

            // Also apply delivery settings
            ApplyDeliverySettingsFromConVars();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CTF] Error applying bases from convars: {ex}");
        }
    }

    private void ApplyDeliverySettingsFromConVars()
    {
        var mode = (CtfDeliveryMode.Value ?? "").Trim();
        _useEntityDelivery = string.Equals(mode, "entity", StringComparison.OrdinalIgnoreCase);

        _ctDeliveryName = string.IsNullOrWhiteSpace(CtfCtDeliveryEnt.Value) ? "deliver_ct" : CtfCtDeliveryEnt.Value.Trim();
        _tDeliveryName  = string.IsNullOrWhiteSpace(CtfTDeliveryEnt.Value)  ? "deliver_t"  : CtfTDeliveryEnt.Value.Trim();
        _deliveryMissingBehavior = string.IsNullOrWhiteSpace(CtfDeliveryMissing.Value) ? "radius" : CtfDeliveryMissing.Value.Trim();

        Console.WriteLine($"[CTF] Delivery mode: {(_useEntityDelivery ? "entity" : "radius")} (CT='{_ctDeliveryName}', T='{_tDeliveryName}', missing='{_deliveryMissingBehavior}')");
    }

    private static bool TryParseVector(string value, out Vector vec)
    {
        vec = new Vector(0f, 0f, 0f);
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3) return false;

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) return false;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) return false;
        if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z)) return false;

        vec = new Vector(x, y, z);                          
        return true;
    }

    private static bool TryParseQAngle(string value, out QAngle ang)
    {
        ang = QAngle.Zero;
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3) return false;

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var pitch)) return false;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var yaw)) return false;
        if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var roll)) return false;

        ang = new QAngle(pitch, yaw, roll);
        return true;
    }

    private static string FormatVector(Vector v)
        => string.Create(CultureInfo.InvariantCulture, $"{v.X:F3} {v.Y:F3} {v.Z:F3}");

    private static string FormatAngle(QAngle a)
        => string.Create(CultureInfo.InvariantCulture, $"{a.X:F3} {a.Y:F3} {a.Z:F3}");

    private static string GetMapCfgExecPath()
        => $"ctf/{Server.MapName}.cfg";

    private static string GetGameRootDir()
    {
        // Server working dir is typically .../game/bin/<arch>; go up to .../game
        var cwd = Directory.GetCurrentDirectory();
        var gameRoot = Path.GetFullPath(Path.Combine(cwd, "..", ".."));
        return gameRoot;
    }

    private static string GetMapCfgFilePath()
        => Path.Combine(GetGameRootDir(), "csgo", "cfg", "ctf", $"{Server.MapName}.cfg");
    
    [ConsoleCommand("ctf_load")]
    private void OnLoadCtf(CCSPlayerController? player, CommandInfo command)
    {
        try
        {
            var execRelPath = GetMapCfgExecPath(); // "ctf/<map>.cfg"
            var absolute = Path.Combine(GetGameRootDir(), "csgo", "cfg", execRelPath);

            if (!File.Exists(absolute))
            {
                player?.PrintToChat($"[CTF] No saved config found for '{Server.MapName}'. Expected: {absolute}");
                Console.WriteLine($"[CTF] ctf_load: file not found: {absolute}");
                return;
            }

            // Clear old entities before applying new config
            _tFlag.RemoveEntities();
            _ctFlag.RemoveEntities();

            // Exec the cfg, then apply the convars shortly after
            Server.ExecuteCommand($"exec {execRelPath}");
            AddTimer(0.1f, ApplyBasesFromConVars);

            player?.PrintToChat($"[CTF] Loaded CTF config for '{Server.MapName}'.");
            Console.WriteLine($"[CTF] Loaded from {absolute}");
        }
        catch (Exception ex)
        {
            player?.PrintToChat("[CTF] Error loading config. See server console.");
            Console.WriteLine($"[CTF] Error loading config: {ex}");
        }
    }

    [ConsoleCommand("ctf_start")]
    private void OnStartCtf(CCSPlayerController? player, CommandInfo command)
    {
        // Ensure CTF enabled
        Server.ExecuteCommand("mp_ctf_enabled 1");

        // Only (re)apply bases if flags aren’t present (safety)
        bool needApply =
            _ctFlag.BasePosition is null || _tFlag.BasePosition is null ||
            _ctFlag.Position is null || _tFlag.Position is null;

        if (needApply)
        {
            ApplyBasesFromConVars();
            Console.WriteLine("[CTF] ctf_start: flags missing, re-applied bases from convars.");
        }
        else
        {
            Console.WriteLine("[CTF] ctf_start: flags already present, not re-applying bases.");
        }

        // Ensure objective loop is running
        _gameObjectiveTimer?.Kill();
        _gameObjectiveTimer = AddTimer(0.1f, CheckIfPlayersEnterObjectives, TimerFlags.REPEAT);

        player?.PrintToChat("[CTF] Capture The Flag started.");
        Console.WriteLine("[CTF] CTF started (timer running).");
    }
    
    // Handlers for chat/console config commands

    [ConsoleCommand("ctf_mode")]
    private void OnCmdSetDeliveryMode(CCSPlayerController? player, CommandInfo cmd)
    {
        if (cmd.ArgCount <= 1)
        {
            player?.PrintToChat("[CTF] Usage: ctf_mode radius|entity");
            return;
        }
        var mode = cmd.ArgByIndex(1).Trim().ToLowerInvariant();
        if (mode != "radius" && mode != "entity")
        {
            player?.PrintToChat("[CTF] Invalid mode. Use: radius|entity");
            return;
        }
        CtfDeliveryMode.Value = mode;
        ApplyDeliverySettingsFromConVars();
        player?.PrintToChat($"[CTF] Delivery mode set to: {mode}");
    }

    [ConsoleCommand("ctf_ct_ent")]
    private void OnCmdSetCtDeliveryEnt(CCSPlayerController? player, CommandInfo cmd)
    {
        if (cmd.ArgCount <= 1)
        {
            player?.PrintToChat("[CTF] Usage: ctf_ct_ent <targetname>  (default: deliver_ct)");
            return;
        }
        var name = cmd.ArgByIndex(1).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            player?.PrintToChat("[CTF] Targetname cannot be empty.");
            return;
        }
        CtfCtDeliveryEnt.Value = name;
        ApplyDeliverySettingsFromConVars();
        player?.PrintToChat($"[CTF] CT delivery trigger set to: {name}");
    }

    [ConsoleCommand("ctf_t_ent")]
    private void OnCmdSetTDeliveryEnt(CCSPlayerController? player, CommandInfo cmd)
    {
        if (cmd.ArgCount <= 1)
        {
            player?.PrintToChat("[CTF] Usage: ctf_t_ent <targetname>  (default: deliver_t)");
            return;
        }
        var name = cmd.ArgByIndex(1).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            player?.PrintToChat("[CTF] Targetname cannot be empty.");
            return;
        }
        CtfTDeliveryEnt.Value = name;
        ApplyDeliverySettingsFromConVars();
        player?.PrintToChat($"[CTF] T delivery trigger set to: {name}");
    }

    [ConsoleCommand("ctf_delivery_missing")]
    private void OnCmdSetDeliveryMissing(CCSPlayerController? player, CommandInfo cmd)
    {
        if (cmd.ArgCount <= 1)
        {
            player?.PrintToChat("[CTF] Usage: ctf_delivery_missing radius|block");
            return;
        }
        var val = cmd.ArgByIndex(1).Trim().ToLowerInvariant();
        if (val != "radius" && val != "block")
        {
            player?.PrintToChat("[CTF] Invalid value. Use: radius|block");
            return;
        }
        CtfDeliveryMissing.Value = val;
        ApplyDeliverySettingsFromConVars();
        player?.PrintToChat($"[CTF] Missing-trigger behavior set to: {val}");
    }

    [ConsoleCommand("ctf_require_home_flag")]
    private void OnCmdSetRequireHomeFlag(CCSPlayerController? player, CommandInfo cmd)
    {
        if (cmd.ArgCount <= 1)
        {
            player?.PrintToChat("[CTF] Usage: ctf_require_home_flag 0|1");
            return;
        }
        var arg = cmd.ArgByIndex(1).Trim();
        if (!(arg == "0" || arg == "1"))
        {
            player?.PrintToChat("[CTF] Invalid value. Use: 0|1");
            return;
        }
        CtfRequireHomeFlag.Value = arg == "1" ? 1 : 0;
        // No extra apply needed; flag is read at capture time.
        player?.PrintToChat($"[CTF] Require-home-flag set to: {CtfRequireHomeFlag.Value}");
    }

    // Plays the start capture sound (defuse beep) for the player, if configured
    private void TryPlayStartBeep(CCSPlayerController player)
    {
        try
        {
            var ev = CtfPickupStartSound?.Value;
            if (!string.IsNullOrWhiteSpace(ev))
            {
                player.EmitSound(ev!); // default: "C4.DisarmStart"
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CTF] Failed to play start sound '{CtfPickupStartSound.Value}': {ex.Message}");
        }
    }
}