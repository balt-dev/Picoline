using System;
using System.ComponentModel;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Picoline;

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
                if (player.Get<PicoOverrideComponent>() is {} comp1)
                    player.Remove(comp1);
                else
                    player.Add(new PicoOverrideComponent(true, true));
                break;
            case RefillKind.Inside or RefillKind.On:
                if (player.Get<PicoOverrideComponent>() == null)
                    player.Add(new PicoOverrideComponent(true, true));
                break;
            default:
                if (player.Get<PicoOverrideComponent>() is {} comp3)
                    player.Remove(comp3);
                break;
        }
    }

    public override void OnLeave(Player player) {
        base.OnLeave(player);
        switch (_refillKind) {
            case RefillKind.Swap:
                if (player.Get<PicoOverrideComponent>() is {} comp1)
                    player.Remove(comp1);
                else
                    player.Add(new PicoOverrideComponent(true, true));
                break;
            case RefillKind.Inside:
                if (player.Get<PicoOverrideComponent>() is {} comp3)
                    player.Remove(comp3);
                break;
            case RefillKind.Outside:
                if (player.Get<PicoOverrideComponent>() is not null)
                    player.Add(new PicoOverrideComponent(true, true));
                break;
            default: break;
        }
    }
}