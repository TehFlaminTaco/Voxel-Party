using Sandbox;

public struct BlockData {
	public byte BlockID;
	public byte BlockDataValue;

	public BlockData( byte blockID, byte blockDataValue ) {
		BlockID = blockID;
		BlockDataValue = blockDataValue;
	}
	
	public static BlockData[,,] GetAreaInBox( Vector3Int position, Vector3Int size )
	{
		BlockData[,,] area = new BlockData[size.x, size.y, size.z];
		for ( var x = 0; x < size.x; x++ )
		{
			for ( var y = 0; y < size.x; y++ )
			{
				for ( var z = 0; z < size.x; z++ )
				{
					area[x,y,z] = World.Active.GetBlock( position + new Vector3Int( x, y, z ) );
				}
			}
		}

		return area;
	}

	public BlockData( int blockType ) : this( (byte)blockType, 0 ) { }

	public Block GetBlock() {
		return ItemRegistry.GetBlock( BlockID );
	}
}
