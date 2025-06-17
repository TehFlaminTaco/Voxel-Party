using Editor.Audio;

[EditorTool]
[Title( "Voxel Builder" )]
[Icon( "view_in_ar" )]
[Alias( "voxelbuilder" )]
[Group( "8" )]
public class VoxelBuilder : EditorTool
{
	const float MAX_DISTANCE = 8096;
	public override void OnEnabled()
	{
		AllowGameObjectSelection = false;
	}
	
	public override void OnUpdate()
	{
		var trace = World.Active
			.Trace( Gizmo.CurrentRay.Position, Gizmo.CurrentRay.Position + Gizmo.CurrentRay.Forward * MAX_DISTANCE )
			.Run();
		var hitPos = trace.HitBlockPosition;
		var hitDirection = trace.HitFace;
		if ( !trace.Hit )
		{
			var planeTrace = new Plane( Vector3.Zero, Vector3.Up ).Trace( Gizmo.CurrentRay, true, MAX_DISTANCE );
			if ( planeTrace == null ) return;
			
			hitPos = Helpers.WorldToVoxel( planeTrace.Value );
			hitDirection = Direction.Up;
		}

		var faceCenter = (hitPos + 0.5f) * World.BlockScale + hitDirection.Forward() * World.BlockScale * 0.5f;
		Vector3 boxSize = new Vector3(World.BlockScale, World.BlockScale, World.BlockScale);
		switch (hitDirection)
		{
			case Direction.North:
			case Direction.South:
				boxSize.x *= 0.01f;
				break;
			case Direction.East:
			case Direction.West:
				boxSize.y *= 0.01f;
				break;
			case Direction.Up:
			case Direction.Down:
				boxSize.z *= 0.01f;
				break;
		}
		
		Gizmo.Draw.Color = Color.Black;
		Gizmo.Draw.LineThickness = 2f;
		Gizmo.Draw.LineBBox(BBox.FromPositionAndSize(faceCenter, boxSize));

		if ( Gizmo.WasLeftMousePressed )
		{
			World.Active.SetBlock( hitPos + hitDirection.Forward() * 1, new BlockData( ItemRegistry.GetItem( "Grass" ).ID ) );
		}
	}
}
