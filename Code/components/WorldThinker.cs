using System;
using Sandbox;

public sealed class WorldThinker : Component, Component.ExecuteInEditor
{
	[Property] public int RenderChunkRadius { get; set; } = 10;
	[Property] public int RenderForgetRadius { get; set; } = 20; // How far away chunks are unloaded
	[Property] public Material TextureAtlas { get; set; }
	[Property] public Material TranslucentTextureAtlas { get; set; } // For translucent blocks, like glass or water
	[Property] public int BatchSize { get; set; } = 10; // Number of chunks to load in each batch
	[Property] public int UnloadBatchSize { get; set; } = 10; // Number of chunks to check to unload in each batch

	[Property, Hide]
	public byte[] SerializedWorld
	{
		get
		{
			return World.Active?.Serialize().ToArray();
		}
		set
		{
			if ( value == null ) return;
			World.Active?.Deserialize( value );
		}
	}

	public World World = new();

	[ConVar] public static int vp_debug_showchunkborders { get; set; } = 0;

	protected override void OnStart()
	{
		base.OnStart();
		foreach ( var child in GameObject.Children.ToList() )
			child.DestroyImmediate();
	}

	[Button]
	public void Regenerate()
	{
		var data = this.SerializedWorld;
		foreach ( var child in GameObject.Children.ToList() )
			child.DestroyImmediate();
		World.SimulatedChunks.Clear();
		this.SerializedWorld = data; // Re-apply the serialized world to regenerate it.
		foreach ( var obj in Scene.GetAll<StructureLoader>() )
		{
			obj.Regenerate(); // Re-run the OnEnabled logic to ensure the structure is loaded in the editor.
		}

	}

	protected override void OnFixedUpdate()
	{
		World.Active = World;
		if ( IsProxy ) return;

		// Iterate (randomly) through every loaded chunk, check if it's outside of the forget radius from any player, and if so, unload it.
		if ( Game.IsPlaying )
		{
			List<Vector3Int> forgetList = new();
			foreach ( var kv in Random.Shared.TakeRandom( World.SimulatedChunks.Where( c => c.Value.IsRendered ), UnloadBatchSize ) )
			{
				// If the chunk manhatten distance is greater than the forget radius, append it to the forget list
				var chunk = kv.Value;
				var chunkDistance = Scene.GetAllComponents<PlayerController>()
						.Select( c => ((c.WorldPosition / World.BlockScale / Chunk.SIZE).Floor() - chunk.Position)
							.Components().Select( Math.Abs ).Max() ).Min();
				if ( chunkDistance > RenderForgetRadius )
				{
					forgetList.Add( chunk.Position );
				}
			}
			// Now we have a list of chunks to unload, we can unload them.
			foreach ( var chunkPosition in forgetList )
			{
				var chunk = World.GetChunk( chunkPosition );
				Log.Info( $"Checking chunk at {chunkPosition} for unloading." );
				if ( chunk.IsRendered )
				{
					Log.Info( $"Unloading chunk at {chunkPosition}." );
					chunk.ChunkObject.GameObject?.Destroy();
					chunk.ChunkObject = null;
				}
			}
		}

		// For each player, ensure all the chunks around them are loaded.
		// We do NOT load every chunk at once, we do it in batches, prioritizing chunks closest to the player.

		// To do this, we get every player, and create a hashset of chunks to load.
		// Then, we order the set by distance to the closest player, and skip loaded chunks.
		// Finally, we load the chunks in batches of 10, or until we reach a certain time limit.

		var players = Scene.GetAll<PlayerController>();
		var targetChunks = new HashSet<Vector3Int>();
		if ( !Game.IsPlaying )
		{
			targetChunks = World.SimulatedChunks.Select( kv => kv.Key ).ToHashSet();
		}
		else
		{
			foreach ( var player in players )
			{
				var playerPosition = (player.WorldPosition / World.BlockScale).Floor();
				var playerChunkPosition = new Vector3Int(
					playerPosition.x.FloorDiv( Chunk.SIZE.x ),
					playerPosition.y.FloorDiv( Chunk.SIZE.y ),
					playerPosition.z.FloorDiv( Chunk.SIZE.z )
				);
				for ( int x = -RenderChunkRadius; x <= RenderChunkRadius; x++ )
				{
					for ( int y = -RenderChunkRadius; y <= RenderChunkRadius; y++ )
					{
						for ( int z = -RenderChunkRadius; z <= RenderChunkRadius; z++ )
						{
							var chunkPosition = new Vector3Int(
								playerChunkPosition.x + x,
								playerChunkPosition.y + y,
								playerChunkPosition.z + z
							);
							targetChunks.Add( chunkPosition );
						}
					}
				}
			}
		}

		// Now we have a set of chunks to load, we can order them by distance to the closest player.
		var orderedChunks = Game.IsEditor ? targetChunks.Where( chunkPosition => !World.GetChunk( chunkPosition ).IsEmpty && !World.GetChunk( chunkPosition ).IsRendered ).ToList() : targetChunks
		.Where( chunkPosition => !World.GetChunk( chunkPosition ).IsEmpty && !World.GetChunk( chunkPosition ).IsRendered )
		.OrderBy( chunkPosition => players.Min( player => (player.WorldPosition / World.BlockScale).DistanceSquared( new Vector3( chunkPosition.x * Chunk.SIZE.x, chunkPosition.y * Chunk.SIZE.y, chunkPosition.z * Chunk.SIZE.z ) ) ) )
		.ToList();

		// Load the chunks in batches of 10, or until we reach a certain time limit.
		float timeLimit = 0.1f; // 100ms per batch
		float startTime = Time.Now;
		foreach ( var pos in orderedChunks.Take( BatchSize ) )
		{
			if ( Time.Now - startTime > timeLimit )
			{
				Log.Warning( "Exceeded time limit for loading chunks, stopping batch processing." );
				break; // Stop loading if we exceed the time limit
			}
			var chunk = World.GetChunk( pos );
			//Log.Info( $"Creating chunk at {pos}. {chunk.Dirty} {chunk.IsRendered}" );
			World.GetChunk( pos ).Render( Scene );
		}

	}

	[Rpc.Broadcast]
	public void BreakBlock( Vector3Int position, bool dropItems = true )
	{
		// Fill the block with SimpleParticles
		var block = World.GetBlock( position ).GetBlock();

		if ( dropItems )
		{
			foreach ( var stack in block.GetDrops( World, position ) )
				stack.Clone().Spawn( (position + new Vector3(
					Random.Shared.Float(),
					Random.Shared.Float(),
					Random.Shared.Float()
				)) * World.BlockScale ); // Spawn the item at the center of the block
		}

		// Remove the block at the specified position.
		World.SetBlock( position, new BlockData( 0 ) ); // Assuming 0 is the ID for air.
	}

	protected override void OnPreRender()
	{
		if ( vp_debug_showchunkborders == 1 )
		{
			foreach ( var i in World.SimulatedChunks )
			{
				var pos = Helpers.VoxelToWorld( i.Key );
				var size = Chunk.SIZE * World.BlockScale;
				var mins = pos - size / 2;
				var maxs = pos + size / 2;
				var bbox = new BBox( mins, maxs );
				Gizmo.Draw.LineBBox( bbox );
			}
		}
	}
}
