module GatekeepHelperFlagSummitGem

using ..Ahorn, Maple

# Thanks to Communal Helper for most of this implementation

@mapdef Entity "GatekeepHelper/FlagSummitGem" FlagSummitGem(
    x::Integer,
    y::Integer,
    index::Integer=0,
    sprite::String="",
    flag::String="",
    particleColor::String="",
)

const placements = Ahorn.PlacementDict(
    "Flag Summit Gem (Gatekeep Helper)" => Ahorn.EntityPlacement(
        FlagSummitGem,
    ),
)

const sprites = ["collectables/summitgems/$i/gem00" for i in 0:7]

function getSprite(index)
    if index > length(sprites)
        return sprites[end]
    end
    return sprites[index]
end

# positive numbers only
function getClampedIndex(entity::FlagSummitGem)
    index = Int(get(entity.data, "index", 0))
    if index < 0
        index = 0
    end
    entity.data["index"] = index
    return index
end

function Ahorn.selection(entity::FlagSummitGem)
    x, y = Ahorn.position(entity)

    return Ahorn.getSpriteRectangle(getSprite(getClampedIndex(entity) + 1), x, y)
end

Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::FlagSummitGem) =
    Ahorn.drawSprite(ctx, getSprite(getClampedIndex(entity) + 1), 0, 0)
end
