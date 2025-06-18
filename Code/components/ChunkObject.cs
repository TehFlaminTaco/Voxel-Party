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
	public WorldThinker WorldThinkerInstanceOverride = null;
	public WorldThinker WorldThinkerInstance => WorldThinkerInstanceOverride ?? Scene.Get<WorldThinker>();
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

	ModelRenderer OpaqueRenderer;
	ModelRenderer TransparentRenderer;
	public void UpdateMesh()
	{
		WorldInstance.GetChunk( ChunkPosition ).Dirty = false; // Mark the chunk as clean before we start updating the mesh.

		// Opaque Pass
		List<Vector3> Verts = new List<Vector3>();
		List<Vector3> Normals = new List<Vector3>();
		List<Vector3> UVs = new List<Vector3>();

		List<Vector3> TransparentVerts = new List<Vector3>();
		List<Vector3> TransparentNormals = new List<Vector3>();
		List<Vector3> TransparentUVs = new List<Vector3>();

		var opaqueModel = new ModelBuilder();
		var transparentModel = new ModelBuilder();
		var collisionModel = new ModelBuilder();
		for ( int z = 0; z < Chunk.SIZE.z; z++ )
		{
			for ( int y = 0; y < Chunk.SIZE.y; y++ )
			{
				for ( int x = 0; x < Chunk.SIZE.x; x++ )
				{
					var blockPos = new Vector3Int( x, y, z );
					var blockID = WorldInstance.GetBlock( blockPos + (ChunkPosition * Chunk.SIZE) );
					var block = ItemRegistry.GetBlock( blockID.BlockID );
					if ( block.Opaque )
						AddBlockMesh( blockPos + (ChunkPosition * Chunk.SIZE), Verts, Normals, UVs );
					else
						AddBlockMesh( blockPos + (ChunkPosition * Chunk.SIZE), TransparentVerts, TransparentNormals, TransparentUVs );
					if ( block.IsSolid )
					{
						var aabb = block.GetCollisionAABB( WorldInstance, blockPos + (ChunkPosition * Chunk.SIZE) );
						collisionModel.AddCollisionBox( aabb.Size, aabb.Center );
					}
				}
			}
		}

		TexArrayTool.UpdateMaterialTexture( WorldThinkerInstance.TextureAtlas );
		TexArrayTool.UpdateMaterialTexture( WorldThinkerInstance.TranslucentTextureAtlas );


		if ( Verts.Count > 0 )
		{
			if ( OpaqueRenderer == null || !OpaqueRenderer.IsValid() )
				OpaqueRenderer = GameObject.AddComponent<ModelRenderer>();
			var mr = OpaqueRenderer;
			mr.Enabled = Verts.Count > 0; // Only enable if we have vertices to render
			var mesh = new Mesh();
			mesh.CreateVertexBuffer( Verts.Count, Vertex.Layout, Verts.Zip( Normals, UVs ).Select( v => new Vertex( v.First, v.Second, Vector3.Zero, new Vector4( v.Third, 2f ) ) ).ToList() );
			mesh.CreateIndexBuffer( Verts.Count, Enumerable.Range( 0, Verts.Count ).ToArray() );
			mesh.Material = WorldThinkerInstance.TextureAtlas ?? null;

			opaqueModel.AddMesh( mesh );
			opaqueModel.AddTraceMesh( Verts, Enumerable.Range( 0, Verts.Count ).ToList() );
			mr.Model = opaqueModel.Create();
		}
		else
		{
			if ( OpaqueRenderer != null && OpaqueRenderer.IsValid() )
			{
				OpaqueRenderer.Destroy();
			}
		}
		if ( TransparentVerts.Count > 0 )
		{
			if ( TransparentRenderer == null || !TransparentRenderer.IsValid() )
				TransparentRenderer = GameObject.AddComponent<ModelRenderer>();
			var mr = TransparentRenderer;
			mr.RenderOptions.Overlay = true; // Set the overlay option for transparent rendering
			mr.Enabled = TransparentVerts.Count > 0; // Only enable if we have vertices to render
			var transparentMesh = new Mesh();
			transparentMesh.CreateVertexBuffer( TransparentVerts.Count, Vertex.Layout, TransparentVerts.Zip( TransparentNormals, TransparentUVs ).Select( v => new Vertex( v.First, v.Second, Vector3.Zero, new Vector4( v.Third, 2f ) ) ).ToList() );
			transparentMesh.CreateIndexBuffer( TransparentVerts.Count, Enumerable.Range( 0, TransparentVerts.Count ).ToArray() );
			transparentMesh.Material = WorldThinkerInstance.TranslucentTextureAtlas ?? null;

			transparentModel.AddMesh( transparentMesh );
			transparentModel.AddTraceMesh( TransparentVerts, Enumerable.Range( 0, TransparentVerts.Count ).ToList() );
			mr.Model = transparentModel.Create();
		}

		var collider = GetOrAddComponent<ModelCollider>();
		collider.Model = collisionModel.Create();
		collider.Enabled = true; // Enable the collider for the chunk object

	}
}
