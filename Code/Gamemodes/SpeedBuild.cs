using System;
using System.Threading;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.theoretical;

public sealed class SpeedBuild : Component
{
	[Property] List<GameObject> Islands { get; set; } = new();
	[Property] GameObject Spawn { get; set; }

	public NetList<VoxelPlayer> Players { get; set; } = new();

	[Property] List<Structure> TargetObjects { get; set; } = new();

	protected override void OnStart()
	{
		GameModeLogic();
	}

	[Rpc.Broadcast]
	public void SetPlayerTransform( VoxelPlayer player, Vector3 position, Rotation rotation )
	{
		player.WorldPosition = position;
		player.GetComponent<PlayerController>().EyeAngles = rotation;
	}

	[Property] public int MemorizeTimeSeconds { get; set; } = 60;
	[Property] public int BuildTimeSeconds { get; set; } = 300;

	private async void GameModeLogic()
	{
		if ( !Networking.IsHost )
			return;

		foreach ( var ply in Scene.GetAll<VoxelPlayer>() )
		{
			ply.IsReady = false; // Reset player readiness
		}

		TimeUntil readyCheckDone = 120f;
		SpeedBuildHud.Instance.Message = "Waiting for players to be ready...";
		while ( readyCheckDone > 0f || !Scene.GetAll<VoxelPlayer>().Any( p => p.IsReady ) )
		{
			// If NO-ONE is ready, reset the ready check timer
			if ( !Scene.GetAll<VoxelPlayer>().Any( p => p.IsReady ) )
			{
				readyCheckDone = 120f;
				SpeedBuildHud.Instance.HasTimer = false; // Hide the timer UI whilst waiting for players to be ready
			}
			else
			{
				SpeedBuildHud.Instance.HasTimer = true;
			}

			// If half the online players are ready, set the timer to 30 seconds
			if ( Scene.GetAll<VoxelPlayer>().Count( p => p.IsReady ) >= Scene.GetAll<VoxelPlayer>().Count() / 2 )
			{
				readyCheckDone = MathF.Min( readyCheckDone, 30f );
			}

			// If EVERYONE is ready, set the timer to 3 seconds
			if ( Scene.GetAll<VoxelPlayer>().All( p => p.IsReady ) )
			{
				readyCheckDone = MathF.Min( readyCheckDone, 3f );
			}

			if ( Scene.GetAll<VoxelPlayer>().Any( p => p.IsReady ) )
			{
				SpeedBuildHud.Instance.TotalTime = 120f;
				SpeedBuildHud.Instance.TimerEnd = readyCheckDone;
			}
			else
			{
				SpeedBuildHud.Instance.TotalTime = 0f; // No total time if no players are ready
				SpeedBuildHud.Instance.TimerEnd = 0f; // No timer if no players are ready
			}
			await Task.DelayRealtime( 100 );
		}

		SpeedBuildHud.Instance.HasTimer = false;
		SpeedBuildHud.Instance.HasReadyCheck = false; // Hide the ready check UI
		await Task.DelayRealtimeSeconds( 1 );

		foreach ( var player in Scene.GetAll<VoxelPlayer>().Where( c => c.IsReady ) )
		{
			Players.Add( player );
		}

		var targetStructure = Random.Shared.FromList( TargetObjects );
		if ( targetStructure == null )
		{
			Log.Error( "No target structure set for SpeedBuild." );
			return;
		}

		var index = 0;
		BlockData[,,] anyStructureData = null;
		foreach ( var player in Players )
		{
			if ( index >= Islands.Count )
			{
				Log.Error( "Not enough islands for all players." );
				return;
			}

			Islands[index].Enabled = true;
			SetPlayerTransform( player, Islands[index].WorldPosition + Vector3.Up * 40f, Rotation.LookAt( Vector3.Zero ).Angles().WithRoll( 0 ) );

			// Spawn a copy of the target structure on the player's island
			var obj = new GameObject();
			var structure = obj.AddComponent<StructureLoader>( false );
			structure.LoadedStructure = targetStructure;

			var targetPosition = Islands[index].Children.FirstOrDefault( c => c.Name == "StructureAnchor" ).WorldPosition;

			var size = structure.StructureSize;

			structure.WorldPosition = targetPosition - ((size.WithZ( 0 ) - Vector3Int.One) * World.BlockScale * 0.5f);
			structure.Enabled = true;

			player.HasBuildVolume = true;
			player.BuildAreaMins = (structure.WorldPosition / World.BlockScale).Floor();
			player.BuildAreaMaxs = player.BuildAreaMins + size - Vector3Int.One;
			anyStructureData ??= BlockData.GetAreaInBox( player.BuildAreaMins, size );

			index++;
		}

		SpeedBuildHud.Instance.TimerEnd = MemorizeTimeSeconds; // Set the timer for 60 seconds
		SpeedBuildHud.Instance.TotalTime = MemorizeTimeSeconds; // Set the total time for the game mode	
		SpeedBuildHud.Instance.HasTimer = true; // Show the timer UI

		SpeedBuildHud.Instance.Message = "Memorize the structure!";

		await Task.DelayRealtimeSeconds( MemorizeTimeSeconds ); // Wait for players to memorize the structure

		SpeedBuildHud.Instance.Message = "Time's up! Starting the build phase...";
		SpeedBuildHud.Instance.HasTimer = false; // Hide the timer UI

		foreach ( var player in Players )
		{
			// Clear the blocks in the player's build area
			var world = Scene.GetAll<WorldThinker>().FirstOrDefault()?.World;
			if ( world != null )
			{
				for ( int z = player.BuildAreaMins.z; z <= player.BuildAreaMaxs.z; z++ )
				{
					for ( int y = player.BuildAreaMins.y; y <= player.BuildAreaMaxs.y; y++ )
					{
						for ( int x = player.BuildAreaMins.x; x <= player.BuildAreaMaxs.x; x++ )
						{
							world.SetBlock( new Vector3Int( x, y, z ), new BlockData( 0 ) );
						}
					}
				}
			}


		}

		await Task.DelayRealtimeSeconds( 10 ); // Wait for the build phase

		foreach ( var player in Players )
		{
			player.inventory.Clear();
			for ( int z = 0; z < anyStructureData.GetLength( 2 ); z++ )
			{
				for ( int y = 0; y < anyStructureData.GetLength( 1 ); y++ )
				{
					for ( int x = 0; x < anyStructureData.GetLength( 0 ); x++ )
					{
						var block = anyStructureData[x, y, z];
						player.inventory.PutInFirstAvailableSlot( new ItemStack( ItemRegistry.GetItem( block.BlockID ), 1 ) );
					}
				}
			}
			player.CanBuild = true; // Allow players to start building
		}

		SpeedBuildHud.Instance.Message = "Build!";
		SpeedBuildHud.Instance.TimerEnd = BuildTimeSeconds; // Set the timer for 5 minutes
		SpeedBuildHud.Instance.TotalTime = BuildTimeSeconds; // Set the total time for the game mode
		SpeedBuildHud.Instance.HasTimer = true; // Show the timer UI

		await Task.DelayRealtimeSeconds( BuildTimeSeconds ); // Wait for the build phase to end

		SpeedBuildHud.Instance.Message = "Time's up! Judging the builds...";
		SpeedBuildHud.Instance.HasTimer = false; // Hide the timer UI
		foreach ( var player in Players )
		{
			player.CanBuild = false; // Disable building for all players
		}

		await Task.DelayRealtimeSeconds( 1 );

		foreach ( var player in Players )
		{
			player.TotalBlockArea = anyStructureData.GetLength( 0 ) * anyStructureData.GetLength( 1 ) * anyStructureData.GetLength( 2 ); // Set total block area
			player.CorrectBlocksPlaced = 0; // Reset correct blocks placed
			player.IncorrectBlocksPlaced = 0; // Reset incorrect blocks placed
		}

		{   // Evaluate each block in each player's build area
			for ( int z = 0; z < anyStructureData.GetLength( 2 ); z++ )
			{
				for ( int y = 0; y < anyStructureData.GetLength( 1 ); y++ )
				{
					for ( int x = 0; x < anyStructureData.GetLength( 0 ); x++ )
					{
						var block = anyStructureData[x, y, z];
						foreach ( var player in Players )
						{
							var pos = new Vector3Int( x, y, z ) + player.BuildAreaMins;
							var playerBlock = World.Active.GetBlock( pos );
							if ( playerBlock.BlockID == block.BlockID && playerBlock.BlockDataValue == block.BlockDataValue )
							{
								player.CorrectBlocksPlaced++;
							}
							else
							{
								player.IncorrectBlocksPlaced++;
							}
						}

						await Task.DelayRealtime( 10 ); // Wait 10 ms for animation purposes
					}
				}
			}

		}

	}


	/*public async void PreRound()
	{
		if ( Players.Count == 0 )
		{
			foreach ( var i in Scene.GetAllComponents<PlayerController>() )
			{
				Players.Add( i );
				//SetPlayerTransform( i, Spawn.WorldPosition, Rotation.Identity );
			}
		}


		//await Task.DelayRealtimeSeconds( 5 );

		var targetStructure = Random.Shared.FromList( TargetObjects );
		if ( targetStructure == null )
		{
			Log.Error( "No target structure set for SpeedBuild." );
			return;
		}

		var index = 0;
		foreach ( var i in Players )
		{
			Islands[index].Enabled = true;
			SetPlayerTransform( i, Islands[index].WorldPosition + Vector3.Up * 40f, Rotation.LookAt( Vector3.Zero ).Angles().WithRoll( 0 ) );

			// Spawn a copy of the target structure on the player's island
			var obj = new GameObject();
			var structure = obj.AddComponent<StructureLoader>( false );
			structure.LoadedStructure = targetStructure;

			var targetPosition = Islands[index].Children.FirstOrDefault( c => c.Name == "StructureAnchor" ).WorldPosition;

			var size = structure.StructureSize;

			structure.WorldPosition = targetPosition - ((size.WithZ( 0 ) - Vector3Int.One) * World.BlockScale * 0.5f);
			structure.Enabled = true;

			index++;
		}

		CurrentState = State.Starting;
	}*/
	//repeat until there's 1 player left:
	//place build to replicate, optionally layer by layer
	//after 10 seconds, delete build
	//after 60 seconds, place build in the center island
	//compare every player's score, lowest scoring player is killed and their island removed, then given a spectator camera

	//Put top 3 on a pillar, confetti and shit
	//wait 15 seconds
	//reload the scene
}
