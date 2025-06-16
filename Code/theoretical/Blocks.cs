// Some default, Simple block definitions
public static class Blocks {
    [RegisterBlock( 0 )]
    public static Block Air { get; } = new Block { Name = "Air", Opaque = false, IsSolidBlock = false, IsSolid = false };
    [RegisterBlock( 1 )]
    public static Block Stone { get; } = new Block { Name = "Stone", Drops = [new ItemStack( Items.Stone )] };
    [RegisterBlock( 2 )]
    public static Block Dirt { get; } = new Block { Name = "Dirt", TextureIndex = new Vector2Int( 1, 0 ), Drops = [new ItemStack( Items.Dirt )] };
    [RegisterBlock( 3 )]
    public static Block Grass { get; } = new Block { Name = "Grass", TextureIndex = new Vector2Int( 2, 0 ), BottomTextureIndex = new Vector2Int( 1, 0 ), TopTextureIndex = new Vector2Int( 2, 0 ), SideTextureIndex = new Vector2Int( 3, 0 ), Drops = [new ItemStack( Items.Dirt )] };
}