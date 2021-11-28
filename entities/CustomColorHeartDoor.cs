using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.GatekeepHelper.Entities
{
  [CustomEntity("GatekeepHelper/CustomColorHeartDoor")]
  public class CustomColorHeartDoor : GenericHeartDoor
  {
    public CustomColorHeartDoor(EntityData data, Vector2 offset, EntityID entityID) : base(data, offset, entityID)
    {
      color = data.Attr("color");
    }
  }
}
