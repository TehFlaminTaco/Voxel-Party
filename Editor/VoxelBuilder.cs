using System.Runtime.Serialization;
using Editor.Audio;

[EditorTool]
[Title( "Voxel Builder" )]
[Icon( "view_in_ar" )]
[Alias( "voxelbuilder" )]
[Group( "8" )]
public class VoxelBuilder : EditorTool
{
	const float MAX_DISTANCE = 8096;
	public int SelectedItemID { get; set; } = 0; // Default to Grass block
	Dictionary<int, ImageButton> itemButtons = new Dictionary<int, ImageButton>();
	public override void OnEnabled()
	{
		AllowGameObjectSelection = false;

		var window = new WidgetWindow( SceneOverlay );
		window.Layout = Layout.Grid();
		window.Layout.Margin = 16;

		window.WindowTitle = "Voxel Pallete";
		int i = 0;
		foreach ( var item in ResourceLibrary.GetAll<Item>().OrderBy( c => c.ID ) )
		{
			if ( item.ID == 0 ) continue; // Skip empty item
			if ( item.IsBlock )
			{
				var icon = new ImageButton();
				itemButtons[item.ID] = icon;
				using ( var tex = RenderItem( item ) )
				{
					icon.Texture = tex;
				}
				icon.FixedSize = new Vector2( 64, 64 );
				icon.Tint = SelectedItemID == item.ID ? Color.White.WithAlpha( 0.2f ) : Color.White.WithAlpha( 0f );
				icon.Clicked = () =>
				{
					if ( itemButtons.ContainsKey( SelectedItemID ) )
					{
						itemButtons[SelectedItemID].Tint = Color.White.WithAlpha( 0f );
					}
					SelectedItemID = item.ID;
					icon.Tint = Color.White.WithAlpha( 0.2f );
				};
				icon.ToolTip = $"{item.Name} (ID: {item.ID})";
				(window.Layout as GridLayout).AddCell( i % 4, i++ / 4, icon );
			}
		}

		AddOverlay( window, TextFlag.RightTop, 10 );
	}

	private Bitmap RenderItem( Item item )
	{
		var tex = new Bitmap( 100, 100 );
		Scene scene = new Scene();
		using ( scene.Push() )
		{
			var so = new SceneCustomObject( scene.SceneWorld );
			so.Transform = global::Transform.Zero.WithPosition( new Vector3( 0, 0, 10000 ) );
			so.Flags.CastShadows = false;
			so.Flags.IsOpaque = true;
			so.Flags.IsTranslucent = false;

			so.RenderOverride = ( obj ) =>
			{
				item.Render( global::Transform.Zero.WithPosition( new Vector3( 0f, -7f, -4f ) ).WithRotation( Rotation.FromAxis( Vector3.Right, 35f ) * Rotation.FromAxis( Vector3.Up, 45 ) ) );
			};

			var camera = new GameObject().AddComponent<CameraComponent>();
			camera.Orthographic = true;
			camera.OrthographicHeight = 17f;
			camera.WorldPosition = new Vector3( -50, 0, 10000 );
			camera.WorldRotation = Rotation.From( 0, 0, 0 );
			camera.BackgroundColor = Color.Transparent;
			camera.RenderToBitmap( tex );
			so.Delete();
		}
		scene.Destroy();
		return tex;
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

		if ( Gizmo.KeyboardModifiers.HasFlag( KeyboardModifiers.Ctrl ) )
		{
			var boxSize = new Vector3( World.BlockScale, World.BlockScale, World.BlockScale );
			Gizmo.Draw.Color = Color.Red;
			Gizmo.Draw.LineThickness = 2f;
			Gizmo.Draw.LineBBox( BBox.FromPositionAndSize( (hitPos + Vector3.One / 2f) * World.BlockScale, boxSize ) );
			if ( Gizmo.WasLeftMousePressed )
			{
				World.Active.SetBlock( hitPos, new BlockData( 0 ) );
			}
		}
		else
		{
			var faceCenter = (hitPos + 0.5f) * World.BlockScale + hitDirection.Forward() * World.BlockScale * 0.5f;
			Vector3 boxSize = new Vector3( World.BlockScale, World.BlockScale, World.BlockScale );
			switch ( hitDirection )
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
			Gizmo.Draw.LineBBox( BBox.FromPositionAndSize( faceCenter, boxSize ) );

			if ( Gizmo.WasLeftMousePressed )
			{
				World.Active.SetBlock( hitPos + hitDirection.Forward() * 1, new BlockData( SelectedItemID ) );
			}
		}
	}
}
