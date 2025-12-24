using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;
namespace Celeste.Mod.Picoline;

public class PicoOverrideComponent : Component
{
    public PicoPlayer player;

    public PicoOverrideComponent(bool active, bool visible) : base(active, visible) {
    }

    public override void Added(Entity entity) {
        base.Added(entity);
        if (!(entity is Player p))
            throw new InvalidOperationException("Cannot add picoline player override component to non-player");
        player = new(p);
        player.self = p;
        p.StateMachine.state = Player.StNormal;
        p.StateMachine.RemoveSelf();
        p.Hair.Active = false;
        p.Hair.Visible = false;
        p.Position.Y -= 8;
        p.Collider = PicoPlayer.PicoHitbox;
        p.hurtbox = PicoPlayer.PicoHitbox;
        player._hairCalcPosition = p.Position;
        player._oldLightPosition = p.Light.Position;
        p.Light.Position = new Vector2(4, 4);
        player._oldHairNodeCount = p.Hair.Nodes.Count;
        p.Hair.Nodes = Enumerable.Repeat(p.Position, 5).ToList();
        player.OverrideHitboxes();
    }

    public override void Removed(Entity entity) {
        base.Removed(entity);
        if (!(entity is Player p))
            throw new InvalidOperationException("Cannot add picoline player override component to non-player");
        p.StateMachine.state = Player.StNormal;
        p.Add(p.StateMachine);
        p.Hair.Active = true;
        p.Hair.Visible = true;
        p.Position.Y += 8;
        p.Collider = p.normalHitbox;
        p.hurtbox = p.normalHurtbox;
        p.Light.Position = player._oldLightPosition;
        p.Hair.Nodes = Enumerable.Repeat(p.Position, player._oldHairNodeCount).ToList();
        player.RestoreHitboxes();
    }

    public override void Render() {
        player.Render();
    }

    public override void Update() {
        player.Update();
    }
}