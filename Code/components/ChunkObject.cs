using Sandbox;

public sealed class ChunkObject : Component, Component.ExecuteInEditor
{
	[Sync, Property] public Vector3Int ChunkPosition { get; set; } = Vector3Int.Zero;
	[Sync( SyncFlags.FromHost | SyncFlags.Query )]
	public byte[] ChunkData
	{
		get
		{
			if ( GameObject == null && this.WorldThinkerInstanceOverride == null ) return new byte[0]; // No data if we're not initialized
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
	[Sync] public WorldThinker WorldThinkerInstanceOverride { get; set; } = null;
	public WorldThinker WorldThinkerInstance => WorldThinkerInstanceOverride ?? Scene.Get<WorldThinker>();
	public World WorldInstance => WorldThinkerInstance?.World;
	[Property] public bool TortureTest = false;
	protected override void OnUpdate()
	{
		WorldInstance.GetChunk( ChunkPosition ).ChunkObject = this; // Important in case we're loaded by a remote host.
		if ( TortureTest || WorldInstance.GetChunk( ChunkPosition ).Dirty )
		{
			UpdateMesh(); // If the chunk is dirty, update the mesh.
		}
	}


	protected override void OnStart()
	{
		UpdateMesh();
	}

	public void AddBlockMesh( Vector3Int blockPos, List<Vertex> verts )
	{
		// For testing, let's start by just creating a full single block for every block
		var blockData = WorldInstance.GetBlock( blockPos );
		var block = ItemRegistry.GetBlock( blockData.BlockID );
		if ( block == null )
		{
			ItemRegistry.UpdateRegistry();
			Log.Warning( $"Block with ID {blockData.BlockID} not found at position {blockPos}." );
			return;
		}
		block.AddBlockMesh( WorldInstance, blockPos, verts );
	}

	Dictionary<Material, ModelRenderer> Renderers = new();

	public Dictionary<Vector3Int, (GameObject obj, BlockData data)> BlockObjects { get; set; } = new Dictionary<Vector3Int, (GameObject obj, BlockData data)>();

	static Profiler meshUpdate { get; set; } = new();
	[Property] public Profiler MeshUpdate => meshUpdate;

	public void UpdateMesh()
	{
		using ( MeshUpdate.Push() )
		{

			WorldInstance.GetChunk( ChunkPosition ).Dirty = false; // Mark the chunk as clean before we start updating the mesh.
																   // Opaque Pass
			Dictionary<Material, List<Vertex>> Vertexes = new();

			TexArrayTool.UpdateMaterialTexture( WorldThinkerInstance.TextureAtlas );
			TexArrayTool.UpdateMaterialTexture( WorldThinkerInstance.TranslucentTextureAtlas );

			var collisionModel = new ModelBuilder();
			for ( int z = 0; z < Chunk.SIZE.z; z++ )
			{
				for ( int y = 0; y < Chunk.SIZE.y; y++ )
				{
					for ( int x = 0; x < Chunk.SIZE.x; x++ )
					{
						var blockPos = new Vector3Int( x, y, z );
						var blockData = WorldInstance.GetBlock( blockPos + (ChunkPosition * Chunk.SIZE) );
						var block = ItemRegistry.GetBlock( blockData.BlockID );
						if ( Networking.IsHost && BlockObjects.ContainsKey( blockPos ) && BlockObjects[blockPos].data != blockData )
						{
							// Destroy the BlockObject at this position if it exists and the block data has changed
							BlockObjects[blockPos].obj.Destroy();
							BlockObjects.Remove( blockPos );
						}
						if ( block.BlockObject != null )
						{
							if ( Networking.IsHost )
							{
								// Instantiate the block object if it doesn't exist
								// (If it was wrong, it would have been destroyed above)
								if ( !BlockObjects.ContainsKey( blockPos ) )
								{
									var blockObject = block.BlockObject.Clone( GameObject, blockPos * World.BlockScale, Rotation.Identity, Vector3.One );
									BlockObjects[blockPos] = (blockObject, blockData);
									if ( blockObject.GetComponent<IBlockDataReceiver>() is IBlockDataReceiver receiver )
									{
										receiver.AcceptBlockData( this.WorldInstance, blockPos + (ChunkPosition * Chunk.SIZE), blockData );
									}
									blockObject.NetworkSpawn();
								}
							}
						}
						else
						{
							var mat = block.Material ?? (block.Opaque ? WorldThinkerInstance.TextureAtlas : WorldThinkerInstance.TranslucentTextureAtlas);
							AddBlockMesh( blockPos + (ChunkPosition * Chunk.SIZE), Vertexes.GetOrCreate( mat ) );
						}
						if ( block.IsSolid )
						{
							var aabb = block.GetCollisionAABBChunk( WorldInstance, blockPos + (ChunkPosition * Chunk.SIZE) );
							foreach ( var bbox in aabb )
								collisionModel.AddCollisionBox( bbox.Extents, bbox.Center );
						}
					}
				}
			}


			/*if ( Verts.Count > 0 )
			{
				if ( OpaqueRenderer == null || !OpaqueRenderer.IsValid() )
				{
					OpaqueRenderer = GameObject.AddComponent<ModelRenderer>();
					OpaqueRenderer.Flags = ComponentFlags.NotNetworked;
				}
				var mr = OpaqueRenderer;
				mr.Enabled = Verts.Count > 0; // Only enable if we have vertices to render
				var mesh = new Mesh();

				Vertex[] vertexes = new Vertex[Verts.Count];
				for ( int i = 0; i < Verts.Count; i++ )
				{
					var pos = Verts[i];
					var normal = Normals[i];
					var uv = UVs[i];
					var tangent = Tangents[i];
					vertexes[i] = new(
						pos,
						normal,
						tangent,
						new Vector4( uv, 0f )
					);
				}

				mesh.CreateVertexBuffer( Verts.Count, Vertex.Layout, vertexes.ToList() );
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
				{
					TransparentRenderer = GameObject.AddComponent<ModelRenderer>();
					TransparentRenderer.Flags = ComponentFlags.NotNetworked;
				}
				var mr = TransparentRenderer;
				mr.MaterialOverride = WorldThinkerInstance.TranslucentTextureAtlas ?? null; // Use the translucent texture atlas
				mr.Enabled = TransparentVerts.Count > 0; // Only enable if we have vertices to render
				var transparentMesh = new Mesh();
				Vertex[] vertexes = new Vertex[TransparentVerts.Count];
				for ( int i = 0; i < TransparentVerts.Count; i++ )
				{
					var pos = TransparentVerts[i];
					var normal = TransparentNormals[i];
					var uv = TransparentUVs[i];
					var tangent = TransparentTangents[i];
					vertexes[i] = new(
						pos,
						normal,
						tangent,
						new Vector4( uv, 0f )
					);
				}
				transparentMesh.CreateVertexBuffer( TransparentVerts.Count, Vertex.Layout, vertexes.ToList() );
				transparentMesh.CreateIndexBuffer( TransparentVerts.Count, Enumerable.Range( 0, TransparentVerts.Count ).ToArray() );
				transparentMesh.Material = WorldThinkerInstance.TranslucentTextureAtlas ?? null;

				transparentModel.AddMesh( transparentMesh );
				transparentModel.AddTraceMesh( TransparentVerts, Enumerable.Range( 0, TransparentVerts.Count ).ToList() );
				mr.Model = transparentModel.Create();
			}
			else
			{
				if ( TransparentRenderer != null && TransparentRenderer.IsValid() )
				{
					TransparentRenderer.Destroy();
				}
			}*/
			var toRemove = Renderers.Where( k => !Vertexes.ContainsKey( k.Key ) || Vertexes[k.Key].Count == 0 ); // Remove all renderers where we don't have verts for.
			foreach ( var r in toRemove )
			{
				r.Value.Destroy();
				Renderers.Remove( r.Key );
			}

			foreach ( var kv in Vertexes.Where( c => c.Value.Count > 0 ) )
			{
				var mat = kv.Key;
				var verts = kv.Value;

				if ( !Renderers.ContainsKey( mat ) )
				{
					var renderer = AddComponent<ModelRenderer>();
					renderer.Flags = ComponentFlags.NotNetworked;
					renderer.MaterialOverride = mat;
					Renderers[mat] = renderer;
				}
				var indicies = Enumerable.Range( 0, verts.Count ).ToArray();

				var mesh = new Mesh();
				mesh.CreateVertexBuffer( verts.Count, Vertex.Layout, verts );
				mesh.CreateIndexBuffer( verts.Count, indicies );
				mesh.Material = mat;

				var model = new ModelBuilder();
				model.AddMesh( mesh );
				model.AddTraceMesh( verts.Select( c => c.Position ).ToList(), indicies.ToList() );

				Renderers[mat].Model = model.Create();
			}


			var collider = GetOrAddComponent<ModelCollider>();
			collider.Model = collisionModel.Create();
			collider.Static = true; // Set the collider to static since chunks do not move
			collider.Enabled = true; // Enable the collider for the chunk object

		}
	}
}
