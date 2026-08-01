using MonoMod.ModInterop;

namespace Celeste.Mod.Picoline.Interop;

[ModExportName("Picoline")]
public static class PicolineExports {
    public static bool PlayerIsPico() => PicolineModule.Session?.WasPicolineOnRoomEnter ?? false;
}