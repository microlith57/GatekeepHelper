using System;
using System.Linq;
using Celeste.Mod.Entities;
using Monocle;
using Microsoft.Xna.Framework;
using System.Collections.Generic;


namespace Celeste.Mod.GatekeepHelper.Entities {
  [CustomEntity("GatekeepHelper/FullyCustomGemDoor")]
  public class FullyCustomGemDoor : GenericHeartDoor {
    public string[] Flags;
    public string[] CloseFlags;

    public FullyCustomGemDoor(EntityData data, Vector2 offset, EntityID entityID) : base(data, offset, entityID) {
      Color = Calc.HexToColor(data.Attr("color"));
      Flags = data.Attr("flags").Split(',');
      CloseFlags = data.Attr("close_flags").Split(',');
      string[] IconNames = data.Attr("icons").Split(',');

      IconFillProgress = new float[Flags.Length];
      Icons = new List<MTexture>[Flags.Length];
      for (int i = 0; i < Flags.Length; i++) {
        Icons[i] = GFX.Game.GetAtlasSubtextures(IconNames[i % IconNames.Length]);
        IconFillProgress[i] = 0f;
      }
    }

    public override bool ShouldTryClose =>
      CloseFlags.Length >= 1
      && CloseFlags.All((flag) => FlagSet(flag));

    public override bool ShouldFillIcon(int id) {
      return FlagSet(Flags[id]);
    }

    private bool FlagSet(string flag) {
      if (flag.StartsWith("!")) {
        return !(base.Scene as Level).Session.GetFlag(flag.Remove(0, 1));
      }
      return (base.Scene as Level).Session.GetFlag(flag);
    }
  }
}
