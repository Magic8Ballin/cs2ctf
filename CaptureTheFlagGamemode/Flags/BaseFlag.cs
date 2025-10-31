using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace CaptureTheFlagGamemode.Flags;

public class BaseFlag
{
    private CDynamicProp? _entity;
    
    private CBeam? _baseEntity;
    
    public CCSPlayerController? Carrier;

    public Vector? BasePosition;

    public readonly float BaseRadius = 100.0f;
        
    public Vector? Position;
    
    public readonly float FlagRadius = 40.0f;

    protected virtual int[] BaseBeamColor { get; set; } = new int[] {255,255,255,255 };
    public virtual string Model { get; set; } = "models/ctf/ctf_flag_t.vmdl";

    public virtual CsTeam Team { get; set; } = CsTeam.None;

    public readonly Dictionary<string, string> Sounds = new Dictionary<string, string>
    {
        {"pickup", "ctf_flag_pickup"},
        {"win", "ctf_flag_win"},
        {"return", "ctf_flag_return"}
    };

    public void SetBase(Vector position, QAngle? angle = null)
    {
        BasePosition = new Vector(position.X, position.Y, position.Z);
        
        Spawn(position, angle);

        if (!CaptureTheFlag.Instance.FlagBaseHasBeam.Value) return;
                
        CBeam beam = Utilities.CreateEntityByName<CBeam>("beam")!;
        beam.Render = Color.FromArgb(BaseBeamColor[0], BaseBeamColor[1], BaseBeamColor[2], BaseBeamColor[3]);
        beam.Width = 10f;
        beam.Teleport(position, QAngle.Zero, Vector.Zero);
        beam.EndPos.X = position.X;
        beam.EndPos.Y = position.Y;
        beam.EndPos.Z = position.Z + 120f;
        beam.DispatchSpawn();

        _baseEntity = beam;
    }

    public void Spawn(Vector position, QAngle? angle = null)
    {
        Carrier = null;

        if (angle == null)
            angle = new QAngle(0, 0, 0);
        else
            angle = new QAngle(0, angle.Y, 0);

        position = new Vector(position.X, position.Y, position.Z);

        // If we have a flag already around, make sure to remove its entity and trigger as we only want one of it
        if (_entity != null) { RemoveFlagEntities(); }
        
        CDynamicProp entity = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic")!;
        
        entity.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= ~(uint)(1 << 2);
        
        entity.SetModel(Model);
        entity.DispatchSpawn();
        
        entity.Teleport(position,  angle, Vector.Zero);
        
        entity.Collision.CollisionGroup = (byte) CollisionGroup.COLLISION_GROUP_NEVER;
            
        _entity = entity;
        
        Position = position;
    }

    public void Pickup(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.PlayerPawn.Value == null || !player.PlayerPawn.Value.IsValid) return;

        Carrier = player;
        
        _entity!.AcceptInput("SetParent", player.PlayerPawn.Value, _entity, "!activator");
        
        var origin = player.PlayerPawn.Value.AbsOrigin;
        if (origin != null)
        {
            var offset = new Vector(2, -2, 50);
            _entity.Teleport(origin + offset, new QAngle(0, player.PlayerPawn.Value.EyeAngles.Y, 0), player.PlayerPawn.Value.AbsVelocity);
        }
        
        foreach (CCSPlayerController p in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false }))
        {
            p.EmitSound(Sounds["pickup"]);
        }

        Position = null;
    }

    public void Drop(Vector position, QAngle? angle = null)
    {
        Spawn(position, angle);
    }

    public void Return()
    {
        foreach (CCSPlayerController p in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false }))
        {
            p.EmitSound(Sounds["return"]);
        }
        
        Spawn(BasePosition!);
    }

    public void Secure(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return;
        
        foreach (CCSPlayerController p in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false }))
        {
            p.EmitSound(Sounds["win"]);
        }

        Spawn(BasePosition!);
    }

    public void RemoveEntities()
    {
        if (_baseEntity != null && _baseEntity.IsValid) { _baseEntity.Remove(); }
        _baseEntity = null;
        
        RemoveFlagEntities();
    }

    public void RemoveFlagEntities()
    {
        if (_entity != null && _entity.IsValid) { _entity.Remove(); }
        _entity = null;
    }

    public bool IsTaken()
    {
        return Position is null;
    }
}