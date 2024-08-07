using System;
using System.Reflection;
using Celeste.Pico8;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mod.Picoline;

public class PicolineModule : EverestModule {
    public static PicolineModule Instance { get; private set; }

    public PicolineModule() {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(PicolineModule), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(PicolineModule), LogLevel.Info);
#endif
    }

    #nullable enable
    public bool ExtVarsLoaded;

    public override void Load()
    {
        On.Celeste.Player.TransitionTo += FixTransitionJank;
        On.Celeste.Player.Die += PicoDie;
        On.Celeste.Player.Boost += FixBoost;
        On.Celeste.Player.RedBoost += FixRedBoost;
        On.Celeste.Player.ChaserState.ctor += FixOmniManBadeline;
        On.Celeste.Level.LoadNewPlayer += ReplacePlayerWithOurs;
        BoostCoroutineHook = new ILHook(
            typeof(Booster).GetMethod("BoostRoutine", BindingFlags.NonPublic | BindingFlags.Instance)!.GetStateMachineTarget()!,
            AllowPicoBoosting
        );
        
        EverestModuleMetadata extVars = new() {
            Name = "ExtendedVariantMode",
            Version = new Version(0, 38 ,0)
        };

        ExtVarsLoaded = Everest.Loader.DependencyLoaded(extVars);
    }

    private ILHook BoostCoroutineHook;

#nullable disable

    public override void Unload()
    {
        On.Celeste.Player.TransitionTo -= FixTransitionJank;
        On.Celeste.Player.Die -= PicoDie;
        On.Celeste.Player.Boost -= FixBoost;
        On.Celeste.Player.RedBoost -= FixRedBoost;
        On.Celeste.Player.ChaserState.ctor -= FixOmniManBadeline;
        On.Celeste.Level.LoadNewPlayer -= ReplacePlayerWithOurs;
        
        BoostCoroutineHook.Dispose();
    }

    private static void FixOmniManBadeline(On.Celeste.Player.ChaserState.orig_ctor orig, ref Player.ChaserState self, Player player) {
        orig(ref self, player);
        if (player is not PicoPlayer { Overriding: true }) return;
        
        self.Position.X += 4;
        self.Position.Y += 8;
    }

    // It's fineeeeee :)
    #pragma warning disable CL0003 
    private static Player ReplacePlayerWithOurs(On.Celeste.Level.orig_LoadNewPlayer orig, Vector2 position, PlayerSpriteMode mode) {
        return new PicoPlayer(position, mode);
    }
    #pragma warning restore

    private static PlayerDeadBody PicoDie(On.Celeste.Player.orig_Die orig, Player self, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats)
    {
        return orig(self, self is PicoPlayer player && player.Overriding ? Vector2.Zero : direction, evenIfInvincible, registerDeathInStats);
    }

    private void AllowPicoBoosting(ILContext il) {
        ILCursor cur = new(il);

        cur.GotoNext(MoveType.After,
            instr => instr.MatchCallvirt<StateMachine>("get_State"),
            instr => instr.MatchLdcI4(out _)
        );
        ILLabel label = null;
        cur.GotoNext(MoveType.After,
            instr => instr.MatchBeq(out label)
        );

        cur.MoveAfterLabels();
        cur.EmitLdarg0();
        cur.EmitLdfld(BoosterCoroType.GetField("<>4__this")!);
        cur.EmitLdarg0();
        cur.EmitLdfld(BoosterCoroType.GetField("player")!);
        cur.EmitDelegate(CheckIfBoosting);
        cur.EmitBrtrue(label!);
    }

    private static readonly Type BoosterCoroType =
        typeof(Booster)
            .GetNestedType("<BoostRoutine>d__31", BindingFlags.NonPublic)!;
    
    private static bool CheckIfBoosting(Booster self, Player player) {
        if (player is not PicoPlayer { Overriding: true } picoPlayer) return false;
        return picoPlayer.CurrentBooster == self;
    }

    private static void FixBoost(On.Celeste.Player.orig_Boost orig, Player self, Booster booster) {
        if (self is PicoPlayer { Overriding: true } picoPlayer) {
            if (picoPlayer.BoostTimer > 0 || picoPlayer.CurrentBooster != null) return;
            picoPlayer.BoostTimer = 0.4f;
        }

        orig(self, booster);
    }
    
    private static void FixRedBoost(On.Celeste.Player.orig_RedBoost orig, Player self, Booster booster) {
        if (self is PicoPlayer { Overriding: true } picoPlayer) {
            if (picoPlayer.BoostTimer > 0 || picoPlayer.CurrentBooster != null) return;
            picoPlayer.BoostTimer = 0.4f;
        }
        orig(self, booster);
    }

    private static bool FixTransitionJank(On.Celeste.Player.orig_TransitionTo orig, Player self, Vector2 target, Vector2 direction)
    {
        var done = orig(self, target, direction);
        if (!done || self is not PicoPlayer { Overriding: true }) return done;

        self.Position.X = Math.Clamp(self.Position.X, self.level.Bounds.Left - 1, self.level.Bounds.Right - 7);
        self.Position.Y = Math.Clamp(self.Position.Y, self.level.Bounds.Top - 1, self.level.Bounds.Bottom - 7);

        if (direction.Y < 0)
            self.Speed.Y = Math.Min(self.Speed.Y, -150);
        
        return true;
    }
    

    [Command("picoplayer", "Force the player into PICO-8 mode, or back. Modes: swap, on, off")]
    private static void PicoPlayer(string mode = "") {
        var scene = Celeste.Instance.scene;
        if (scene is not Level level) return;
        foreach (var player in level.Tracker.GetEntities<Player>()) {
            if (player is not PicoPlayer picoPlayer) continue;
            switch (mode) {
                case "swap":
                    Engine.Commands.Log($"Swapping {(picoPlayer.Overriding ? "from" : "to")} PICO-8 mode");
                    picoPlayer.Overriding = !picoPlayer.Overriding;
                    return;
                case "off":
                    Engine.Commands.Log($"Switching from PICO-8 mode");
                    picoPlayer.Overriding = false;
                    return;
                case "on":
                    Engine.Commands.Log($"Switching to PICO-8 mode");
                    picoPlayer.Overriding = true;
                    return;
                default:
                    Engine.Commands.Log("Modes: swap, on, off");
                    return;
            }
        }
        Engine.Commands.Log("No PicoPlayer found!");
    }

    [Command("picowarp", "Warps to a level in PICO-8.")]
    private static void PicoWarp(int level = 0) {
        var scene = Celeste.Instance.scene;
        if (scene is not Emulator emu) return;
        emu.game.load_room(level % 8, level / 8);
    }
}