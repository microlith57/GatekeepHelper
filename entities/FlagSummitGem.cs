using System;
using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using Celeste.Mod.Entities;
using Monocle;
using MonoMod.Utils;
using MonoMod.Cil;
using Mono.Cecil.Cil;

namespace Celeste.Mod.GatekeepHelper.Entities {
  /*
  Thanks to Communal Helper devs for most of this implementation
  */
  [CustomEntity("GatekeepHelper/FlagSummitGem")]
  public class FlagSummitGem : Entity {
    public readonly EntityID entityID;
    protected Color? particleColor;
    public string Flag;
    public int GemID;

    private Sprite sprite;

    public static readonly Color[] GemColors = SummitGem.GemColors;
    private static Type t_BgFlash = typeof(SummitGem).GetNestedType("BgFlash", BindingFlags.NonPublic);
    public static ParticleType P_Shatter = SummitGem.P_Shatter;

    private Wiggler scaleWiggler;
    private Wiggler moveWiggler;
    private Vector2 moveWiggleDir;
    private float bounceSfxDelay;

    // TODO: Support CommunalHelper GemColors
    // static FlagSummitGem() {
    //   if (Everest.Loader.DependencyLoaded(new EverestModuleMetadata { Name = "CommunalHelper", Version = new Version(1, 9, 4) })) {
    //     Type customSummitGemType = Everest.Modules.First(module => module.GetType().ToString() == "Celeste.Mod.CommunalHelper.CommunalHelperModule")
    //     .GetType().Assembly.GetType("Celeste.Mod.CommunalHelper.Entities.CustomSummitGem");
    //     GemColors = customSummitGemType.GetField("GemColors", BindingFlags.Public | BindingFlags.Static).;
    //     FieldInfo customSummitGemType = customSummitGemType.GetField("icon", BindingFlags.NonPublic | BindingFlags.Static);
    //   }
    // }

    public FlagSummitGem(EntityData data, Vector2 position, EntityID entityID) : base(data.Position + position) {
      this.entityID = entityID;

      GemID = data.Int("index");
      Flag = data.Attr("flag");
      base.Collider = new Hitbox(12f, 12f, -6f, -6f);

      // Handle custom sprite
      string spriteName = data.Attr("sprite");
      if (spriteName.Length > 0 && GFX.Game.Has(spriteName)) {
        sprite = new Sprite(GFX.Game, spriteName);
      } else {
        sprite = new Sprite(GFX.Game, "collectables/summitgems/" + GemID + "/gem");
      }
      sprite.AddLoop("idle", "", 0.08f);
      sprite.Play("idle");
      sprite.CenterOrigin();
      Add(sprite);

      Add(scaleWiggler = Wiggler.Create(0.5f, 4f, delegate (float f) {
        sprite.Scale = Vector2.One * (1f + f * 0.3f);
      }));
      moveWiggler = Wiggler.Create(0.8f, 2f);
      moveWiggler.StartZero = true;
      Add(moveWiggler);

      // Handle custom particle colour
      string colorName = data.Attr("particleColor");
      if (colorName.Length > 0) {
        particleColor = Calc.HexToColor(colorName);
      }

      Add(new PlayerCollider(OnPlayer));
    }

    private IEnumerator SmashRoutine(Player player, Level level) {
      Visible = false;
      Collidable = false;
      player.Stamina = 110f;
      SoundEmitter.Play(SFX.game_07_gem_get, this, null);

      Session session = (Scene as Level).Session;
      session.SetFlag(Flag);

      level.Shake(0.3f);
      Celeste.Freeze(0.1f);
      P_Shatter.Color = particleColor ?? GemColors[Calc.Clamp(GemID, 0, 7)];
      float angle = player.Speed.Angle();
      level.ParticlesFG.Emit(P_Shatter, 5, Position, Vector2.One * 4f, angle - Calc.QuarterCircle);
      level.ParticlesFG.Emit(P_Shatter, 5, Position, Vector2.One * 4f, angle + Calc.QuarterCircle);
      SlashFx.Burst(Position, angle);

      for (int i = 0; i < 10; i++) {
        Scene.Add(new AbsorbOrb(Position, player, null));
      }
      level.Flash(Color.White, true);
      Scene.Add((Entity)Activator.CreateInstance(t_BgFlash));

      Engine.TimeRate = 0.5f;
      while (Engine.TimeRate < 1f) {
        Engine.TimeRate = Calc.Approach(Engine.TimeRate, 1f, Engine.RawDeltaTime * 0.5f);
        yield return null;
      }

      RemoveSelf();
      yield break;
    }

    private void OnPlayer(Player player) {
      Level level = base.Scene as Level;
      if (player.DashAttacking) {
        Add(new Coroutine(SmashRoutine(player, level)));
        return;
      }
      player.PointBounce(base.Center);
      moveWiggler.Start();
      scaleWiggler.Start();
      moveWiggleDir = (base.Center - player.Center).SafeNormalize(Vector2.UnitY);
      Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
      if (bounceSfxDelay <= 0f) {
        Audio.Play("event:/game/general/crystalheart_bounce", Position);
        bounceSfxDelay = 0.1f;
      }
    }

    public override void Awake(Scene scene) {
      base.Awake(scene);

      if ((scene as Level).Session.GetFlag(Flag)) {
        RemoveSelf();
      }
    }

    public override void Update() {
      base.Update();
      bounceSfxDelay -= Engine.DeltaTime;
      sprite.Position = moveWiggleDir * moveWiggler.Value * -8f;
    }
  }
}
