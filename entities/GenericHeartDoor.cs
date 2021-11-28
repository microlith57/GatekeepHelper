using System;
using Microsoft.Xna.Framework;
using Celeste.Mod.Entities;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace Celeste.Mod.GatekeepHelper.Entities
{
  public abstract class GenericHeartDoor : HeartGemDoor
  {
    public string color = "18668f";

    public GenericHeartDoor(EntityData data, Vector2 offset, EntityID entityID) : base(data, offset)
    {
    }

    internal static void Load()
    {
    }

    internal static void Unload()
    {
    }

    internal static void LoadILHooks()
    {
      IL.Celeste.HeartGemDoor.DrawInterior += modDoorColor;
    }

    internal static void UnloadILHooks()
    {
      IL.Celeste.HeartGemDoor.DrawInterior -= modDoorColor;
    }

    private static void modDoorColor(ILContext il)
    {
      ILCursor cursor = new ILCursor(il);
      while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("18668f")))
      {
        Logger.Log("GatekeepHelper/GenericHeartDoor", $"Modding door at {cursor.Index} in IL for HeartGemDoor.DrawInterior");
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate<Func<string, HeartGemDoor, string>>((orig, self) =>
        {
          if (self is GenericHeartDoor door)
          {
            return door.color;
          }
          return orig;
        });
      }
    }
  }
}
