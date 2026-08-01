using System;
using Celeste.Mod.Roslyn.ModLifecycleAttributes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.Picoline.Pico;

internal class PicoEntity(Vector2 position) : Entity(position) {
    protected record struct FlipOptions(bool X, bool Y);
    protected float Sprite;
    protected Vector2 Speed;
    protected FlipOptions Flip;

    public override void Update() {
        base.Update();
        Position += Speed * Utils.SpeedFactor * Engine.DeltaTime;
    }

    public virtual bool Draw() => true;
    
    public override void Render() {
        base.Render();
        if (Draw()) Utils.Spr((int) Sprite, Position, Vector2.One, Flip.X, Flip.Y);
    }
}
