using System;
using Celeste.Mod.Entities;
using Monocle;
using Microsoft.Xna.Framework;
using System.Collections.Generic;


namespace Celeste.Mod.GatekeepHelper.Entities {
  [CustomEntity("GatekeepHelper/FullyCustomGemDoor")]
  public class FullyCustomGemDoor : GenericHeartDoor {
    public string[] Flags;

    public FullyCustomGemDoor(EntityData data, Vector2 offset, EntityID entityID) : base(data, offset, entityID) {
      Color = Calc.HexToColor(data.Attr("color"));
      Flags = data.Attr("flags").Split(',');
      string[] IconNames = data.Attr("icons").Split(',');
      // ShouldCloseAfter = Calc.HexToColor(data.Bool("close_after"));

      IconFillProgress = new float[Flags.Length];
      Icons = new List<MTexture>[Flags.Length];
      for (int i = 0; i < Flags.Length; i++) {
        Icons[i] = GFX.Game.GetAtlasSubtextures(IconNames[i % IconNames.Length]);
        IconFillProgress[i] = 0f;
      }
    }

    public override bool ShouldTryClose {
      get {
        Player player = Scene.Tracker.GetEntity<Player>();
        return player != null && Math.Abs(player.X - Center.X) > (DoorWidth + 20f) && player.X > X;
      }
    }

    public override bool ShouldFillIcon(int id) {
      string flag = Flags[id];
      if (flag.StartsWith("!")) {
        return !(base.Scene as Level).Session.GetFlag(flag.Remove(0, 1));
      }
      return (base.Scene as Level).Session.GetFlag(flag);
    }
  }
}
