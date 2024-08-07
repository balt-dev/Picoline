using System;
using System.ComponentModel;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.PicoPlayer;

[CustomEntity("PicoTrigger")]
public class PicoTrigger : Trigger {
    private readonly RefillKind _refillKind;
    private float _cooldown;
    
    public PicoTrigger(EntityData data, Vector2 offset) : base(data, offset) {
        _refillKind = (string) data.Values["kind"] switch {
            "on" => RefillKind.On,
            "off" => RefillKind.Off,
            "swap" => RefillKind.Swap,
            _ => throw new InvalidEnumArgumentException("Attribute \"kind\" of PICO-8 Trigger must be either \"on\", \"off\", or \"swap\"")
        };
    }

    public override void OnEnter(Player player) {
        if (player is not PicoPlayer picoPlayer)
            throw new ArgumentException("Tried to activate a PICO-8 trigger with an incompatible player object. This likely stems from a mod incompatibility!");
        if (_cooldown > 0) return;
        picoPlayer.Overriding = _refillKind switch {
            RefillKind.Swap => !picoPlayer.Overriding,
            RefillKind.On => true,
            RefillKind.Off => false,
        };
        _cooldown = 0.1f;
    }

    public override void Update() {
        base.Update();
        if (_cooldown > 0) _cooldown -= Engine.DeltaTime;
    }
}