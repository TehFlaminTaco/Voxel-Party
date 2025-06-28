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

	public List<Structure> TargetStructures => ResourceLibrary.GetAll<Structure>( "structures/speedbuild/", false ).ToList();

	Structure _currentStructure;

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

	[Property, Description( "In seconds, in order of rounds. Rounds past the last entry use the same number." )]
	public int[] MemorizeTime { get; set; } = new[]{
		45, // Nice and easy
		30, // A bit more stressful
		15,
		15,
		10,
		10,
		10
	};
	[Property, Description( "In seconds, in order of rounds. Rounds past the last entry use the same number." )]
	public int[] BuildTimeOffset { get; set; } = new[]{
		30, // Nice and easy
		15, // A bit more stressful
		5,
		0,
		-5,
		-5,
		-10,
		-15
	};

	[Property, Description( "In seconds, in order of rounds. Rounds past the last entry use the same number." )]
	public int[] TargetAccuracy { get; set; } = new[]{
		40,
		50,
		60,
		70,
		80,
		90
	};

	[Property]
	public Structure.StructureDifficulty[] Difficulty { get; set; } = new[] {
		Structure.StructureDifficulty.Easy,
		Structure.StructureDifficulty.Easy,
		Structure.StructureDifficulty.Standard,
		Structure.StructureDifficulty.Standard,
		Structure.StructureDifficulty.Standard,
		Structure.StructureDifficulty.Hard,
		Structure.StructureDifficulty.Hard,
	};

	public bool IsPlaying = false;
	async void GameModeLogic()
	{
		if ( !Networking.IsHost )
			return;
		if ( IsPlaying ) return;
		IsPlaying = true;

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

		List<VoxelPlayer> HistoricalPlayers = new();

		foreach ( var player in Scene.GetAll<VoxelPlayer>() )
		{
			Players.Add( player );
		}

		bool gameRunning = true;
		Structure lastStructure = new();
		int RoundNumber = 0;
		if ( Players.Count < 3 ) // If we're only playing one round, up the difficulty a little.
		{
			RoundNumber = 2;
		}
		while ( gameRunning )
		{
			var structureList = TargetStructures.Where( c => c.ReplicateDifficulty == Difficulty.IndexOrLast( RoundNumber ) ).ToList();
			if ( structureList.Count == 0 )
			{
				Log.Warning( $"We don't have any structures for difficulty {Difficulty.IndexOrLast( RoundNumber )}? Why!?" );
				structureList = TargetStructures;
			}
			else if ( Players.Count < 3 ) // If we're in final death, use any structure.
			{
				structureList = TargetStructures;
			}

			_currentStructure = structureList[Random.Shared.Int( 0, structureList.Count - 1 )];
			while ( lastStructure.IsValid() && _currentStructure == lastStructure )
			{
				_currentStructure = structureList[Random.Shared.Int( 0, structureList.Count - 1 )];
			}

			if ( _currentStructure == null )
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
				SetPlayerTransform( player, Islands[index].GetComponentInChildren<SpawnPoint>().WorldPosition,
					Rotation.LookAt( Vector3.Zero ).Angles().WithRoll( 0 ) );
				player.MakeFlying();
				player.IslandIndex = index;
				index++;
			}


			await Task.DelayRealtimeSeconds( 1f );
			foreach ( var p in Players.Where( c => !(c?.IsValid() ?? false) ).ToList() )
				Players.Remove( p ); // Cleanup any players we lost.

			List<(Vector3Int mins, Vector3Int maxes)> CleanupAreas = new();
			foreach ( var player in Players )
			{
				// Spawn a copy of the target structure on the player's island
				var obj = new GameObject();
				var structure = obj.AddComponent<StructureLoader>( false );
				structure.LoadedStructure = _currentStructure;

				var targetPosition = Islands[player.IslandIndex].Children.FirstOrDefault( c => c.Name == "StructureAnchor" ).WorldPosition;

				var size = structure.StructureSize;

				structure.WorldPosition = targetPosition - ((size.WithZ( 0 ) - Vector3Int.One) * World.BlockScale * 0.5f);
				structure.Enabled = true;

				player.HasBuildVolume = true;
				player.CanBuild = false;
				player.BuildAreaMins = (structure.WorldPosition / World.BlockScale).Floor();
				player.BuildAreaMaxs = player.BuildAreaMins + size - Vector3Int.One;
				anyStructureData ??= BlockData.GetAreaInBox( player.BuildAreaMins, size );

				CleanupAreas.Add( (player.BuildAreaMins, player.BuildAreaMaxs) );
			}

			SpeedBuildHud.Instance.TimerEnd = MemorizeTime.IndexOrLast( RoundNumber ); // Set the timer for 60 seconds
			SpeedBuildHud.Instance.TotalTime = MemorizeTime.IndexOrLast( RoundNumber ); // Set the total time for the game mode	
			SpeedBuildHud.Instance.HasTimer = true; // Show the timer UI

			SpeedBuildHud.Instance.Message = "Memorize the structure!";

			await Task.DelayRealtimeSeconds( MemorizeTime.IndexOrLast( RoundNumber ) ); // Wait for players to memorize the structure

			SpeedBuildHud.Instance.Message = "Time's up! Starting the build phase...";
			SpeedBuildHud.Instance.HasTimer = false; // Hide the timer UI
			foreach ( var p in Players.Where( c => !(c?.IsValid() ?? false) ).ToList() )
				Players.Remove( p ); // Cleanup any players we lost.
			foreach ( var player in Players )
			{
				// Clear the blocks in the player's build area;
				if ( World.Active != null )
				{
					for ( int z = player.BuildAreaMins.z; z <= player.BuildAreaMaxs.z; z++ )
					{
						for ( int y = player.BuildAreaMins.y; y <= player.BuildAreaMaxs.y; y++ )
						{
							for ( int x = player.BuildAreaMins.x; x <= player.BuildAreaMaxs.x; x++ )
							{
								World.Active.SetBlock( new Vector3Int( x, y, z ), new BlockData( 0 ) );
							}
						}
					}
				}
			}

			//await Task.DelayRealtimeSeconds( 2 ); // Wait for the build phase

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
			SpeedBuildHud.Instance.TimerEnd = _currentStructure.SecondsToBuild + BuildTimeOffset.IndexOrLast( RoundNumber ); // Set the timer for 5 minutes
			SpeedBuildHud.Instance.TotalTime = _currentStructure.SecondsToBuild + BuildTimeOffset.IndexOrLast( RoundNumber ); // Set the total time for the game mode
			SpeedBuildHud.Instance.HasTimer = true; // Show the timer UI

			await Task.DelayRealtimeSeconds( _currentStructure.SecondsToBuild + BuildTimeOffset.IndexOrLast( RoundNumber ) ); // Wait for the build phase to end

			SpeedBuildHud.Instance.Message = "Time's up! Judging the builds...";
			SpeedBuildHud.Instance.HasTimer = false; // Hide the timer UI

			SpeedBuildHud.Instance.KillPercentage = TargetAccuracy.IndexOrLast( RoundNumber );
			foreach ( var p in Players.Where( c => !(c?.IsValid() ?? false) ).ToList() )
				Players.Remove( p ); // Cleanup any players we lost.
			foreach ( var player in Players )
			{
				player.CanBuild = false; // Disable building for all players
			}

			await Task.DelayRealtimeSeconds( 1 );

			int validBlocks = 0;
			for ( int z = 0; z < anyStructureData.GetLength( 2 ); z++ )
			{
				for ( int y = 0; y < anyStructureData.GetLength( 1 ); y++ )
				{
					for ( int x = 0; x < anyStructureData.GetLength( 0 ); x++ )
					{
						var block = anyStructureData[x, y, z];
						if ( block.BlockID != 0 )
							validBlocks++;
					}
				}
			}
			foreach ( var p in Players.Where( c => !(c?.IsValid() ?? false) ).ToList() )
				Players.Remove( p ); // Cleanup any players we lost.
			foreach ( var player in Players )
			{
				player.TotalBlockArea = validBlocks; // Set total block area
				player.CorrectBlocksPlaced = 0; // Reset correct blocks placed
				player.IncorrectBlocksPlaced = 0; // Reset incorrect blocks placed
			}


			Dictionary<VoxelPlayer, int> ScoreByPlayer = new();
			{   // Evaluate each block in each player's build area
				for ( int z = 0; z < anyStructureData.GetLength( 2 ); z++ )
				{
					for ( int y = 0; y < anyStructureData.GetLength( 1 ); y++ )
					{
						for ( int x = 0; x < anyStructureData.GetLength( 0 ); x++ )
						{
							var block = anyStructureData[x, y, z];
							if ( block.BlockID == 0 ) continue;
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
			await Task.DelayRealtimeSeconds( 0.5f );
			foreach ( var p in Players.Where( c => !(c?.IsValid() ?? false) ).ToList() )
				Players.Remove( p ); // Cleanup any players we lost.
			foreach ( var player in Players )
			{
				ScoreByPlayer[player] = player.CorrectBlocksPlaced * 100 / validBlocks;
			}

			if ( Players.Count > 0 )
			{
				SpeedBuildHud.Instance.Message = "Executing the failures!";
				await Task.DelayRealtimeSeconds( 1 );
				foreach ( var p in Players.Where( c => !(c?.IsValid() ?? false) ).ToList() )
					Players.Remove( p ); // Cleanup any players we lost.
				var losers = Players.Where( k => ScoreByPlayer[k] < TargetAccuracy.IndexOrLast( RoundNumber ) ).OrderByDescending( k => ScoreByPlayer[k] ).ToList();
				foreach ( var player in losers )
				{
					if ( !(player?.IsValid() ?? false) )
						continue;
					if ( ScoreByPlayer[player] < TargetAccuracy.IndexOrLast( RoundNumber ) )
					{
						player.TotalBlockArea = 0;
						player.Explode();
						await Task.DelayRealtime( 500 );
					}
				}
				HistoricalPlayers.AddRange( losers.Reverse<VoxelPlayer>() );
				foreach ( var loser in losers )
					Players.Remove( loser );
			}
			foreach ( var p in Players.Where( c => !(c?.IsValid() ?? false) ).ToList() )
				Players.Remove( p ); // Cleanup any players we lost.
			if ( Players.Count <= 0 )
			{
				SpeedBuildHud.Instance.Message = "Declaring winners...";
				var podiumObject = GameObject.Children.Find( j => j.Name == "PodiumCamera" );
				foreach ( var ply in Scene.GetAll<VoxelPlayer>() )
				{
					ply.Spectator = true;
					ply.Lock();
					ply.MoveCameraTo( podiumObject.WorldTransform, true );
				}

				var allPlayers = Players.OrderByDescending( k => ScoreByPlayer[k] ).Concat( HistoricalPlayers.Reverse<VoxelPlayer>() ).Where( c => c?.IsValid() ?? false ).ToArray();
				await Task.DelayRealtimeSeconds( 1 );
				if ( allPlayers.Length > 2 )
				{
					SpeedBuildHud.Instance.Message = "Third Place";
					await Task.DelayRealtimeSeconds( 3f );
					if ( allPlayers[2].IsValid() )
					{
						var bronze = GameObject.Children.Find( j => j.Name == "Bronze" );
						SpeedBuildHud.Instance.Message = allPlayers[2].Network.Owner.DisplayName;
						allPlayers[2].Spectator = false;
						allPlayers[2].IsFlying = false;
						allPlayers[2].MoveTo( bronze.WorldTransform );
					}
					else
					{
						SpeedBuildHud.Instance.Message = "Someone who left!";
					}
					await Task.DelayRealtimeSeconds( 5f );
				}
				if ( allPlayers.Length > 1 )
				{
					SpeedBuildHud.Instance.Message = "Second Place";
					await Task.DelayRealtimeSeconds( 3f );
					if ( allPlayers[1].IsValid() )
					{
						var silver = GameObject.Children.Find( j => j.Name == "Silver" );
						SpeedBuildHud.Instance.Message = allPlayers[1].Network.Owner.DisplayName;
						allPlayers[1].Spectator = false;
						allPlayers[1].IsFlying = false;
						allPlayers[1].MoveTo( silver.WorldTransform );
					}
					else
					{
						SpeedBuildHud.Instance.Message = "Someone who left!";
					}
					await Task.DelayRealtimeSeconds( 5f );
				}
				SpeedBuildHud.Instance.Message = "The winner is...";
				await Task.DelayRealtimeSeconds( 3f );
				if ( allPlayers[0].IsValid() )
				{
					var gold = GameObject.Children.Find( j => j.Name == "Gold" );
					SpeedBuildHud.Instance.Message = allPlayers[0].Network.Owner.DisplayName;
					if ( allPlayers.Length == 1 )
						SpeedBuildHud.Instance.Message = allPlayers[0].Network.Owner.DisplayName + " (By default)";
					allPlayers[0].Spectator = false;
					allPlayers[0].IsFlying = false;
					allPlayers[0].MoveTo( gold.WorldTransform );
				}
				else
				{
					SpeedBuildHud.Instance.Message = "Someone who left!";
				}
				await Task.DelayRealtimeSeconds( 10f ); // Take 30 seconds to celebrate!
				SpeedBuildHud.Instance.Message = "Restarting!";
				// Restart the lobby
				Scene.LoadFromFile( "scenes/speed build.scene" );
				break;
			}

			foreach ( var p in Players.Where( c => !(c?.IsValid() ?? false) ).ToList() )
				Players.Remove( p ); // Cleanup any players we lost.
			foreach ( var ply in Players )
				ply.TotalBlockArea = 0;

			// Clean up all player structures.
			foreach ( var area in CleanupAreas )
			{
				for ( int z = area.mins.z; z <= area.maxes.z; z++ )
				{
					for ( int y = area.mins.y; y <= area.maxes.y; y++ )
					{
						for ( int x = area.mins.x; x <= area.maxes.x; x++ )
						{
							World.Active.SetBlock( new Vector3Int( x, y, z ), BlockData.Empty );
						}
					}
				}
			}
			SpeedBuildHud.Instance.Message = "Get ready for the next round!";
			await Task.DelayRealtimeSeconds( 3 );
			RoundNumber++;
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
