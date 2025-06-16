using System;
using Sandbox;

public sealed class WorldThinker : Component {
	[Property] public int RenderChunkRadius { get; set; } = 10;
	[Property] public int RenderForgetRadius { get; set; } = 20; // How far away chunks are unloaded
	[Property] public Material TextureAtlas { get; set; }
	[Property] public int BatchSize { get; set; } = 10; // Number of chunks to load in each batch
	[Property] public int UnloadBatchSize { get; set; } = 10; // Number of chunks to check to unload in each batch
	public World World = new();
	protected override void OnUpdate() {
		if ( IsProxy ) return;
		// Iterate (randomly) through every loaded chunk, check if it's outside of the forget radius from any player, and if so, unload it.
		List<Vector3Int> forgetList = new();
		foreach ( var kv in Random.Shared.TakeRandom( World.SimulatedChunks, UnloadBatchSize ) ) {
			// If the chunk manhatten distance is greater than the forget radius, append it to the forget list
			var chunk = kv.Value;
			var chunkDistance = Scene.GetAll<PlayerController>()
				.Select( c => ((c.WorldPosition / World.BlockScale / Chunk.SIZE).Floor() - chunk.Position).Components().Select( Math.Abs ).Max() ).Min();
			if ( chunkDistance > RenderForgetRadius ) {
				forgetList.Add( chunk.Position );
			}
		}
		// Now we have a list of chunks to unload, we can unload them.
		foreach ( var chunkPosition in forgetList ) {
			var chunk = World.GetChunk( chunkPosition );
			if ( chunk.IsRendered ) {
				chunk.ChunkObject.GameObject?.Destroy();
				chunk.ChunkObject = null;
			}
			World.SimulatedChunks.Remove( chunkPosition );
		}

		// For each player, ensure all the chunks around them are loaded.
		// We do NOT load every chunk at once, we do it in batches, prioritizing chunks closest to the player.

		// To do this, we get every player, and create a hashset of chunks to load.
		// Then, we order the set by distance to the closest player, and skip loaded chunks.
		// Finally, we load the chunks in batches of 10, or until we reach a certain time limit.

		var players = Scene.GetAll<PlayerController>();
		var targetChunks = new HashSet<Vector3Int>();
		foreach ( var player in players ) {
			var playerPosition = (player.WorldPosition / World.BlockScale).Floor();
			var playerChunkPosition = new Vector3Int(
				playerPosition.x.FloorDiv( Chunk.SIZE.x ),
				playerPosition.y.FloorDiv( Chunk.SIZE.y ),
				playerPosition.z.FloorDiv( Chunk.SIZE.z )
			);
			for ( int x = -RenderChunkRadius; x <= RenderChunkRadius; x++ ) {
				for ( int y = -RenderChunkRadius; y <= RenderChunkRadius; y++ ) {
					for ( int z = -RenderChunkRadius; z <= RenderChunkRadius; z++ ) {
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

		// Now we have a set of chunks to load, we can order them by distance to the closest player.
		var orderedChunks = targetChunks
			.Where( chunkPosition => !World.GetChunk( chunkPosition ).IsEmpty && !World.GetChunk( chunkPosition ).IsRendered )
			.OrderBy( chunkPosition => players.Min( player => (player.WorldPosition / World.BlockScale).DistanceSquared( new Vector3( chunkPosition.x * Chunk.SIZE.x, chunkPosition.y * Chunk.SIZE.y, chunkPosition.z * Chunk.SIZE.z ) ) ) )
			.ToList();

		// Load the chunks in batches of 10, or until we reach a certain time limit.
		float timeLimit = 0.1f; // 100ms per batch
		float startTime = Time.Now;
		foreach ( var pos in orderedChunks.Take( BatchSize ) ) {
			if ( Time.Now - startTime > timeLimit ) {
				Log.Warning( "Exceeded time limit for loading chunks, stopping batch processing." );
				break; // Stop loading if we exceed the time limit
			}
			var chunk = World.GetChunk( pos );
			Log.Info( $"Creating chunk at {pos}. {chunk.Dirty} {chunk.IsRendered}" );
			World.GetChunk( pos ).Render( Scene );
		}
	}

	[Rpc.Broadcast]
	public void BreakBlock( Vector3Int position ) {
		// Fill the block with SimpleParticles
		var block = World.GetBlock( position ).GetBlock();
		foreach ( var stack in block.GetDrops( World, position ) )
			stack.Clone().Spawn( (position + new Vector3(
					Random.Shared.Float(),
					Random.Shared.Float(),
					Random.Shared.Float()
				)) * World.BlockScale ); // Spawn the item at the center of the block
		for ( int i = 0; i < 30; i++ ) {
			var particlePos = (position + new Vector3(
				Random.Shared.Float(),
				Random.Shared.Float(),
				Random.Shared.Float()
			)) * World.BlockScale;
			var particle = new GameObject().AddComponent<SimpleParticle>();
			particle.WorldPosition = particlePos;
			particle.Material = Material.Load( "materials/textureatlas.vmat" );
			particle.TextureRect = new Rect( 0, 0, 1, 1 ); // Assuming a single texture for simplicity.
														   // Velocity is distance to the center of the block, scaled by a random factor.
			particle.Velocity = (particlePos - (position + Vector3.One * 0.5f) * World.BlockScale) * Random.Shared.Float( 2f, 7f );
			particle.Acceleration = Vector3.Down * 9.81f * 85f; // Gravity effect.
			particle.Damping = 0.99f; // Damping to slow down the particles.
			particle.Lifetime = Random.Shared.Float( 0.5f, 1.5f ); // Random lifetime for the particles.
			Vector2 texCoord = block.TextureIndex;
			texCoord += new Vector2( Random.Shared.Float( 0.0f, 1f - (1f / 16f) ), Random.Shared.Float( 0.0f, 1f - (2f / 16f) ) );
			particle.TextureRect = Rect.FromPoints( texCoord / 16f, texCoord / 16f + new Vector2( 2f / 16f, 2f / 16f ) / 16f );
			particle.Scale = new Curve( new List<Curve.Frame>
			{
				new Curve.Frame(0f, 10f, -MathF.PI, MathF.PI),
				new Curve.Frame(1f, 0f, 0f, 0f)
			} );
		}

		// Remove the block at the specified position.
		World.SetBlock( position, new BlockData( 0 ) ); // Assuming 0 is the ID for air.


	}

	protected override void OnStart() {
		base.OnStart();
	}
}
