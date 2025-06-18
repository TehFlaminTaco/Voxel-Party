using Sandbox;

public struct BlockData
{
	public byte BlockID;
	public byte BlockDataValue;

	public BlockData( byte blockID, byte blockDataValue )
	{
		BlockID = blockID;
		BlockDataValue = blockDataValue;
	}

	public static BlockData[,,] GetAreaInBox( Vector3Int position, Vector3Int size )
	{
		if ( size.x <= 0 || size.y <= 0 || size.z <= 0 )
		{
			throw new System.ArgumentException( "Size must be greater than zero in all dimensions." );
		}
		BlockData[,,] area = new BlockData[size.x, size.y, size.z];
		for ( var x = 0; x < size.x; x++ )
		{
			for ( var y = 0; y < size.y; y++ )
			{
				for ( var z = 0; z < size.z; z++ )
				{
					area[x, y, z] = World.Active.GetBlock( position + new Vector3Int( x, y, z ) );
				}
			}
		}

		return area;
	}

	public BlockData( int blockType ) : this( (byte)blockType, 0 ) { }

	public Block GetBlock()
	{
		return ItemRegistry.GetBlock( BlockID );
	}

	// Equality operator to compare two BlockData instances
	public static bool operator ==( BlockData left, BlockData right )
	{
		return left.BlockID == right.BlockID && left.BlockDataValue == right.BlockDataValue;
	}
	public static bool operator !=( BlockData left, BlockData right )
	{
		return left.BlockID != right.BlockID || left.BlockDataValue != right.BlockDataValue;
	}

	public override bool Equals( object obj )
	{
		if ( obj is BlockData other )
		{
			return this == other;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return (BlockID << 8) | BlockDataValue; // Combine BlockID and BlockDataValue into a single hash code
	}
}
