using System.Threading.Tasks;
using Sandbox;

public sealed class SpeedBuild : Component
{
	[Property] List<GameObject> Islands { get; set; } = new();
	[Property] GameObject Spawn { get; set; }

	public NetList<PlayerController> Players { get; set; } = new();
	State CurrentState { get; set; } = State.PreRound;

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		//if ( Players.Count <= 1 ) EndRound( Players.FirstOrDefault() );

		switch ( CurrentState )
		{
			case State.PreRound:
				PreRound();
				break;
		}
	}

	[Rpc.Broadcast]
	public void SetPlayerTransform( PlayerController player, Vector3 position, Rotation rotation )
	{
		player.WorldPosition = position;
		player.EyeAngles = rotation;
	}
	
	public async void PreRound()
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
		
		var index = 0;
		foreach ( var i in Players )
		{
			Islands[index].Enabled = true;
			SetPlayerTransform( i, Islands[index].WorldPosition + Vector3.Up * 40f, Rotation.LookAt(Vector3.Zero).Angles().WithRoll( 0 ) );
			index++;
		}
		
		CurrentState = State.Starting;
	}

	public void EndRound( PlayerController winner )
	{
		Log.Info( winner.Network.Owner.DisplayName + " won!" );
	}
	
	//repeat until there's 1 player left:
	//place build to replicate, optionally layer by layer
	//after 10 seconds, delete build
	//after 60 seconds, place build in the center island
	//compare every player's score, lowest scoring player is killed and their island removed, then given a spectator camera
	
	//Put top 3 on a pillar, confetti and shit
	//wait 15 seconds
	//reload the scene

	enum State
	{
		PreRound,
		Starting,
		Memorizing,
		Building,
		PostRound
	}
}
