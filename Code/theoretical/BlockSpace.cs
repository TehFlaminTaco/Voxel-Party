using System;

public struct BlockTraceResult
{
	public float Distance;
	public Vector3 EndPosition;
	public Vector3Int HitBlockPosition;
	public Direction HitFace;
	public bool Hit;

}

public class BlockTrace
{
	public Func<Vector3Int, bool> IgnoreFilter { get; set; } = _ => false;
	public Vector3 Start { get; set; }
	public Vector3 Dir { get; set; }
	public float MaxDistance { get; set; } = 1024f;
	public World World { get; set; }
	public bool Debug { get; set; } = false;

	public BlockTrace WithIgnoreFilter( Func<Vector3Int, bool> filter )
	{
		this.IgnoreFilter = filter;
		return this;
	}

	public BlockTrace WithStart( Vector3 start )
	{
		this.Start = start / World.BlockScale; // Convert to grid coordinates
		return this;
	}

	public BlockTrace WithDirection( Vector3 direction )
	{
		this.Dir = direction.Normal;
		return this;
	}

	public BlockTrace WithDistance( float distance )
	{
		this.MaxDistance = distance / World.BlockScale; // Convert to grid coordinates
		return this;
	}

	public BlockTrace WithWorld( World world )
	{
		this.World = world;
		return this;
	}

	public BlockTrace WithDebug( bool debug = true )
	{
		this.Debug = debug;
		return this;
	}

	public BlockTraceResult Run()
	{
		// We use the Error method
		// We step along the way by moving along the direction vector a partial amount such that we hit the next axis border
		// Because blocks can have non-standard AABB, we then run a raycast against the block at that position to see if we hit it (Unless it's IsSolidBlock)
		// Process the face, check if it matches the filter or if we've stepped past the max distance.

		Vector3 pos = Start;
		Vector3Int bPos = pos.Floor(); // Get the block position in grid coordinates
		float distanceTraveled = 0f;
		if ( Debug )
		{
			Gizmo.Draw.Color = Color.Red;
			Gizmo.Draw.SolidSphere( pos * World.BlockScale, 0.1f * World.BlockScale );
		}
		int steps = 0;
		bool hit = false;
		Direction hitFace = Direction.None;
		while ( distanceTraveled < MaxDistance && steps++ < 100 )
		{
			// Get the fractional component of the position, and compare it to the next axis border.
			(float stride, hitFace) = pos.Fractional().Components().Zip( Dir.Components() ).Select( ab =>
			{
				var a = ab.First;
				var b = ab.Second;
				if ( b == 0 ) return float.MaxValue; // Avoid division by zero
				if ( b < 0 ) // if b < 0, we need to stride until the next a = 0. So we take a / b
					return a == 0 ? 1f / -b : a / -b;
				// Otherwise, we need to stride until the next a = 1. So we take (1 - a) / b
				return (1f - a) / b;
			} ).Zip( new[]{
				Dir.x > 0 ? Direction.North : Dir.x < 0 ? Direction.South : Direction.None,
				Dir.y > 0 ? Direction.Left : Dir.y < 0 ? Direction.Right : Direction.None,
				Dir.z > 0 ? Direction.Up : Dir.z < 0 ? Direction.Down : Direction.None
			} ).MinBy( ab => ab.First );
			if ( stride == 0 || stride == float.MaxValue )
			{
				// If stride is 0, we're already at the next axis border, so we can skip this step.
				// If stride is float.MaxValue, we're not moving in any direction, so we can also skip this step.
				break;
			}
			// Move the position along the direction vector by the stride amount.
			pos += Dir * stride;
			distanceTraveled += stride;
			bPos += hitFace.Forward(); // Move the block position in the direction of the hit face.

			if ( Debug )
			{
				Gizmo.Draw.Color = Color.Blue;
				Gizmo.Draw.SolidSphere( pos * World.BlockScale, 0.02f * World.BlockScale );
			}

			// Determine the block position and the face hit depending on our current pos
			// if any dir component is negative, we need to adjust the block position to the next block in that direction.
			if ( Debug )
			{
				Gizmo.Draw.Color = Color.Orange;
				Gizmo.Draw.LineBBox(
					BBox.FromPoints( [bPos * World.BlockScale, (bPos + Vector3.One) * World.BlockScale] )
				);
				Gizmo.Draw.Color = Color.Green;
				Gizmo.Draw.Arrow( pos * World.BlockScale, (pos + hitFace.Forward() * 0.5f) * World.BlockScale, 0.1f * World.BlockScale, 0.01f * World.BlockScale );
				Gizmo.Draw.Color = Color.Blue;
				Gizmo.Draw.Line( pos * World.BlockScale, (bPos + Vector3.One * 0.5f) * World.BlockScale );
			}

			// Determine which of our current axis fractions is closest to zero, and use that to determine the face hit.
			if ( !IgnoreFilter( bPos ) )
			{
				hit = true; // We hit a block that is not ignored.
				break;
			}
		}
		// if any dir component is negative, we need to adjust the block position to the next block in that direction.
		var frac = (pos + 0.5f).Fractional() - 0.5f;
		if ( Debug )
		{
			Gizmo.Draw.Color = Color.Yellow;
			Gizmo.Draw.SolidSphere( bPos * World.BlockScale, 0.1f * World.BlockScale );
			Gizmo.Draw.Color = Color.Orange;
			Gizmo.Draw.Line( pos * World.BlockScale, Start * World.BlockScale );
		}
		return new BlockTraceResult()
		{
			Distance = distanceTraveled,
			EndPosition = pos * World.BlockScale, // Convert back to world coordinates
			HitBlockPosition = bPos,
			HitFace = hitFace.Flip(),
			Hit = hit
		};
	}

}

public class BlockSpace
{
	// SimulatedChunks refer to chunks that have been loaded into memory, and thus can be interacted with.
	public Dictionary<Vector3Int, Chunk> SimulatedChunks { get; private set; } = new();

	public Chunk GetChunk( Vector3Int position )
	{
		if ( !SimulatedChunks.ContainsKey( position ) )
			if ( !LoadOrMakeChunk( position ) )
				throw new System.Exception( $"Failed to load or create chunk at {position}." );
		return SimulatedChunks[position];
	}

	public Chunk GetChunkIfExists( Vector3Int position )
	{
		if ( SimulatedChunks.TryGetValue( position, out var chunk ) )
			return chunk;
		return null; // Return null if the chunk does not exist
	}

	public BlockTrace Trace( Vector3 Start, Vector3 End )
	{
		return new BlockTrace()
			.WithStart( Start )
			.WithDirection( (End - Start).Normal )
			.WithDistance( (End - Start).Length )
			.WithWorld( this as World ) // Cast to World to access the World-specific methods
			.WithIgnoreFilter( pos => !GetBlock( pos ).GetBlock().IsSolid ); // Ignore air blocks
	}

	private bool LoadOrMakeChunk( Vector3Int position )
	{
		if ( SimulatedChunks.ContainsKey( position ) )
			return true; // Chunk already exists

		// For now, create a blank chunk.
		var chunk = new Chunk( this, position );
		return SimulatedChunks.TryAdd( position, chunk );
	}
	// Get the block in World coordinates.
	public BlockData GetBlock( Vector3Int position )
	{
		var chunkPosition = new Vector3Int(
			position.x.FloorDiv( Chunk.SIZE.x ),
			position.y.FloorDiv( Chunk.SIZE.y ),
			position.z.FloorDiv( Chunk.SIZE.z )
		);
		var chunk = GetChunk( chunkPosition );
		var blockPosition = new Vector3Int(
			((position.x % Chunk.SIZE.x) + Chunk.SIZE.x) % Chunk.SIZE.x,
			((position.y % Chunk.SIZE.y) + Chunk.SIZE.y) % Chunk.SIZE.y,
			((position.z % Chunk.SIZE.z) + Chunk.SIZE.z) % Chunk.SIZE.z
		);
		return chunk.GetBlock( blockPosition.x, blockPosition.y, blockPosition.z );
	}

	public void SetBlock( Vector3Int position, BlockData blockData )
	{
		var chunkPosition = new Vector3Int(
			position.x.FloorDiv( Chunk.SIZE.x ),
			position.y.FloorDiv( Chunk.SIZE.y ),
			position.z.FloorDiv( Chunk.SIZE.z )
		);
		var chunk = GetChunk( chunkPosition );
		var blockPosition = new Vector3Int(
			((position.x % Chunk.SIZE.x) + Chunk.SIZE.x) % Chunk.SIZE.x,
			((position.y % Chunk.SIZE.y) + Chunk.SIZE.y) % Chunk.SIZE.y,
			((position.z % Chunk.SIZE.z) + Chunk.SIZE.z) % Chunk.SIZE.z
		);
		chunk.SetBlock( blockPosition.x, blockPosition.y, blockPosition.z, blockData );
	}

	public string SerializeRegion( Vector3Int start, Vector3Int end )
	{
		int xMin = Math.Min( start.x, end.x );
		int xMax = Math.Max( start.x, end.x );
		int yMin = Math.Min( start.y, end.y );
		int yMax = Math.Max( start.y, end.y );
		int zMin = Math.Min( start.z, end.z );
		int zMax = Math.Max( start.z, end.z );


		List<byte> data = new();
		// Add the size of the region to the data.
		data.AddRange( BitConverter.GetBytes( xMax - xMin + 1 ) );
		data.AddRange( BitConverter.GetBytes( yMax - yMin + 1 ) );
		data.AddRange( BitConverter.GetBytes( zMax - zMin + 1 ) );
		List<byte> blocks = new();
		for ( int z = zMin; z <= zMax; z++ )
		{
			for ( int y = yMin; y <= yMax; y++ )
			{
				for ( int x = xMin; x <= xMax; x++ )
				{
					var blockData = GetBlock( new Vector3Int( x, y, z ) );
					blocks.Add( blockData.BlockID );
					blocks.Add( blockData.BlockDataValue );
				}
			}
		}
		data.AddRange( blocks.RunLengthEncodeBy( 2 ) );
		return Convert.ToBase64String( data.ToArray() );
	}

	public Vector3Int GetStructureBounds( string structureData )
	{
		var bytes = Convert.FromBase64String( structureData );
		if ( bytes.Length < 12 )
		{
			Log.Error( "Invalid structure data." );
			return new Vector3Int( 0, 0, 0 );
		}
		int xSize = BitConverter.ToInt32( bytes, 0 );
		int ySize = BitConverter.ToInt32( bytes, 4 );
		int zSize = BitConverter.ToInt32( bytes, 8 );
		return new Vector3Int( xSize, ySize, zSize );
	}

	public BlockData[,,] GetStructureData( string structureData )
	{
		var bytes = Convert.FromBase64String( structureData );
		if ( bytes.Length < 12 )
		{
			return null;
		}
		int xSize = BitConverter.ToInt32( bytes, 0 );
		int ySize = BitConverter.ToInt32( bytes, 4 );
		int zSize = BitConverter.ToInt32( bytes, 8 );
		var blockData = bytes.Skip( 12 ).RunLengthDecodeBy( 2 ).ToList();
		if ( blockData.Count != xSize * ySize * zSize * 2 )
		{
			return null;
		}

		BlockData[,,] structure = new BlockData[xSize, ySize, zSize];

		for ( int z = 0; z < zSize; z++ )
		{
			for ( int y = 0; y < ySize; y++ )
			{
				for ( int x = 0; x < xSize; x++ )
				{
					int index = (z * ySize * xSize + y * xSize + x) * 2;
					byte blockID = blockData[index];
					byte blockDataValue = blockData[index + 1];
					structure[x, y, z] = new BlockData( blockID, blockDataValue );
				}
			}
		}
		return structure;
	}

	public BlockData[,,] LoadStructure( Vector3Int location, string structureData )
	{
		var bytes = Convert.FromBase64String( structureData );
		if ( bytes.Length < 12 )
		{
			Log.Error( "Invalid structure data." );
			return null;
		}
		int xSize = BitConverter.ToInt32( bytes, 0 );
		int ySize = BitConverter.ToInt32( bytes, 4 );
		int zSize = BitConverter.ToInt32( bytes, 8 );
		var blockData = bytes.Skip( 12 ).RunLengthDecodeBy( 2 ).ToList();
		if ( blockData.Count != xSize * ySize * zSize * 2 )
		{
			Log.Error( $"Invalid structure data length: {blockData.Count}. Expected {xSize * ySize * zSize * 2}." );
			return null;
		}

		for ( int z = 0; z < zSize; z++ )
		{
			for ( int y = 0; y < ySize; y++ )
			{
				for ( int x = 0; x < xSize; x++ )
				{
					int index = (z * ySize * xSize + y * xSize + x) * 2;
					byte blockID = blockData[index];
					byte blockDataValue = blockData[index + 1];
					SetBlock( location + new Vector3Int( x, y, z ), new BlockData( blockID, blockDataValue ) );
				}
			}
		}

		return BlockData.GetAreaInBox( location, new Vector3Int( xSize, ySize, zSize ) );
	}

	public IEnumerable<byte> Serialize()
	{
		var data = new List<byte>();
		foreach ( var chunk in SimulatedChunks.Values )
		{
			if ( chunk.IsEmpty )
				continue;
			data.AddRange( BitConverter.GetBytes( chunk.Position.x ) );
			data.AddRange( BitConverter.GetBytes( chunk.Position.y ) );
			data.AddRange( BitConverter.GetBytes( chunk.Position.z ) );
			var chunkData = chunk.Serialize().ToList();
			data.AddRange( BitConverter.GetBytes( chunkData.Count ) );
			data.AddRange( chunkData );
		}
		return data;
	}

	public void Deserialize( IEnumerable<byte> data )
	{
		var dataList = data.ToList();
		int index = 0;
		while ( index < dataList.Count )
		{
			int x = BitConverter.ToInt32( dataList.ToArray(), index );
			index += 4;
			int y = BitConverter.ToInt32( dataList.ToArray(), index );
			index += 4;
			int z = BitConverter.ToInt32( dataList.ToArray(), index );
			index += 4;
			var chunkPosition = new Vector3Int( x, y, z );

			int chunkDataLength = BitConverter.ToInt32( dataList.ToArray(), index );
			index += 4;

			var chunkData = dataList.Skip( index ).Take( chunkDataLength ).ToList();
			index += chunkDataLength;

			var chunk = new Chunk( this, chunkPosition );
			chunk.Deserialize( chunkData );
			SimulatedChunks[chunkPosition] = chunk;
		}
	}
}
