using System;
using MonoMod.ModInterop;

namespace Celeste.Mod.Picoline;

public class PicolineModule : EverestModule {
    public static PicolineModule Instance { get; private set; } = null!;

    public override Type SessionType => typeof(PicolineModuleSession);
    public static PicolineModuleSession Session => (PicolineModuleSession)Instance._Session;

    public PicolineModule() {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(Picoline), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(Picoline), LogLevel.Info);
#endif

        EverestModuleMetadata extVars = new() {
            Name = "ExtendedVariantMode",
            Version = new Version(0, 47 ,0)
        };
        
        ExtVarsLoaded = Everest.Loader.DependencyLoaded(extVars);
    }
    
    internal static bool ExtVarsLoaded;
    internal static bool ShouldBePicoline;

    public override void Load() {
        typeof(Interop.PicolineExports).ModInterop();
        LifecycleMethods.OnLoad();
    }

    public override void Unload() {
        LifecycleMethods.OnUnload();
    }
}
