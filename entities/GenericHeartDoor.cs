using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Celeste;
using Monocle;
using Celeste.Mod.Entities;

namespace Celeste.Mod.GatekeepHelper.Entities {
  public abstract class GenericHeartDoor : Entity {
    #region particles
    private struct Particle {
      public Vector2 Position;
      public float Speed;
      public Color Color;
    }

    private class WhiteLine : Entity {
      private float fade = 1f;
      private float DoorWidth;

      public WhiteLine(Vector2 origin, float DoorWidth) : base(origin) {
        base.Depth = -1000000;
        this.DoorWidth = DoorWidth;
      }

      public override void Update() {
        base.Update();
        fade = Calc.Approach(fade, 0f, Engine.DeltaTime);
        if (fade <= 0f) {
          RemoveSelf();
          Level level = SceneAs<Level>();
          for (float vslice = (int)(level.Camera.Left - 10f); vslice < (level.Camera.Right + 10f); vslice += 1f) {
            if (vslice < base.X || vslice >= base.X + DoorWidth) {
              level.Particles.Emit(HeartGemDoor.P_Slice, new Vector2(vslice, base.Y));
            }
          }
        }
      }

      public override void Render() {
        Vector2 position = (base.Scene as Level).Camera.Position;
        float height = Math.Max(1f, 4f * fade);
        Draw.Rect(position.X - 10f, base.Y - height / 2f, 340f, height, Color.White);
      }
    }
    #endregion

    public readonly EntityID entityID;

    public readonly float DoorWidth;
    public readonly float DoorOpenDistance = 32f;
    public float OpenFactor { get; private set; }
    public float CurrentOpenDistance => (DoorOpenDistance * OpenFactor);

    private Solid TopSolid;
    private Solid BotSolid;
    private float topClosedY;
    private float botClosedY;
    // private bool startHidden;

    private Vector2 mist;
    private float PatternOffset = 0f;
    private Particle[] particles = new Particle[50];
    private MTexture temp = new MTexture();

    public float[] IconFillProgress;
    public List<MTexture>[] Icons;
    public int NumIcons => IconFillProgress.Length;
    private float IconAlpha = 1f;
    public virtual float IconFillSpeed => Math.Max((float)NumIcons * 0.8f, 0.8f);
    public virtual float IconEmptySpeed => Math.Max((float)NumIcons * 4f, 4f);

    private Coroutine FillIconsCoroutine;
    private Coroutine EmptyIconsCoroutine;

    public Color Color = Calc.HexToColor("18668f");

    public bool FullyClosed => (OpenFactor <= 0f);
    public bool FullyOpen => (OpenFactor >= 1f);
    public bool Sealed = true;

    public GenericHeartDoor(EntityData data, Vector2 offset, EntityID entityID) : base(data.Position + offset) {
      this.entityID = entityID;
      DoorWidth = data.Width;

      // If this door has a node, use it for open distance
      Vector2? vector = data.FirstNodeNullable(offset);
      if (vector.HasValue) {
        DoorOpenDistance = Math.Abs(vector.Value.Y - base.Y);
      }

      // startHidden = data.Bool("startHidden");

      int requirement = data.Int("requires");
      List<MTexture> icon = GFX.Game.GetAtlasSubtextures("objects/heartdoor/icon");

      IconFillProgress = new float[requirement];
      Icons = new List<MTexture>[requirement];
      for (int i = 0; i < requirement; i++) {
        Icons[i] = icon;
        IconFillProgress[i] = 0f;
      }

      Add(new CustomBloom(RenderBloom));
    }

    internal static void Load() {
    }

    internal static void Unload() {
    }

    #region logic
    public override void Added(Scene scene) {
      base.Added(scene);
      Level level = scene as Level;

      // Fill up particles
      for (int i = 0; i < particles.Length; i++) {
        particles[i].Position = new Vector2(Calc.Random.NextFloat(DoorWidth), Calc.Random.NextFloat(level.Bounds.Height));
        particles[i].Speed = Calc.Random.Range(4, 12);
        particles[i].Color = Color.White * Calc.Random.Range(0.2f, 0.6f);
      }

      // Add the solid parts of the door
      level.Add(TopSolid = new Solid(
        new Vector2(base.X, level.Bounds.Top - 32),
        DoorWidth,
        base.Y - (float)level.Bounds.Top + 32f,
        safe: true));
      TopSolid.SurfaceSoundIndex = 32;
      TopSolid.SquishEvenInAssistMode = true;
      TopSolid.EnableAssistModeChecks = false;
      topClosedY = TopSolid.Y;

      level.Add(BotSolid = new Solid(
        new Vector2(base.X, base.Y),
        DoorWidth,
        (float)level.Bounds.Bottom - base.Y + 32f,
        safe: true));
      BotSolid.SurfaceSoundIndex = 32;
      BotSolid.SquishEvenInAssistMode = true;
      BotSolid.EnableAssistModeChecks = false;
      botClosedY = BotSolid.Y;

      // Open the door instantly if its flag is set when entering the room
      if (level.Session.GetFlag("gatekeep_door_" + entityID)) {
        Visible = true;
        OpenFactor = 1f;
        Sealed = false;
        for (int i = 0; i < NumIcons; i++) {
          IconFillProgress[i] = 1f;
        }
        TopSolid.Y -= DoorOpenDistance;
        BotSolid.Y += DoorOpenDistance;
      }

      // Add the routine
      Add(new Coroutine(IdleRoutine()));
      Add(FillIconsCoroutine = new Coroutine(FillIconsRoutine()));
      Add(EmptyIconsCoroutine = new Coroutine(EmptyIconsRoutine()));
    }

    public override void Awake(Scene scene) {
      base.Awake(scene);
    }

    public override void Update() {
      base.Update();
      if (Sealed) {
        PatternOffset = (PatternOffset + 12f * Engine.DeltaTime) % 8f;
        mist.X -= 4f * Engine.DeltaTime;
        mist.Y -= 24f * Engine.DeltaTime;
        for (int i = 0; i < particles.Length; i++) {
          particles[i].Position.Y += particles[i].Speed * Engine.DeltaTime;
        }
      }
    }

    #region routines
    public virtual IEnumerator IdleRoutine() {
      Level level = Scene as Level;
      while (true) {
        if (FullyClosed && PlayerInOpenRange && ShouldTryOpen) {
          yield return new SwapImmediately(OpenDoorRoutine());
        } else if (!FullyClosed && ShouldTryClose) {
          yield return new SwapImmediately(CloseDoorRoutine());
        }

        yield return null;
      }
    }

    public virtual IEnumerator OpenDoorRoutine() {
      Remove(FillIconsCoroutine, EmptyIconsCoroutine);
      Level level = Scene as Level;
      yield return 0.5f;
      // Unseal the door
      Sealed = false;
      for (int i = 0; i < NumIcons; i++) {
        IconFillProgress[i] = 1f;
      }
      Scene.Add(new WhiteLine(Position, DoorWidth));
      level.Shake();
      Input.Rumble(RumbleStrength.Strong, RumbleLength.Long);
      level.Flash(Color.White * 0.5f);
      Audio.Play("event:/game/09_core/frontdoor_unlock", Position);
      level.Session.SetFlag("gatekeep_door_" + entityID);
      PatternOffset = 0f;
      yield return 0.6f;
      // Move the two solids apart
      float facInitial = OpenFactor;
      float facFinal = 1f;
      for (float t = 0f; t < 1f; t += Engine.DeltaTime) {
        level.Shake();
        float ease = Ease.CubeIn(t);
        OpenFactor = MathHelper.Lerp(facInitial, facFinal, ease);
        TopSolid.MoveToY(topClosedY - CurrentOpenDistance);
        BotSolid.MoveToY(botClosedY + CurrentOpenDistance);
        // sparkly
        if (t >= 0.4f && level.OnInterval(0.1f)) {
          for (int i = 4; i < DoorWidth; i += 4) {
            level.ParticlesBG.Emit(HeartGemDoor.P_Shimmer, 1, new Vector2(TopSolid.Left + (float)i + 1f, TopSolid.Bottom - 2f), new Vector2(2f, 2f), -(float)Math.PI / 2f);
            level.ParticlesBG.Emit(HeartGemDoor.P_Shimmer, 1, new Vector2(BotSolid.Left + (float)i + 1f, BotSolid.Top + 2f), new Vector2(2f, 2f), (float)Math.PI / 2f);
          }
        }
        yield return null;
      }
      // Ensure the door is fully open
      OpenFactor = 1f;
      TopSolid.MoveToY(topClosedY - DoorOpenDistance);
      BotSolid.MoveToY(botClosedY + DoorOpenDistance);
      yield return 0.5f;

    }

    public virtual IEnumerator CloseDoorRoutine() {
      Remove(FillIconsCoroutine, EmptyIconsCoroutine);
      Level level = Scene as Level;
      // Close the door
      Audio.Play("event:/new_content/game/10_farewell/heart_door", Position);
      level.Session.SetFlag("gatekeep_door_" + entityID, false);
      float facInitial = OpenFactor;
      float facFinal = 0f;
      for (float t = 0f; t < 1f; t += Engine.DeltaTime) {
        level.Shake();
        float ease = Ease.CubeIn(t);
        OpenFactor = MathHelper.Lerp(facInitial, facFinal, ease);
        TopSolid.MoveToY(topClosedY - CurrentOpenDistance);
        BotSolid.MoveToY(botClosedY + CurrentOpenDistance);
        yield return null;
      }
      // Seal the door
      Sealed = true;
      for (int i = 0; i < NumIcons; i++) {
        IconFillProgress[i] = 0f;
      }
      Add(FillIconsCoroutine, EmptyIconsCoroutine);
      Scene.Add(new WhiteLine(Position, DoorWidth));
      level.Shake();
      Input.Rumble(RumbleStrength.Strong, RumbleLength.Long);
      level.Flash(Color.White * 0.5f);
      // Ensure the door is fully closed
      OpenFactor = 0f;
      TopSolid.MoveToY(topClosedY);
      BotSolid.MoveToY(botClosedY);
      yield return 0.5f;
    }

    public virtual IEnumerator FillIconsRoutine() {
      if (NumIcons == 0) {
        yield break;
      }
      while (true) {
        float quota = Engine.DeltaTime * IconFillSpeed;

        // Fill icons until the quota is empty
        if (PlayerInOpenRange) {
          for (int i = 0; i < NumIcons; i++) {
            if (IconFillProgress[i] >= 1f || !ShouldFillIcon(i)) {
              continue;
            }
            if (IconFillProgress[i] <= 0f) {
              yield return 0.1f;
              Audio.Play("event:/game/09_core/frontdoor_heartfill", Position);
            }
            float used = Calc.Approach(0f, 1f - IconFillProgress[i], quota);
            IconFillProgress[i] += used;
            quota -= used;
            if (quota <= 0) {
              break;
            }
          }
        }
        yield return null;
      }
    }

    public virtual IEnumerator EmptyIconsRoutine() {
      if (NumIcons == 0) {
        yield break;
      }
      while (true) {
        float quota = Engine.DeltaTime * IconEmptySpeed;

        // Empty icons until the quota is empty
        for (int i = NumIcons - 1; i >= 0; i--) {
          if (IconFillProgress[i] <= 0f || (ShouldFillIcon(i) && PlayerInOpenRange)) {
            continue;
          }
          float used = Calc.Approach(0f, IconFillProgress[i], quota);
          IconFillProgress[i] -= used;
          quota -= used;
          if (quota <= 0) {
            break;
          }
        }
        yield return null;
      }
    }
    #endregion

    public virtual bool PlayerInCrushRange {
      get {
        Player player = Scene.Tracker.GetEntity<Player>();
        return player != null && Math.Abs(player.X - Center.X) < 100f;
      }
    }

    public virtual bool PlayerInOpenRange {
      get {
        Player player = Scene.Tracker.GetEntity<Player>();
        return player != null && Math.Abs(player.X - Center.X) < 80f && player.X < X;
      }
    }

    public virtual bool ShouldTryOpen => IconFillProgress.All(p => p >= 1f);

    public virtual bool ShouldTryClose => false;

    public virtual bool ShouldFillIcon(int id) {
      if (SaveData.Instance.CheatMode) {
        return true;
      }
      return SaveData.Instance.TotalHeartGems > id;
    }
    #endregion

    #region rendering
    public override void Render() {
      Color accent = (Sealed ? Color.White : (Color.White * 0.25f));
      // Draw bounding boxes
      if (Sealed && TopSolid.Visible && BotSolid.Visible) {
        Rectangle bounds = new Rectangle(
          (int)TopSolid.X,
          (int)TopSolid.Y,
          (int)DoorWidth,
          (int)(TopSolid.Height + BotSolid.Height));
        DrawInterior(bounds);
        DrawEdges(bounds, accent);
      } else {
        if (TopSolid.Visible) {
          Rectangle topBounds = new Rectangle(
            (int)TopSolid.X,
            (int)TopSolid.Y,
            (int)DoorWidth,
            (int)TopSolid.Height);
          DrawInterior(topBounds);
          DrawEdges(topBounds, accent);
        }
        if (BotSolid.Visible) {
          Rectangle botBounds = new Rectangle(
            (int)BotSolid.X,
            (int)BotSolid.Y,
            (int)DoorWidth,
            (int)BotSolid.Height);
          DrawInterior(botBounds);
          DrawEdges(botBounds, accent);
        }
      }

      // Draw icons
      if (IconAlpha <= 0f || NumIcons == 0) {
        return;
      }
      // Find out how many rows to display
      float iconWidth = 12f;
      int availableWidth = (int)((DoorWidth - 8) / iconWidth);
      int rows = (int)Math.Ceiling((float)NumIcons / (float)availableWidth);
      Color iconAccent = accent * IconAlpha;
      for (int rowNum = 0; rowNum < rows; rowNum++) {
        // Find how many icons there are in this row
        int rowIcons = (((rowNum + 1) * availableWidth < NumIcons) ? availableWidth : (NumIcons - rowNum * availableWidth));
        Vector2 basePos = new Vector2(base.X + (float)DoorWidth * 0.5f, base.Y) + new Vector2((float)(-rowIcons) / 2f + 0.5f, (float)(-rows) / 2f + (float)rowNum + 0.5f) * iconWidth;
        // Move icons away from split
        if (!FullyClosed || !Sealed) {
          if (rowNum < rows / 2) {
            basePos.Y -= CurrentOpenDistance + 8f;
          } else {
            basePos.Y += CurrentOpenDistance + 8f;
          }
        }
        // Draw icons in row
        for (int rowPos = 0; rowPos < rowIcons; rowPos++) {
          int icon = rowNum * availableWidth + rowPos;
          DrawIcon(icon,
                   basePos + new Vector2((float)rowPos * iconWidth, 0f),
                   iconAccent);
        }
      }
    }

    public virtual void DrawIcon(int index, Vector2 pos, Color col) {
      List<MTexture> icon = Icons[index];
      int sprite = (int)(Ease.CubeIn(IconFillProgress[index])
                   * (float)(icon.Count - 1));
      icon[sprite].DrawCentered(pos, col);
    }

    public virtual void RenderBloom() {
      if (Sealed && Visible) {
        DrawBloomRect(new Rectangle(
          (int)TopSolid.X,
          (int)TopSolid.Y,
          (int)DoorWidth,
          (int)(TopSolid.Height + BotSolid.Height)));
      }
    }

    public virtual void DrawBloomRect(Rectangle bounds) {
      Draw.Rect(bounds.Left - 4, bounds.Top, 2f, bounds.Height, Color.White * 0.25f);
      Draw.Rect(bounds.Left - 2, bounds.Top, 2f, bounds.Height, Color.White * 0.5f);
      Draw.Rect(bounds, Color.White * 0.75f);
      Draw.Rect(bounds.Right, bounds.Top, 2f, bounds.Height, Color.White * 0.5f);
      Draw.Rect(bounds.Right + 2, bounds.Top, 2f, bounds.Height, Color.White * 0.25f);
    }

    public virtual void DrawMist(Rectangle bounds, Vector2 mist) {
      Color color = Color.White * 0.6f;
      MTexture mTexture = GFX.Game["objects/heartdoor/mist"];
      int num = mTexture.Width / 2;
      int num2 = mTexture.Height / 2;
      for (int i = 0; i < bounds.Width; i += num) {
        for (int j = 0; j < bounds.Height; j += num2) {
          mTexture.GetSubtexture((int)Mod(mist.X, num), (int)Mod(mist.Y, num2), Math.Min(num, bounds.Width - i), Math.Min(num2, bounds.Height - j), temp);
          temp.Draw(new Vector2(bounds.X + i, bounds.Y + j), Vector2.Zero, color);
        }
      }
    }

    public virtual void DrawInterior(Rectangle bounds) {
      Draw.Rect(bounds, Color);
      DrawMist(bounds, mist);
      DrawMist(bounds, new Vector2(mist.Y, mist.X) * 1.5f);
      Vector2 camOffset = Vector2.Zero;
      if (Sealed) {
        camOffset = (base.Scene as Level).Camera.Position;
      }
      for (int i = 0; i < particles.Length; i++) {
        Vector2 particleOffset = particles[i].Position + camOffset * 0.2f;
        particleOffset.X = Mod(particleOffset.X, bounds.Width);
        particleOffset.Y = Mod(particleOffset.Y, bounds.Height);
        Draw.Pixel.Draw(new Vector2(bounds.X, bounds.Y) + particleOffset, Vector2.Zero, particles[i].Color);
      }
    }

    private void DrawEdges(Rectangle bounds, Color color) {
      MTexture mTexture = GFX.Game["objects/heartdoor/edge"];
      MTexture mTexture2 = GFX.Game["objects/heartdoor/top"];
      int patternOffsetPixels = (int)(PatternOffset % 8f);
      if (patternOffsetPixels > 0) {
        mTexture.GetSubtexture(0, 8 - patternOffsetPixels, 7, patternOffsetPixels, temp);
        temp.DrawJustified(new Vector2(bounds.Left + 4, bounds.Top), new Vector2(0.5f, 0f), color, new Vector2(-1f, 1f));
        temp.DrawJustified(new Vector2(bounds.Right - 4, bounds.Top), new Vector2(0.5f, 0f), color, new Vector2(1f, 1f));
      }
      for (int i = patternOffsetPixels; i < bounds.Height; i += 8) {
        mTexture.GetSubtexture(0, 0, 8, Math.Min(8, bounds.Height - i), temp);
        temp.DrawJustified(new Vector2(bounds.Left + 4, bounds.Top + i), new Vector2(0.5f, 0f), color, new Vector2(-1f, 1f));
        temp.DrawJustified(new Vector2(bounds.Right - 4, bounds.Top + i), new Vector2(0.5f, 0f), color, new Vector2(1f, 1f));
      }
      for (int j = 0; j < bounds.Width; j += 8) {
        mTexture2.DrawCentered(new Vector2(bounds.Left + 4 + j, bounds.Top + 4), color);
        mTexture2.DrawCentered(new Vector2(bounds.Left + 4 + j, bounds.Bottom - 4), color, new Vector2(1f, -1f));
      }
    }

    private float Mod(float x, float m) {
      return (x % m + m) % m;
    }
    #endregion
  }
}
