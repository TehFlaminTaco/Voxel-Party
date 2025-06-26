using Sandbox;
using Sandbox.Rendering;

public sealed class Crosshair : Component
{
	[Property] public float DotSize { get; set; } = 2.5f;
	
	bool LookingAtBlock { get; set; }
	
	protected override void OnUpdate()
	{
		var camera = Scene?.Camera;
		if ( !camera.IsValid() )
			return;

		var screenCenter = Screen.Size / 2f;

		if ( VoxelPlayer.LocalPlayer.EyeTrace().Hit ) camera.Hud.DrawCircle( screenCenter, DotSize, Color.White );
	}
}
