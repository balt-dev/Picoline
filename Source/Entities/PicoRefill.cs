using System;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Picoline.Entities;

[CustomEntity("PicoRefill")]
public class PicoRefill : Refill {
    private readonly RefillKind _refillKind;
    
    public PicoRefill(EntityData data, Vector2 offset) : base(data, offset) {
        // i should use data.Enum for this but i have to keep it like this for backwards compat
        _refillKind = (string) data.Values["kind"] switch {
            "on" => RefillKind.On,
            "off" => RefillKind.Off,
            "swap" => RefillKind.Swap,
            _ => throw new ArgumentException("Attribute \"kind\" of PICO-8 Refill must be either \"on\", \"off\", or \"swap\"")
        };
        
        var idleSprite = _refillKind switch {
            RefillKind.Swap => new Sprite(GFX.Game, "objects/picoRefill/swap_idle"),
            RefillKind.On => new Sprite(GFX.Game, "objects/picoRefill/on_idle"),
            RefillKind.Off => new Sprite(GFX.Game, "objects/picoRefill/off_idle"),
            _ => null!
        };
        idleSprite.AddLoop("idle", "", 0.1f);
        idleSprite.Play("idle");
        idleSprite.CenterOrigin();
        Remove(sprite);
        Add(idleSprite);
        sprite = idleSprite;
        
        var outlineSprite = new Image(GFX.Game["objects/picoRefill/outline"]) { Visible = false };
        outlineSprite.CenterOrigin();
        Remove(outline);
        Add(outlineSprite);
        outline = outlineSprite;

        var flashSprite = new Sprite(GFX.Game, "objects/picoRefill/flash");
        flashSprite.Add("flash", "", 0.05f);
        flashSprite.OnFinish = _ => flashSprite.Visible = false;
        flashSprite.CenterOrigin();
        Remove(flash);
        Add(flashSprite);
        flash = flashSprite;
        
        Get<PlayerCollider>().OnCollide = OnPlayer;

        p_regen = new ParticleType(p_regen) {
            Color = Pico.Utils.Colors.White,
            ColorMode = ParticleType.ColorModes.Static,
        };
        
        p_glow = new ParticleType(p_glow) {
            Color = Pico.Utils.Colors.White,
            ColorMode = ParticleType.ColorModes.Static,
        };
    }

    public override void Update() {
        base.Update();
        var player = level.Tracker.GetEntity<Player>();
        if (player == null) return;
        light.Alpha = CanActivate(player) ? 1.0f : 0.3f;
        bloom.Alpha = CanActivate(player) ? 1.0f : 0.3f;

    }

    private new void OnPlayer(Player player) {
        if (player == null) return;
        if (!CanActivate(player)) return;
        Audio.Play("event:/game/general/diamond_touch", Position);
        Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
        Collidable = false;
        respawnTimer = 2.5f;
        switch (_refillKind) {
            case RefillKind.Swap:
                PicolineModule.ShouldBePicoline ^= true;
                break;
            case RefillKind.On:
                PicolineModule.ShouldBePicoline = true;
                break;
            default:
                PicolineModule.ShouldBePicoline = false;
                break;
        }

        Add(new Coroutine(RefillRoutine(player)));
    }
    
    private bool CanActivate(Player player) {
        if (player == null) return false;
        return _refillKind switch {
            RefillKind.Swap => true,
            RefillKind.On => !PicolineModule.ShouldBePicoline,
            RefillKind.Off => PicolineModule.ShouldBePicoline,
            _ => false
        };
    }
}