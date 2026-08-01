using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.Picoline.Pico;

internal class Smoke : PicoEntity {
    internal Smoke(Vector2 position) : base(position) {
        Depth = 50;
        Sprite = 29;
        Speed.Y = -0.1f;
        Speed.X = -0.1f + Calc.Random.NextFloat(.2f);
        X += Utils.Rnd(2) - 1f;
        Y += Utils.Rnd(2) - 1f;
        Flip.X = Utils.Maybe();
        Flip.Y = Utils.Maybe();
    }

    public override void Update() {
        base.Update();
        Sprite += 0.2f / Utils.SecondsPerTick * Engine.DeltaTime;
        if (Sprite >= 31) RemoveSelf();
    }
}
