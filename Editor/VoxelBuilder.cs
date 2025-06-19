using System;
using System.Reflection;
using System.Runtime.Serialization;
using Editor.Assets;
using Editor.Audio;

[EditorTool]
[Title( "Voxel Builder" )]
[Icon( "view_in_ar" )]
[Alias( "voxelbuilder" )]
[Group( "8" )]
public partial class VoxelBuilder : EditorTool
{

	public class Watcher
	{
		public Widget Target = null;// When this widget becomes invalid, so does the watcher.
		public Func<object> GetValue = null;
		public Func<object, int> BuildHash = ( object v ) => v.GetHashCode();
		public Action<object> onChange = null;
		public int LastHash = 0;
	}

	const float MAX_DISTANCE = 8096;
	static public int SelectedItemID { get; set; } = 0; // Default to Grass block
	static public int SelectedToolID { get; set; } = 0; // Default to Place Block tool

	public VoxelTool[] Tools = [
		new PlaceBlockTool(),
		new DeleteBlockTool(),
		new SquarePlaceTool()
	];

	private static List<Watcher> watchers = new List<Watcher>(); // I hate that this is object instead of Watcher, but C# hates me too.
	public static void RegisterWatcher( Watcher watcher )
	{
		if ( watcher == null || watcher.Target == null || watcher.GetValue == null )
			return; // Invalid watcher, don't register it.

		watchers.Add( watcher ); // Cast to object to store in the list.
	}

	Dictionary<int, ImageButton> itemButtons = new Dictionary<int, ImageButton>();
	public static WidgetWindow ToolOptionsWindow;
	public override void OnEnabled()
	{
		AllowGameObjectSelection = false;

		var palleteWindow = new WidgetWindow( SceneOverlay );

		palleteWindow.Layout = Layout.Column();

		var gridLayout = Layout.Grid();
		palleteWindow.Layout.Add( gridLayout );
		gridLayout.Margin = 16;
		palleteWindow.WindowTitle = "Voxel Pallete";

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
				gridLayout.AddCell( i % 4, i++ / 4, icon );
			}
		}

		AddOverlay( palleteWindow, TextFlag.LeftTop, 10 );

		ToolOptionsWindow = new WidgetWindow( SceneOverlay );
		ToolOptionsWindow.MinimumSize = new Vector2( 200, 16 );
		ToolOptionsWindow.Layout = Layout.Column();
		ToolOptionsWindow.WindowTitle = "Tool Options";
		ToolOptionsWindow.Layout.Margin = 16;


		AddOverlay( ToolOptionsWindow, TextFlag.RightBottom, 10 );

		var toolWindow = new WidgetWindow( SceneOverlay );
		toolWindow.Layout = Layout.Row();
		toolWindow.WindowTitle = "Voxel Builder Tools";
		toolWindow.Layout.Margin = 16;

		var toolBin = new ToolbarGroup( toolWindow, "Tools", null );
		for ( i = 0; i < Tools.Length; i++ )
		{
			int _i = i;
			var tool = Tools[i];
			tool.ToolID = i; // Assign the tool ID
			toolBin.AddButton( tool.Name, tool.Icon, () =>
			{
				if ( Tools.Length > SelectedToolID ) // If the old tool exists, deselect it
				{
					Tools[SelectedToolID].OnDeselected();
				}
				SelectedToolID = _i;
				Tools[_i].OnSelected(); // Select the new tool
			}, () => SelectedToolID == _i );
		}

		toolWindow.Layout.Add( toolBin );

		AddOverlay( toolWindow, TextFlag.LeftBottom, 10 );

	}

	Bitmap RenderItem( Item item )
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

			var right = scene.Camera.WorldRotation.Right;

			var sun = new SceneDirectionalLight( scene.SceneWorld, Rotation.FromPitch( 50 ), Color.White * 2.5f + Color.Cyan * 0.05f );
			sun.ShadowsEnabled = true;
			sun.ShadowTextureResolution = 1024;

			new SceneLight( scene.SceneWorld, scene.Camera.WorldPosition + Vector3.Up * 500.0f + right * 100.0f, 1000.0f, new Color( 1.0f, 0.9f, 0.9f ) * 50.0f );
			new SceneCubemap( scene.SceneWorld, Texture.Load( "textures/cubemaps/default2.vtex" ), BBox.FromPositionAndSize( Vector3.Zero, 1000 ) );

			camera.RenderToBitmap( tex );
			so.Delete();
		}
		scene.Destroy();
		return tex;
	}

	private static Dictionary<KeyCode, bool> WasKeyDown = new Dictionary<KeyCode, bool>();
	private static VoxelTool LastTool = null;
	private static TimeSince holdStart = 0f;
	public override void OnUpdate()
	{
		watchers.RemoveAll( w => w == null || w.Target == null || !w.Target.IsValid() );
		foreach ( var watcher in watchers )
		{
			if ( watcher.Target == null || !watcher.Target.IsValid() )
				continue; // Impossible?

			var value = watcher.GetValue();
			var hash = watcher.BuildHash( value );
			if ( hash != watcher.LastHash )
			{
				watcher.onChange?.Invoke( value );
				watcher.LastHash = hash;
			}
		}

		foreach ( var t in Tools.Where( t => t.Shortcut.HasValue ) )
		{
			if ( Editor.Application.IsKeyDown( t.Shortcut.Value ) )
			{
				if ( !WasKeyDown.GetValueOrDefault( t.Shortcut.Value, false ) )
				{
					holdStart = 0f;
					if ( Tools.Length > SelectedToolID )
						LastTool = Tools[SelectedToolID];
				}
				if ( SelectedToolID != t.ToolID )
				{
					// Deselect the previous tool
					if ( Tools.Length > SelectedToolID )
					{
						Tools[SelectedToolID].OnDeselected();
					}
					SelectedToolID = t.ToolID;
					// Select the new tool
					t.OnSelected();
				}
			}
		}


		var tool = SelectedToolID < Tools.Length ? Tools[SelectedToolID] : null;
		if ( tool == null )
			return; // IMPOSSIBLE?

		if ( tool.Shortcut.HasValue && !Editor.Application.IsKeyDown( tool.Shortcut.Value ) )
		{
			if ( LastTool != null && holdStart > 0.3f ) // If we're holding it down instead of pressing it, return to the LastTool on release.
			{
				tool.OnDeselected();
				SelectedToolID = LastTool?.ToolID ?? 0; // Fallback to the first tool if LastTool is null
				LastTool.OnSelected();
				tool = LastTool;
			}
			LastTool = null;
		}

		foreach ( var t in Tools.Where( t => t.Shortcut.HasValue ) )
		{
			WasKeyDown[t.Shortcut.Value] = Editor.Application.IsKeyDown( t.Shortcut.Value );
		}

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
		using ( Gizmo.Scope( "ToolGizmos" ) )
			tool.DrawGizmos( hitPos, hitDirection );
		if ( Gizmo.WasLeftMousePressed )
		{
			tool.LeftMousePressed( hitPos, hitDirection );
		}
		else if ( Gizmo.IsLeftMouseDown )
		{
			tool.LeftMouseDown( hitPos, hitDirection );
		}
		else if ( Gizmo.WasLeftMouseReleased )
		{
			tool.LeftMouseUp( hitPos, hitDirection );
		}
	}
}
