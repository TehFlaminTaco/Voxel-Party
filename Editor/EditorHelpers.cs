using System;

public static class EditorHelpers
{
	public static void Watch<T>( this Widget widget, Func<T> getValue, Action<T> onChange, Func<T, int> buildHash = null )
	{
		if ( widget == null ) return;

		var watcher = new VoxelBuilder.Watcher
		{
			Target = widget,
			GetValue = () => getValue(),
			onChange = o => onChange( (T)o )
		};
		if ( buildHash != null )
		{
			watcher.BuildHash = o => buildHash( (T)o );
		}

		watcher.LastHash = watcher.BuildHash( watcher.GetValue() );
		VoxelBuilder.RegisterWatcher( watcher );
	}

	public static void QuickAddUndo( this SceneEditorSession editor, string name, Action undo, Action redo )
	{
		editor.AddUndo( name, undo, redo );
		redo.Invoke(); // Immediately apply the redo action to the scene.
	}

	public static Bitmap RenderItem( Item item )
	{
		var tex = new Bitmap( 100, 100 );
		Scene scene = new Scene();
		using ( scene.Push() )
		{
			var camera = new GameObject().AddComponent<CameraComponent>();
			camera.Orthographic = true;
			camera.OrthographicHeight = 17f;
			camera.WorldPosition = new Vector3( -50, 0, 10000 );
			camera.WorldRotation = Rotation.From( 0, 0, 0 );
			camera.BackgroundColor = Color.Transparent;


			SceneCustomObject so = null;
			if ( item.Block.BlockObject != null )
			{
				var obj = item.Block.BlockObject.Clone( new Vector3( 0, 0, 10000 ), Rotation.FromAxis( Vector3.Right, 35f ) * Rotation.FromAxis( Vector3.Up, 45 ) );
				camera.WorldPosition = new Vector3( -50 * World.BlockScale, 20f * (float)Math.Sqrt( 2f ), 10000 + 20f * MathF.Sin( 35f / (180f / MathF.PI) ) );
				camera.OrthographicHeight *= 4;
			}
			else
			{
				so = new SceneCustomObject( scene.SceneWorld );
				so.Transform = global::Transform.Zero.WithPosition( new Vector3( 0, 0, 10000 ) );
				so.Flags.CastShadows = false;
				so.Flags.IsOpaque = true;
				so.Flags.IsTranslucent = false;

				so.RenderOverride = ( obj ) =>
				{
					item.Render( Transform.Zero.WithPosition( new Vector3( 0f, -7f, -4f ) ).WithRotation( Rotation.FromAxis( Vector3.Right, 35f ) * Rotation.FromAxis( Vector3.Up, 45 ) ) );
				};
			}
			var right = scene.Camera.WorldRotation.Right;

			var sun = new SceneDirectionalLight( scene.SceneWorld, Rotation.FromPitch( 35 ), (Color.White * 2.5f + Color.Cyan * 0.05f) * 0.5f );
			sun.ShadowsEnabled = false;

			new SceneLight( scene.SceneWorld, camera.WorldPosition + Vector3.Up * 50f - Vector3.Right * 50f, 500f, new Color( 1.0f, 0.9f, 0.9f ) * 1.0f );
			new SceneCubemap( scene.SceneWorld, Texture.Load( "textures/cubemaps/default2.vtex" ), BBox.FromPositionAndSize( Vector3.Zero, 1000 ) );

			camera.RenderToBitmap( tex );
			so?.Delete();
		}
		scene.Destroy();
		return tex;
	}
}
