namespace Movers
{
    // What a breakable thing is made of. It decides how hard a hit has to be before it hurts,
    // how much the thing can take, how much a blast bites into it, and which sound it makes
    // when it goes (see the destruction material table).
    // Other systems store and compare these values: add new ones at the end, never reorder.
    public enum BreakMaterial { Glass, Ceramic, Plastic, Wood, Fabric, Metal, Stone, Plaster, Brick, Concrete }
}
