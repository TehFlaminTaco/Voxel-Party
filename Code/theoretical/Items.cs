public static class Items {
    [RegisterItem( 0x01 )]
    public static Item Stone { get; } = new BlockItem { BlockID = 0x01 };
    [RegisterItem( 0x02 )]
    public static Item Dirt { get; } = new BlockItem { BlockID = 0x02 };
    [RegisterItem( 0x03 )]
    public static Item Grass { get; } = new BlockItem { BlockID = 0x03 };
}