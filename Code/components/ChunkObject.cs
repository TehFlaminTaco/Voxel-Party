using System;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Sandbox;

public sealed class ChunkObject : Component, Component.ExecuteInEditor
{

	[Sync, Property] public Vector3Int ChunkPosition { get; set; } = Vector3Int.Zero;
	[Sync( SyncFlags.FromHost )]
	public NetList<byte> ChunkData { get; set; } = new();
	[Sync] public WorldThinker WorldThinkerInstanceOverride { get; set; } = null;
	public WorldThinker WorldThinkerInstance => WorldThinkerInstanceOverride ?? Scene.Get<WorldThinker>();
	public World WorldInstance => WorldThinkerInstance?.World;
	[Property] public bool TortureTest = false;
	int LastChunkDataHash = -1;
	protected override void OnUpdate()
	{
		if ( !ItemRegistry.FinishedLoading )
			return;
		var chunk = WorldInstance.GetChunk( ChunkPosition );
		chunk.ChunkObject = this; // Important in case we're loaded by a remote host.
		if ( !Networking.IsHost )
		{
			int hash = Convert.ToBase64String( ChunkData.ToArray() ).GetHashCode();
			if ( hash != LastChunkDataHash )
			{
				LastChunkDataHash = hash;
				OnChunkDataChanged( ChunkData );
			}
		}

		if ( Networking.IsHost && chunk.NetworkDirty || ChunkData.Count == 0 )
			UpdateChunkData();
		if ( TortureTest || chunk.RenderDirty )
		{
			if ( Scene is not null )
				_ = UpdateMesh(); // If the chunk is dirty, update the mesh.
		}
	}

	void OnChunkDataChanged( NetList<byte> value )
	{
		var chunk = WorldInstance.GetChunk( ChunkPosition );
		Log.Info( $"Got chunk update @ {ChunkPosition}" );
		if ( chunk == null )
		{
			Log.Warning( $"Chunk at position {ChunkPosition} not found." );
			return;
		}
		if ( value.Count != Chunk.SIZE.x * Chunk.SIZE.y * Chunk.SIZE.z * 2 )
		{
			Log.Warning( $"Invalid chunk data length: {value.Count}. Expected {Chunk.SIZE.x * Chunk.SIZE.y * Chunk.SIZE.z * 2}." );
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

	void UpdateChunkData()
	{
		if ( GameObject == null && this.WorldThinkerInstanceOverride == null ) return; // No data if we're not initialized
		var chunk = WorldInstance.GetChunk( ChunkPosition );
		if ( chunk == null )
		{
			Log.Warning( $"Chunk at position {ChunkPosition} not found." );
			return;
		}
		// Serialize the chunk data to a byte array.
		var data = ChunkData;
		data.Clear();
		for ( int z = 0; z < Chunk.SIZE.z; z++ )
		{
			for ( int y = 0; y < Chunk.SIZE.y; y++ )
			{
				for ( int x = 0; x < Chunk.SIZE.x; x++ )
				{
					var blockData = chunk.GetBlock( x, y, z );
					data.Add( blockData.BlockID ); // Assuming BlockID is a byte
					data.Add( blockData.BlockDataValue );
				}
			}
		}
		ChunkData = data;
		chunk.NetworkDirty = false;
		Log.Info( $"Sent chunk update @ {ChunkPosition}" );
	}


	protected override void OnStart()
	{
		_ = UpdateMesh();
	}

	public void AddBlockMesh( World world, Vector3Int blockPos, List<Vertex> verts )
	{
		// For testing, let's start by just creating a full single block for every block
		var blockData = world.GetBlock( blockPos );
		var block = ItemRegistry.GetBlock( blockData.BlockID );
		if ( block == null )
		{
			ItemRegistry.UpdateRegistry();
			Log.Warning( $"Block with ID {blockData.BlockID} not found at position {blockPos}." );
			return;
		}
		block.AddBlockMesh( world, blockPos, verts );
	}

	Dictionary<Material, ModelRenderer> Renderers = new();
	Dictionary<Material, Mesh> Meshes = new();
	Model Collider;

	public Dictionary<Vector3Int, (GameObject obj, BlockData data)> BlockObjects { get; set; } = new Dictionary<Vector3Int, (GameObject obj, BlockData data)>();
	bool locked = false;
	public async Task UpdateMesh()
	{
		if ( locked ) return;
		if ( Scene is null ) return;
		if ( !ItemRegistry.FinishedLoading ) return;
		try
		{
			var world = WorldInstance;
			var thinker = WorldThinkerInstance;
			var chunkPos = ChunkPosition;
			world.GetChunk( chunkPos ).RenderDirty = false; // Mark the chunk as clean before we start updating the mesh.
															// Opaque Pass
			Dictionary<Material, List<Vertex>> Vertexes = new();

			_ = TexArrayTool.UpdateMaterialTexture( WorldThinkerInstance.TextureAtlas );
			_ = TexArrayTool.UpdateMaterialTexture( WorldThinkerInstance.TranslucentTextureAtlas );
			await GameTask.WorkerThread();
			var collisionModel = new ModelBuilder();
			List<Vector3Int> DestroyBlockObjects = new();
			List<Vector3Int> CreateBlockObjects = new();
			for ( int z = 0; z < Chunk.SIZE.z; z++ )
			{
				for ( int y = 0; y < Chunk.SIZE.y; y++ )
				{
					for ( int x = 0; x < Chunk.SIZE.x; x++ )
					{
						var blockPos = new Vector3Int( x, y, z );
						var blockData = world.GetBlock( blockPos + (chunkPos * Chunk.SIZE) );
						var block = ItemRegistry.GetBlock( blockData.BlockID );
						if ( Networking.IsHost && BlockObjects.ContainsKey( blockPos ) && BlockObjects[blockPos].data != blockData )
						{
							DestroyBlockObjects.Add( blockPos );
						}
						if ( block.BlockObject != null )
						{
							if ( Networking.IsHost )
								CreateBlockObjects.Add( blockPos );
						}
						else
						{
							var mat = block.Material ?? (block.Opaque ? thinker.TextureAtlas : thinker.TranslucentTextureAtlas);
							if ( mat != null )
								AddBlockMesh( world, blockPos + (chunkPos * Chunk.SIZE), Vertexes.GetOrCreate( mat ) );
						}
						if ( block.IsSolid )
						{
							var aabb = block.GetCollisionAABBChunk( world, blockPos + (chunkPos * Chunk.SIZE) );
							foreach ( var bbox in aabb )
								collisionModel.AddCollisionBox( bbox.Extents, bbox.Center );
						}
					}
				}
			}

			await GameTask.MainThread();
			if ( Scene == null )
			{
				return;
			}
			using ( Scene.Push() )
			{
				foreach ( var blockPos in DestroyBlockObjects )
				{
					BlockObjects[blockPos].obj.Destroy();
					BlockObjects.Remove( blockPos );
				}
				foreach ( var blockPos in CreateBlockObjects )
				{
					if ( !BlockObjects.ContainsKey( blockPos ) )
					{

						var blockData = world.GetBlock( blockPos + (chunkPos * Chunk.SIZE) );
						var block = ItemRegistry.GetBlock( blockData.BlockID );
						var blockObject = block.BlockObject.Clone( GameObject, blockPos * World.BlockScale, Rotation.Identity, Vector3.One );
						BlockObjects[blockPos] = (blockObject, blockData);
						if ( blockObject.GetComponent<IBlockDataReceiver>() is IBlockDataReceiver receiver )
						{
							receiver.AcceptBlockData( world, blockPos + (chunkPos * Chunk.SIZE), blockData );
						}
						blockObject.NetworkSpawn();
					}
				}

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
		finally
		{
			locked = false;
		}
	}
}
