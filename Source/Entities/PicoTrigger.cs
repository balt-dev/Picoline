using System;
using System.ComponentModel;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Picoline.Entities;

[CustomEntity("PicoTrigger")]
public class PicoTrigger(EntityData data, Vector2 offset) : Trigger(data, offset) {
    private readonly RefillKind _refillKind = (string)data.Values["kind"] switch {
        "on" => RefillKind.On,
        "off" => RefillKind.Off,
        "swap" => RefillKind.Swap,
        "inside" => RefillKind.Inside,
        "outside" => RefillKind.Outside,
        _ => throw new InvalidEnumArgumentException("Attribute \"kind\" of PICO-8 Trigger must be either \"on\", \"off\", \"swap\", \"inside\", or \"outside\"")
    };

    public override void OnEnter(Player player) {
        base.OnEnter(player);
        switch (_refillKind) {
            case RefillKind.Swap:
                PicolineModule.ShouldBePicoline ^= true;
                break;
            case RefillKind.Inside or RefillKind.On:
                PicolineModule.ShouldBePicoline = true;
                break;
            default:
                PicolineModule.ShouldBePicoline = false;
                break;
        }
    }

    public override void OnLeave(Player player) {
        base.OnLeave(player);
        switch (_refillKind) {
            case RefillKind.Inside:
                PicolineModule.ShouldBePicoline = false;
                break;
            case RefillKind.Outside:
                PicolineModule.ShouldBePicoline = true;
                break;
            default: break;
        }
    }
}