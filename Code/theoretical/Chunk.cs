public class Chunk
{
	public static readonly Vector3Int SIZE = new Vector3Int( 16, 16, 16 );

	public Vector3Int Position;

	private BlockData[,,] blocks = new BlockData[SIZE.x, SIZE.y, SIZE.z];
	public bool IsEmpty = true; // Flag to indicate if the chunk is empty. This should be set to false when any non-air is added.

	public ChunkObject ChunkObject { get; set; } = null;

	public bool IsRendered => ChunkObject != null && ChunkObject.IsValid();

	public int X => Position.x;
	public int Y => Position.y;
	public int Z => Position.z;
	public bool Dirty = true;
	public BlockSpace world;

	public Chunk( BlockSpace world, Vector3Int position )
	{
		Position = position;
		this.world = world;
	}

	public override string ToString()
	{
		return $"Chunk({X}, {Y}, {Z})";
	}

	public BlockData GetBlock( int x, int y, int z )
	{
		// Ensure the coordinates are within the chunk's bounds
		if ( x < 0 || x >= SIZE.x || y < 0 || y >= SIZE.y || z < 0 || z >= SIZE.z )
		{
			throw new System.ArgumentOutOfRangeException( $"Coordinates ({x}, {y}, {z}) are out of bounds for chunk at ({X}, {Y}, {Z})." );
		}
		return blocks[x, y, z];
	}

	public void MarkDirty()
	{
		Dirty = true; // Mark the chunk as dirty to indicate it needs to be updated/rendered.
	}

	public void SetBlock( int x, int y, int z, BlockData blockData )
	{
		// Ensure the coordinates are within the chunk's bounds
		if ( x < 0 || x >= SIZE.x || y < 0 || y >= SIZE.y || z < 0 || z >= SIZE.z )
		{
			throw new System.ArgumentOutOfRangeException( $"Coordinates ({x}, {y}, {z}) are out of bounds for chunk at ({X}, {Y}, {Z})." );
		}
		if ( blockData.BlockID != 0 )
		{
			IsEmpty = false; // If we set a non-air block, the chunk is no longer empty.
		}
		blocks[x, y, z] = blockData;
		Dirty = true; // Mark the chunk as dirty to indicate it needs to be updated/rendered.
					  // If we're on a chunk border (x y or z is 0 or SIZE-1), we might need to update neighboring chunks.
		if ( x == 0 )
			world.GetChunkIfExists( Position + new Vector3Int( -1, 0, 0 ) )?.MarkDirty();
		if ( x == SIZE.x - 1 )
			world.GetChunkIfExists( Position + new Vector3Int( 1, 0, 0 ) )?.MarkDirty();
		if ( y == 0 )
			world.GetChunkIfExists( Position + new Vector3Int( 0, -1, 0 ) )?.MarkDirty();
		if ( y == SIZE.y - 1 )
			world.GetChunkIfExists( Position + new Vector3Int( 0, 1, 0 ) )?.MarkDirty();
		if ( z == 0 )
			world.GetChunkIfExists( Position + new Vector3Int( 0, 0, -1 ) )?.MarkDirty();
		if ( z == SIZE.z - 1 )
			world.GetChunkIfExists( Position + new Vector3Int( 0, 0, 1 ) )?.MarkDirty();
	}

	public ChunkObject Render( Scene scene, WorldThinker thinker = null )
	{
		// Create a chunk object and then ask it to update.
		if ( IsEmpty ) return null; // Never render empty chunks.
		var obj = new GameObject( true, $"Chunk ({Position.x}, {Position.y}, {Position.z})" );
		obj.Parent = (thinker ?? scene.Get<WorldThinker>()).GameObject;
		var chunkObj = obj.AddComponent<ChunkObject>();
		chunkObj.ChunkPosition = Position; // Set the chunk position in world coordinates.
		obj.WorldPosition = Helpers.VoxelToWorld( Position * SIZE );
		ChunkObject = chunkObj;
		obj.Network.AssignOwnership( Connection.Host );
		obj.NetworkSpawn();
		return chunkObj;
	}

	public IEnumerable<byte> Serialize()
	{
		var data = new List<byte>();
		for ( int z = 0; z < SIZE.z; z++ )
		{
			for ( int y = 0; y < SIZE.y; y++ )
			{
				for ( int x = 0; x < SIZE.x; x++ )
				{
					var blockData = GetBlock( x, y, z );
					data.Add( blockData.BlockID );
					data.Add( blockData.BlockDataValue );
				}
			}
		}
		return data.RunLengthEncodeBy( 2 );
	}

	public void Deserialize( IEnumerable<byte> data )
	{
		var dataList = data.RunLengthDecodeBy( 2 ).ToList();
		if ( dataList.Count != SIZE.x * SIZE.y * SIZE.z * 2 )
		{
			Log.Warning( $"Invalid chunk data length: {dataList.Count}. Expected {SIZE.x * SIZE.y * SIZE.z * 2}." );
			return;
		}
		for ( int z = 0; z < SIZE.z; z++ )
		{
			for ( int y = 0; y < SIZE.y; y++ )
			{
				for ( int x = 0; x < SIZE.x; x++ )
				{
					int index = (z * SIZE.y * SIZE.x + y * SIZE.x + x) * 2;
					var blockID = dataList[index];
					var blockDataValue = dataList[index + 1];
					SetBlock( x, y, z, new BlockData( blockID, blockDataValue ) );
				}
			}
		}
	}
}
