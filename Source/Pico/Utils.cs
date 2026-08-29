using System;
using Celeste.Mod.Roslyn.ModLifecycleAttributes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.Picoline.Pico;

internal static class Utils {
    internal const int SpeedFactor = 60;
    internal const float SecondsPerTick = 1 / 30f;
    internal static float TicksPerDelta => 1 / SecondsPerTick * Engine.DeltaTime;

    internal static bool Maybe() => Calc.Random.Chance(0.5f);

    internal static float Rnd(float max) => Calc.Random.NextFloat(max);

    static MTexture? Atlas;
    static MTexture? PlayerAtlas;
    [OnLoadContent]
    internal static void OnLoadContent(bool first) {
        Atlas = null;
        PlayerAtlas = null;
    }

    internal static void Spr(int sprite, Vector2 position, Vector2 size, bool flipX, bool flipY, Color? tint = null) {
        Atlas ??= GFX.Game["pico8/atlas"];
        if (sprite <= 0) return;
        Color color = tint ?? Color.White;
        var fx = SpriteEffects.None;
        if (flipX) fx |= SpriteEffects.FlipHorizontally;
        if (flipY) fx |= SpriteEffects.FlipVertically;
        Draw.SpriteBatch.Draw(
            Atlas.Texture.Texture_Safe,
            position, new Rectangle(
                sprite % 16 * 8 + (int) Atlas.ClipRect.X, sprite / 16 * 8 + (int) Atlas.ClipRect.Y,
                (int)size.X * 8, (int)size.Y * 8
            ), color, 0f,
            Vector2.Zero, 1f, fx, 0f
        );
    }

    internal static void PlayerSpr(int sprite, Vector2 position, bool flipX, bool flipY, Color hairColor, bool silhouette = false) {
        PlayerAtlas ??= GFX.Game["Picoline/player_atlas"];
        if (sprite <= 0) return;
        var fx = SpriteEffects.None;
        if (flipX) fx |= SpriteEffects.FlipHorizontally;
        if (flipY) fx |= SpriteEffects.FlipVertically;
        Draw.SpriteBatch.Draw(
            PlayerAtlas.Texture.Texture_Safe,
            position, new Rectangle(
                sprite % 8 * 8, silhouette ? 16 : 0,
                8, 8
            ),
            silhouette ? hairColor : Color.White,
            0f, Vector2.Zero, 1f, fx, 0f
        );
        Draw.SpriteBatch.Draw(
            PlayerAtlas.Texture.Texture_Safe,
            position, new Rectangle(
                sprite % 8 * 8, 8,
                8, 8
            ),
            hairColor,
            0f, Vector2.Zero, 1f, fx, 0f
        );
    }

    internal static bool SolidAt(Scene scene, Vector2 pos) => scene.CollideCheck<Solid>(pos);

    internal static void PSfx(int sfx) => Audio.Play("event:/classic/sfx" + sfx);

    internal static void DrawCircle(Vector2 position, float radius, Color color) {
        var x = position.X;
        var y = position.Y;
        if (radius <= 1.0) {
            Draw.Rect(x - 1f, y, 3f, 1f, color);
            Draw.Rect(x, y - 1f, 1f, 3f, color);
        } else if (radius <= 2.0) {
            Draw.Rect(x - 2f, y - 1f, 5f, 3f, color);
            Draw.Rect(x - 1f, y - 2f, 3f, 5f, color);
        } else if (radius <= 3.0) {
            Draw.Rect(x - 3f, y - 1f, 7f, 3f, color);
            Draw.Rect(x - 1f, y - 3f, 3f, 7f, color);
            Draw.Rect(x - 2f, y - 2f, 5f, 5f, color);
        } else {
            Draw.Circle(position, radius + 0.5f, color, 8);
        }
    }
    
    internal static _Col Colors = new();
    
    internal class _Col {
        internal _Col() { }
        public Color this[int index] => ((Color[]) [
            Calc.HexToColor("000000"),
            Calc.HexToColor("1d2b53"),
            Calc.HexToColor("7e2553"),
            Calc.HexToColor("008751"),
            Calc.HexToColor("ab5236"),
            Calc.HexToColor("5f574f"),
            Calc.HexToColor("c2c3c7"),
            Calc.HexToColor("fff1e8"),
            Calc.HexToColor("ff004d"),
            Calc.HexToColor("ffa300"),
            Calc.HexToColor("ffec27"),
            Calc.HexToColor("00e436"),
            Calc.HexToColor("29adff"),
            Calc.HexToColor("83769c"),
            Calc.HexToColor("ff77a8"),
            Calc.HexToColor("ffccaa"),
        ])[index];

        public Color Black => this[0];
        public Color DarkBlue => this[1];
        public Color DarkRed => this[2];
        public Color DarkGreen => this[3];
        public Color Brown => this[4];
        public Color Grey => this[5];
        public Color LightGrey => this[6];
        public Color White => this[7];
        public Color Red => this[8];
        public Color Orange => this[9];
        public Color Yellow => this[10];
        public Color Green => this[11];
        public Color Cyan => this[12];
        public Color Purple => this[13];
        public Color Pink => this[14];
        public Color Cream => this[15];
    }
    
    [Command("picoplayer", "Swaps the state of the PICO-8 refill.")]
    internal static void CmdPicoPlayer() {
        if (Engine.Scene is not Level level) return;
        PicolineModule.ShouldBePicoline ^= true;
    }
}
