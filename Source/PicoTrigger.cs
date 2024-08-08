using System;
using System.ComponentModel;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Picoline;

[CustomEntity("PicoTrigger")]
public class PicoTrigger : Trigger {
    private readonly RefillKind _refillKind;
    
    public PicoTrigger(EntityData data, Vector2 offset) : base(data, offset) {
        _refillKind = (string) data.Values["kind"] switch {
            "on" => RefillKind.On,
            "off" => RefillKind.Off,
            "swap" => RefillKind.Swap,
            "inside" => RefillKind.Inside,
            "outside" => RefillKind.Outside,
            _ => throw new InvalidEnumArgumentException("Attribute \"kind\" of PICO-8 Trigger must be either \"on\", \"off\", \"swap\", \"inside\", or \"outside\"")
        };
    }

    public override void OnEnter(global::Celeste.Player player) {
        base.OnEnter(player);
        if (player is not Player picoPlayer)
            return;
        picoPlayer.Overriding = _refillKind switch {
            RefillKind.Swap => !picoPlayer.Overriding,
            RefillKind.Inside or RefillKind.On => true,
            RefillKind.Outside or RefillKind.Off => false,
        };
    }

    public override void OnLeave(global::Celeste.Player player) {
        base.OnLeave(player);
        if (player is not Player picoPlayer)
            return;
        picoPlayer.Overriding = _refillKind switch {
            RefillKind.Inside => false,
            RefillKind.Outside => true,
            _ => picoPlayer.Overriding
        };
    }
}