using Sandbox;

public sealed class GameManager : Component
{
	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
	}
}
