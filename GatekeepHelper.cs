using Monocle;
using System;
using Celeste.Mod.GatekeepHelper.Entities;
using Celeste.Mod.Entities;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace Celeste.Mod.GatekeepHelper {
  public class GatekeepHelper : EverestModule {
    public static GatekeepHelper Instance;

    public GatekeepHelper() {
      Instance = this;
    }

    public override void Load() {
      GenericHeartDoor.Load();
    }

    public override void Unload() {
      GenericHeartDoor.Unload();
    }

    public override void Initialize() {
    }

    public override void LoadContent(bool firstLoad) {
    }
  }
}
