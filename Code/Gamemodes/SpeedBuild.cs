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

	[Property]
	public int[] MemorizeTimeSecondsByRound { get; set; } = new[]{
		45, // Nice and easy
		30, // A bit more stressful
		15,
		15,
		10,
		10,
		10
	};
	[Property]
	public int[] BuildTimeSecondsByRound { get; set; } = new[]{
		120, // Nice and easy
		60, // A bit more stressful
		60,
		60,
		50,
		40,
		30 // 30 seconds is NOT enough time for any structure.
	};

	[Property]
	public int[] TargetBuildPercentageByRound { get; set; } = new[]{
		40,
		50,
		60,
		70,
		80,
		90,
		90
	};

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

		List<VoxelPlayer> HistoricalPlayers = new();

		foreach ( var player in Scene.GetAll<VoxelPlayer>().Where( c => c.IsReady ) )
		{
			Players.Add( player );
		}
		foreach ( var player in Scene.GetAll<VoxelPlayer>().Where( c => !c.IsReady ) )
		{
			player.Spectator = true;
		}

		bool gameRunning = true;
		int RoundNumber = 0;
		while ( gameRunning )
		{

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

			SpeedBuildHud.Instance.TimerEnd = MemorizeTimeSecondsByRound[RoundNumber]; // Set the timer for 60 seconds
			SpeedBuildHud.Instance.TotalTime = MemorizeTimeSecondsByRound[RoundNumber]; // Set the total time for the game mode	
			SpeedBuildHud.Instance.HasTimer = true; // Show the timer UI

			SpeedBuildHud.Instance.Message = "Memorize the structure!";

			await Task.DelayRealtimeSeconds( MemorizeTimeSecondsByRound[RoundNumber] ); // Wait for players to memorize the structure

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

			await Task.DelayRealtimeSeconds( 2 ); // Wait for the build phase

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
			SpeedBuildHud.Instance.TimerEnd = BuildTimeSecondsByRound[RoundNumber]; // Set the timer for 5 minutes
			SpeedBuildHud.Instance.TotalTime = BuildTimeSecondsByRound[RoundNumber]; // Set the total time for the game mode
			SpeedBuildHud.Instance.HasTimer = true; // Show the timer UI

			await Task.DelayRealtimeSeconds( BuildTimeSecondsByRound[RoundNumber] ); // Wait for the build phase to end

			SpeedBuildHud.Instance.Message = "Time's up! Judging the builds...";
			SpeedBuildHud.Instance.HasTimer = false; // Hide the timer UI

			if ( Players.Count > 3 )
			{
				SpeedBuildHud.Instance.KillPercentage = TargetBuildPercentageByRound[RoundNumber];
			}
			else
			{
				SpeedBuildHud.Instance.KillPercentage = null;
			}

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
			foreach ( var player in Players )
			{
				ScoreByPlayer[player] = player.CorrectBlocksPlaced * 100 / validBlocks;
			}

			if ( Players.Count > 3 )
			{
				SpeedBuildHud.Instance.Message = "Executing the failures!";
				await Task.DelayRealtimeSeconds( 1 );
				var losers = Players.Where( k => ScoreByPlayer[k] < TargetBuildPercentageByRound[RoundNumber] ).OrderByDescending( k => ScoreByPlayer[k] ).ToList();
				foreach ( var player in losers )
				{
					if ( ScoreByPlayer[player] < TargetBuildPercentageByRound[RoundNumber] )
					{
						player.Explode();
						await Task.DelayRealtime( 500 );
					}
				}
				HistoricalPlayers.AddRange( losers.Reverse<VoxelPlayer>() );
				foreach ( var loser in losers )
					Players.Remove( loser );
			}
			if ( Players.Count <= 3 )
			{
				SpeedBuildHud.Instance.Message = "Declaring winners...";
				var podiumObject = GameObject.Children.Find( j => j.Name == "PodiumCamera" );
				foreach ( var ply in Scene.GetAll<VoxelPlayer>() )
				{
					ply.Spectator = true;
					ply.Enabled = false;

					var pc = ply.GetComponent<PlayerController>();
					pc.UseCameraControls = false;
				}
				var cam = Scene.Camera;
				cam.WorldPosition = podiumObject.WorldPosition;
				cam.WorldRotation = podiumObject.WorldRotation;
				cam.RenderExcludeTags.Remove( "viewer" );

				var allPlayers = Players.OrderByDescending( k => ScoreByPlayer[k] ).Concat( HistoricalPlayers.Reverse<VoxelPlayer>() ).ToArray();
				await Task.DelayRealtimeSeconds( 1 );
				if ( allPlayers.Length > 2 )
				{
					SpeedBuildHud.Instance.Message = "Third Place";
					await Task.DelayRealtimeSeconds( 2f );
					var bronze = GameObject.Children.Find( j => j.Name == "Bronze" );
					SpeedBuildHud.Instance.Message = allPlayers[2].Network.Owner.DisplayName;
					allPlayers[2].Spectator = false;
					allPlayers[2].IsFlying = false;
					allPlayers[2].WorldPosition = bronze.WorldPosition;
					allPlayers[2].GetComponent<PlayerController>().EyeAngles = bronze.WorldRotation.Angles();
					allPlayers[2].GameObject.Children.First().WorldRotation = allPlayers[2].WorldRotation = bronze.WorldRotation;
					await Task.DelayRealtimeSeconds( 3f );
				}
				if ( allPlayers.Length > 1 )
				{
					SpeedBuildHud.Instance.Message = "Second Place";
					await Task.DelayRealtimeSeconds( 2f );
					var silver = GameObject.Children.Find( j => j.Name == "Silver" );
					SpeedBuildHud.Instance.Message = allPlayers[1].Network.Owner.DisplayName;
					allPlayers[1].Spectator = false;
					allPlayers[1].IsFlying = false;
					allPlayers[1].WorldPosition = silver.WorldPosition;
					allPlayers[1].GetComponent<PlayerController>().EyeAngles = silver.WorldRotation.Angles();
					allPlayers[1].GameObject.Children.First().WorldRotation = allPlayers[1].WorldRotation = silver.WorldRotation;
					await Task.DelayRealtimeSeconds( 3f );
				}
				SpeedBuildHud.Instance.Message = "The winner is...";
				await Task.DelayRealtimeSeconds( 2f );
				var gold = GameObject.Children.Find( j => j.Name == "Gold" );
				SpeedBuildHud.Instance.Message = allPlayers[0].Network.Owner.DisplayName;
				if ( allPlayers.Length == 1 )
					SpeedBuildHud.Instance.Message = allPlayers[0].Network.Owner.DisplayName + " (By default)";
				allPlayers[0].Spectator = false;
				allPlayers[0].IsFlying = false;
				allPlayers[0].WorldPosition = gold.WorldPosition;
				allPlayers[0].GetComponent<PlayerController>().EyeAngles = gold.WorldRotation.Angles();
				allPlayers[0].GameObject.Children.First().WorldRotation = allPlayers[0].WorldRotation = gold.WorldRotation;
				await Task.DelayRealtimeSeconds( 3f );
				SpeedBuildHud.Instance.Message = "Restarting!";
				// Restart the lobby
				Scene.LoadFromFile( "scenes/speed build.scene" );
				break;

			}
			SpeedBuildHud.Instance.Message = "Get ready for the next round!";
			await Task.DelayRealtimeSeconds( 3 );
			RoundNumber++;
			int numberElements = Math.Min( Math.Min( MemorizeTimeSecondsByRound.Length, BuildTimeSecondsByRound.Length ), TargetBuildPercentageByRound.Length );
			if ( RoundNumber >= numberElements )
			{
				RoundNumber = numberElements;
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
