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
    public override string ModuleName => "Capture The Flag 3";

    public override string ModuleAuthor => "Astinox";

    public override string ModuleVersion => "0.0.3";

    public override string ModuleDescription => "Adds the Capture the Flag game mode to Counter Strike 2";
    
    private readonly TFlag _tFlag = new();

    private CtFlag _ctFlag = new();

    private Dictionary<CCSPlayerController, Timer> _respawnTimers = new();
    private Dictionary<CCSPlayerController, int> _respawnTimes = new();

    private bool _isEditing = false;

    private Timer? _gameObjectiveTimer;
    
    public static CaptureTheFlag Instance { get; private set; } = null!;

    public override void Load(bool hotReload)
    {
        Instance = this;

        Console.WriteLine("loaded CTF");

        RegisterCommands();

        RegisterListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
        
        // CTF objective position tracking loop
        _gameObjectiveTimer = AddTimer(0.1f, CheckIfPlayersEnterObjectives, TimerFlags.REPEAT);
        
        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            _tFlag.RemoveEntities();
            _ctFlag.RemoveEntities();
            Console.WriteLine("test mapstart");
        });
        
        RegisterListener<Listeners.OnMapEnd>(() =>
        {
            _tFlag.RemoveEntities();
            _ctFlag.RemoveEntities();
        });
        
        RegisterFakeConVars(typeof(FakeConVar<>));
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
            // TODO: SHOW FLAG BASE LOCATION
            // TODO: SHOW BUY ZONE LOCATION
            // TODO: SHOW SPAWN LOCATIONS

            player.PrintToCenter(Localizer["editor_enabled"]);
        }
        else
        {
            _isEditing = false;
            // TODO: HIDE FLAG BASE LOCATION
            // TODO: HIDE BUY ZONE LOCATION
            // TODO: HIDE SPAWN LOCATIONS
            player.PrintToCenter(Localizer["editor_disabled"]);
        }
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
        
        _ctFlag.SetBase(playerPawn.AbsOrigin, playerPawn.EyeAngles);
    }
    
    private void OnAddTBase(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || player.PlayerPawn.Value == null || !player.PlayerPawn.Value.IsValid) return;
        
        CCSPlayerPawn playerPawn = player.PlayerPawn.Value;
        
        if (playerPawn.AbsOrigin is null) return;
        
        _tFlag.SetBase(playerPawn.AbsOrigin, playerPawn.EyeAngles);
    }
    
    public void OnPlayerTouchTFlag(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return;
        
        if(!player.PawnIsAlive) return;
        
        _tFlag.Pickup(player);
    }
    
    public void OnPlayerTouchCTFlag(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return;
        
        if(!player.PawnIsAlive) return;
        
        _ctFlag.Pickup(player);
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

        return HookResult.Continue;
    }
        
    [GameEventHandler]
    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player == null || !player.IsValid) return HookResult.Continue;

        if(!@event.Disconnect)
        {
            player.Respawn();
        }

        return HookResult.Continue;
    }

    public void OnPlayerEnterCtBase(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return;

        if (player.Team == CsTeam.Terrorist) return;

        if (!_tFlag.IsTaken()) return;
        
        if (_tFlag.Carrier!.UserId != player.UserId) return;

        _tFlag.Secure(player);
        
        player.MVPs += 1;
                
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iMVPs");
    }
    
    public void OnPlayerEnterTBase(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return;

        if (player.Team == CsTeam.CounterTerrorist) return;
        
        if (!_ctFlag.IsTaken()) return;

        if (_ctFlag.Carrier!.UserId != player.UserId) return;
        
        _ctFlag.Secure(player);
        
        player.MVPs += 1;
                
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iMVPs");
    }

    private void CheckIfPlayersEnterObjectives()
    {
        // If somebody enabled the server-wide editor, disregard all CTF objective events
        if (IsInEditor()) return;
            
        foreach (CCSPlayerController player in Utilities.GetPlayers())
        {
            if (!player.IsValid || player.PlayerPawn.Value == null)
                continue;

            var pawn = player.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid)
                continue;

            var playerPos = pawn.AbsOrigin;
            if (playerPos == null) continue;

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
    
    private float VectorDistance(Vector a, Vector b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
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
}