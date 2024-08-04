using System;
using System.Reflection;
using Celeste.Pico8;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Cil;

namespace Celeste.Mod.PicoPlayer;

public class PicoPlayerModule : EverestModule {
    public static PicoPlayerModule Instance { get; private set; }

    public override Type SettingsType => typeof(PicoPlayerModuleSettings);
    public static PicoPlayerModuleSettings Settings => (PicoPlayerModuleSettings) Instance._Settings;

    public override Type SessionType => typeof(PicoPlayerModuleSession);
    public static PicoPlayerModuleSession Session => (PicoPlayerModuleSession) Instance._Session;

    public override Type SaveDataType => typeof(PicoPlayerModuleSaveData);
    public static PicoPlayerModuleSaveData SaveData => (PicoPlayerModuleSaveData) Instance._SaveData;

    public PicoPlayerModule() {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(PicoPlayerModule), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(PicoPlayerModule), LogLevel.Info);
#endif
    }

    #nullable enable
    public bool ExtVarsLoaded;

    public override void Load()
    {
        On.Celeste.Player.TransitionTo += FixTransitionJank;
        On.Celeste.Player.Die += PicoDie;
        IL.Celeste.Player.ctor += PatchHitbox;
        
        EverestModuleMetadata extVars = new() {
            Name = "ExtendedVariantMode",
            Version = new Version(0, 38 ,0)
        };

        ExtVarsLoaded = Everest.Loader.DependencyLoaded(extVars);
    }
    
#nullable disable

    public override void Unload()
    {
        On.Celeste.Player.TransitionTo -= FixTransitionJank;
        On.Celeste.Player.Die -= PicoDie;
        IL.Celeste.Player.ctor -= PatchHitbox;

    }

    private static PlayerDeadBody PicoDie(On.Celeste.Player.orig_Die orig, Player self, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats)
    {
        return orig(self, self is PicoPlayer ? Vector2.Zero : direction, evenIfInvincible, registerDeathInStats);
    }

    private static bool FixTransitionJank(On.Celeste.Player.orig_TransitionTo orig, Player self, Vector2 target, Vector2 direction)
    {
        var done = orig(self, target, direction);
        if (!done || self is not Mod.PicoPlayer.PicoPlayer) return done;
        
        Logger.Log(LogLevel.Error, "!", "YO");
        self.Position += direction * 4;
        return true;
    }

    private static readonly string[] HitboxNames = new[]
    {
        "normalHitbox", "duckHitbox", "normalHurtbox", "duckHurtbox", "starFlyHitbox", "starFlyHurtbox"
    };

    private static void PatchHitbox(ILContext il) {
        ILCursor cur = new(il);

        foreach (var hitboxName in HitboxNames) {
            cur.GotoNext(MoveType.Before,
                instr => instr.MatchStfld<Player>(hitboxName)
            );
            cur.MoveAfterLabels();
            cur.EmitLdarg0();
            cur.EmitDelegate(PatchPlayerHitbox);
        }
    }

    private static Hitbox PatchPlayerHitbox(Hitbox hitbox, Player player) => 
        player is PicoPlayer ? Mod.PicoPlayer.PicoPlayer.PicoHitbox : hitbox;
    
    

    [Command("picoplayer", "Force the player into PICO-8 mode, or back.")]
    private static void PicoPlayer(int mode = -1)
    {
        var scene = Celeste.Instance.scene;
        if (scene is not Level level) return;
        foreach (Player player in level.Tracker.GetEntities<Player>())
        {
            if (player is PicoPlayer picoPlayer && mode != 1)
            {
                picoPlayer.RemoveSelf();
                var newPlayer = new Player(picoPlayer.Position + Vector2.UnitY * 8, picoPlayer.DefaultSpriteMode);
                newPlayer.Speed = picoPlayer.Speed;
                newPlayer.StateMachine.State = Player.StNormal;
                newPlayer.IntroType = picoPlayer.IntroType;
                level.Add(newPlayer);
                break;
            }

            if (mode == 1) continue;
            player.RemoveSelf();
            var newPicoPlayer = new PicoPlayer(player.Position - Vector2.UnitY * 8, player.DefaultSpriteMode);
            newPicoPlayer.StateMachine.RemoveSelf();
            level.Add(newPicoPlayer);
        }
    }

    [Command("picowarp", "Warps to a level in PICO-8.")]
    private static void PicoWarp(int level = 0) {
        var scene = Celeste.Instance.scene;
        if (scene is not Emulator emu) return;
        emu.game.load_room(level % 8, level / 8);
    }
}