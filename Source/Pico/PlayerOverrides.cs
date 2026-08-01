using System;
using System.Collections;
using System.Collections.Generic;
using Celeste.Mod.Roslyn.ModLifecycleAttributes;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.Picoline.Pico;

public static class PlayerOverrides {

    [OnLoad]
    internal static void LoadHooks() {
        using (new DetourConfigContext(new(nameof(Picoline), priority: int.MinValue)).Use()) {
            On.Celeste.Player.Render += OnPlayerRender;
            On.Celeste.Player.Update += OnPlayerUpdate;
        }
        On.Celeste.Player.DebugRender += OnPlayerDebugRender;
        On.Celeste.PlayerDeadBody.ctor += OnPlayerDeadBodyCtor;
        On.Celeste.Session.UpdateLevelStartDashes += OnSessionUpdateLevelStartDashes;
        On.Celeste.Level.LoadLevel += OnLoadLevel;
    }

    [OnUnload]
    internal static void UnloadHooks() {
        On.Celeste.Session.UpdateLevelStartDashes -= OnSessionUpdateLevelStartDashes;
        On.Celeste.Player.DebugRender -= OnPlayerDebugRender;
        On.Celeste.Player.Render -= OnPlayerRender;
        On.Celeste.Player.Update -= OnPlayerUpdate;
        On.Celeste.PlayerDeadBody.ctor -= OnPlayerDeadBodyCtor;
        On.Celeste.Level.LoadLevel -= OnLoadLevel;
    }

    private static void OnPlayerDebugRender(On.Celeste.Player.orig_DebugRender orig, Player self, Camera camera){
        orig(self, camera);
        Draw.Pixel.Draw(self.Position, Vector2.Zero, Color.White);
    }

    private static void OnSessionUpdateLevelStartDashes(On.Celeste.Session.orig_UpdateLevelStartDashes orig, Session self) {
        orig(self);
        PicolineModule.Session.WasPicolineOnRoomEnter = PicolineModule.ShouldBePicoline;
    }

    private static void OnLoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
        if (playerIntro is not Player.IntroTypes.Transition)
            PicolineModule.ShouldBePicoline = PicolineModule.Session.WasPicolineOnRoomEnter;
        orig(level, playerIntro, isFromLoader);
    }

    private static void OnPlayerUpdate(On.Celeste.Player.orig_Update orig, Player self) {
        if (PicolineModule.ShouldBePicoline) {
            if (self.Get<PicoComponent>() is not { } pico) { self.Add(pico = new PicoComponent()); }
            pico.PicoUpdate();
        } else {
            self.StateMachine.Active = true;
            if (self.Get<PicoComponent>() is not null) { self.Components.RemoveAll<PicoComponent>(); }
            orig(self);
        }
    }

    private static void OnPlayerRender(On.Celeste.Player.orig_Render orig, Player self) {
        if (PicolineModule.ShouldBePicoline) {
            if (self.Get<PicoComponent>() is { } pico)
                pico.PicoRender();
        } else {
            orig(self);
        }
    }

    private static void OnPlayerDeadBodyCtor(On.Celeste.PlayerDeadBody.orig_ctor orig, PlayerDeadBody self, Player player, Vector2 direction) {
        orig(self, player, direction);
        if (player.Get<PicoComponent>() is not null) {
            self.Components.RemoveAll<Coroutine>();
            self.bounce = Vector2.Zero;
            self.Add(new Coroutine(PicoPlayerDeadBodyCoroutine(self)));
            self.Add(self.deathEffect = new PicoDeathEffect(player.Center));
        }
    }
    
    private static IEnumerator PicoPlayerDeadBodyCoroutine(PlayerDeadBody self) {
        Level level = self.SceneAs<Level>();
        level.Shake();
        Input.Rumble(RumbleStrength.Medium, RumbleLength.Short);
        Utils.PSfx(0);
        
        yield return 0.35f;
        if (self.ActionDelay > 0f) {
            yield return self.ActionDelay;
        }

        self.End();
    }
}

internal class PicoDeathEffect : DeathEffect {
    internal List<DeadParticle> Particles = [];
    public PicoDeathEffect(Vector2 position) : base(Color.Transparent, Vector2.Zero) {
        Active = true;
        for (int i = 0; i <= 7; i++) {
            float num = i / 8f;
            Particles.Add(new DeadParticle(position, new(MathF.Cos(num * MathF.PI * 2) * 3f, MathF.Sin((num + 0.5f) * MathF.PI * 2) * 3f)));
        }
    }
    public override void Update() {
        foreach (var particle in Particles) particle.Update();
    }
    public override void Render() {
        foreach (var particle in Particles) particle.Render();
    }
}

class DeadParticle(Vector2 position, Vector2 speed) {
    float Timer = 10f;
    Vector2 Position = position;
    bool Done;
    public void Update() {
        Position += speed * Utils.TicksPerDelta;
        Timer -= 1 * Utils.TicksPerDelta;
        if (Timer <= 0) {
            Done = true;
        }
    }
    public void Render() {
        if (Done) return;
        Draw.Rect(
            Position.X - Timer / 5 - 1,
            Position.Y - Timer / 5 - 1,
            Timer / 5 * 2 + 2,
            Timer / 5 * 2 + 2,
            Color.Black
        );
        Draw.Rect(
            Position.X - Timer / 5,
            Position.Y - Timer / 5,
            Timer / 5 * 2,
            Timer / 5 * 2,
            Utils.Colors[(int) (14 + (((Timer % 2) + 2) % 2))]
        );
    }
}