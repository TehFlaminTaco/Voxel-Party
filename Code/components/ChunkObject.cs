using Sandbox;

public sealed class ChunkObject : Component, Component.ExecuteInEditor
{
	[Sync, Property] public Vector3Int ChunkPosition { get; set; } = Vector3Int.Zero;
	[Sync]
	public byte[] ChunkData
	{
		get
		{
			var chunk = WorldInstance.GetChunk( ChunkPosition );
			if ( chunk == null )
			{
				Log.Warning( $"Chunk at position {ChunkPosition} not found." );
				return new byte[0];
			}
			// Serialize the chunk data to a byte array.
			var data = new byte[Chunk.SIZE.x * Chunk.SIZE.y * Chunk.SIZE.z * 2]; // Assuming each block is 2 bytes (ID + data value)
			for ( int z = 0; z < Chunk.SIZE.z; z++ )
			{
				for ( int y = 0; y < Chunk.SIZE.y; y++ )
				{
					for ( int x = 0; x < Chunk.SIZE.x; x++ )
					{
						var blockData = chunk.GetBlock( x, y, z );
						int index = (z * Chunk.SIZE.y * Chunk.SIZE.x + y * Chunk.SIZE.x + x) * 2;
						data[index] = blockData.BlockID; // Assuming BlockID is a byte
						data[index + 1] = blockData.BlockDataValue;
					}
				}
			}
			return data;
		}
		set
		{
			var chunk = WorldInstance.GetChunk( ChunkPosition );
			if ( chunk == null )
			{
				Log.Warning( $"Chunk at position {ChunkPosition} not found." );
				return;
			}
			if ( value.Length != Chunk.SIZE.x * Chunk.SIZE.y * Chunk.SIZE.z * 2 )
			{
				Log.Warning( $"Invalid chunk data length: {value.Length}. Expected {Chunk.SIZE.x * Chunk.SIZE.y * Chunk.SIZE.z * 2}." );
				return;
			}

			for ( int z = 0; z < Chunk.SIZE.z; z++ )
			{
				for ( int y = 0; y < Chunk.SIZE.y; y++ )
				{
					for ( int x = 0; x < Chunk.SIZE.x; x++ )
					{
						int index = (z * Chunk.SIZE.y * Chunk.SIZE.x + y * Chunk.SIZE.x + x) * 2;
						var blockID = value[index];
						var blockDataValue = value[index + 1];
						if ( chunk.GetBlock( x, y, z ).BlockID == blockID && chunk.GetBlock( x, y, z ).BlockDataValue == blockDataValue )
						{
							continue; // No change needed
						}
						chunk.SetBlock( x, y, z, new BlockData( blockID, blockDataValue ) );
					}
				}
			}
		}
	}
	public WorldThinker WorldThinkerInstance => Scene.Get<WorldThinker>();
	public World WorldInstance => WorldThinkerInstance?.World;

	protected override void OnUpdate()
	{
		WorldInstance.GetChunk( ChunkPosition ).ChunkObject = this; // Important in case we're loaded by a remote host.
		if ( WorldInstance.GetChunk( ChunkPosition ).Dirty )
		{
			UpdateMesh(); // If the chunk is dirty, update the mesh.
		}
	}


	protected override void OnStart()
	{
		base.OnStart();
		UpdateMesh();
	}

	public void AddBlockMesh( Vector3Int blockPos, List<Vector3> verts, List<Vector3> normals, List<Vector3> uvs )
	{
		// For testing, let's start by just creating a full single block for every block
		var blockData = WorldInstance.GetBlock( blockPos );
		var block = ItemRegistry.GetBlock( blockData.BlockID );
		if ( block == null )
		{
			Log.Warning( $"Block with ID {blockData.BlockID} not found at position {blockPos}." );
			return;
		}
		block.AddBlockMesh( WorldInstance, blockPos, verts, normals, uvs );
	}

	public async void UpdateMesh()
	{
		WorldInstance.GetChunk( ChunkPosition ).Dirty = false; // Mark the chunk as clean before we start updating the mesh.
		List<Vector3> Verts = new List<Vector3>();
		List<Vector3> Normals = new List<Vector3>();
		List<Vector3> UVs = new List<Vector3>();

		var mb = new ModelBuilder();
		await GameTask.WorkerThread();
		for ( int z = 0; z < Chunk.SIZE.z; z++ )
		{
			for ( int y = 0; y < Chunk.SIZE.y; y++ )
			{
				for ( int x = 0; x < Chunk.SIZE.x; x++ )
				{
					var blockPos = new Vector3Int( x, y, z );
					AddBlockMesh( blockPos + (ChunkPosition * Chunk.SIZE), Verts, Normals, UVs );
					var blockID = WorldInstance.GetBlock( blockPos + (ChunkPosition * Chunk.SIZE) );
					var block = ItemRegistry.GetBlock( blockID.BlockID );
					if ( block.IsSolid )
					{
						var aabb = block.GetCollisionAABB( WorldInstance, blockPos + (ChunkPosition * Chunk.SIZE) );
						mb.AddCollisionBox( aabb.Size, aabb.Center );
					}
				}
			}
		}
		await GameTask.MainThread();

		TexArrayTool.UpdateMaterialTexture( WorldThinkerInstance.TextureAtlas );

		var mr = GetOrAddComponent<ModelRenderer>();
		mr.Enabled = Verts.Count > 0; // Only enable if we have vertices to render
		if ( Verts.Count > 0 )
		{
			var mesh = new Mesh();
			mesh.CreateVertexBuffer( Verts.Count, Vertex.Layout, Verts.Zip( Normals, UVs ).Select( v => new Vertex( v.First, v.Second, Vector3.Zero, new Vector4( v.Third, 2f ) ) ).ToList() );
			mesh.CreateIndexBuffer( Verts.Count, Enumerable.Range( 0, Verts.Count ).ToArray() );
			mesh.Material = WorldThinkerInstance.TextureAtlas ?? null;

			mb.AddMesh( mesh );
			mb.AddTraceMesh( Verts, Enumerable.Range( 0, Verts.Count ).ToList() );
		}

		mr.Model = mb.Create();
		var collisionModel = GetOrAddComponent<ModelCollider>();
		collisionModel.Model = mr.Model;
		collisionModel.Enabled = true; // Enable the collider for the chunk object

	}
}
