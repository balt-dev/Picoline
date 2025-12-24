using System;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Picoline;

[CustomEntity("PicoRefill")]
public class PicoRefill : Refill {
    private readonly RefillKind _refillKind;
    
    public PicoRefill(EntityData data, Vector2 offset) : base(data, offset) {
        _refillKind = (string) data.Values["kind"] switch {
            "on" => RefillKind.On,
            "off" => RefillKind.Off,
            "swap" => RefillKind.Swap,
            _ => throw new ArgumentException("Attribute \"kind\" of PICO-8 Refill must be either \"on\", \"off\", or \"swap\"")
        };
        
        var idleSprite = _refillKind switch {
            RefillKind.Swap => new Sprite(GFX.Game, "objects/picoRefill/swap_idle"),
            RefillKind.On => new Sprite(GFX.Game, "objects/picoRefill/on_idle"),
            RefillKind.Off => new Sprite(GFX.Game, "objects/picoRefill/off_idle")
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
            Color = PicoColors.White,
            ColorMode = ParticleType.ColorModes.Static,
        };
        
        p_glow = new ParticleType(p_glow) {
            Color = PicoColors.White,
            ColorMode = ParticleType.ColorModes.Static,
        };
    }

    public override void Update() {
        base.Update();
        var player = level.Tracker.GetEntity<Player>();
        light.Alpha = CanActivate(player) ? 1.0f : 0.3f;
        bloom.Alpha = CanActivate(player) ? 1.0f : 0.3f;

    }

    private new void OnPlayer(Player player) {
        if (!CanActivate(player)) return;
        Audio.Play("event:/game/general/diamond_touch", Position);
        Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
        Collidable = false;
        respawnTimer = 2.5f;
        switch (_refillKind) {
            case RefillKind.Swap:
                if (player.Get<PicoOverrideComponent>() is {} comp1)
                    player.Remove(comp1);
                else
                    player.Add(new PicoOverrideComponent(true, true));
                break;
            case RefillKind.On:
                if (player.Get<PicoOverrideComponent>() == null)
                    player.Add(new PicoOverrideComponent(true, true));
                break;
            default:
                if (player.Get<PicoOverrideComponent>() is {} comp3)
                    player.Remove(comp3);
                break;
        }

        Add(new Coroutine(RefillRoutine(player)));
    }
    
    private bool CanActivate(Player player) {
        return _refillKind switch {
            RefillKind.Swap => true,
            RefillKind.On when player.Get<PicoOverrideComponent>() == null => true,
            RefillKind.Off when player.Get<PicoOverrideComponent>() != null => true,
            _ => false
        };
    }
}