using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Editor.Assets;
using Facepunch.ActionGraphs;

namespace Sandbox;

[AssetPreview( "item" )]
public class ItemPreview : AssetPreview
{
	Item item;
	Texture texture;
	BlockSpace fakeWorld = new();

	// We're just rendering a still image, so we don't need to render video thumbnails
	public override bool IsAnimatedPreview => item.IsBlock;
	public ItemPreview( Asset asset ) : base( asset )
	{
		item = asset.LoadResource<Item>();
		texture = item.Block?.Texture;
		if ( item.TryGiveID() )
		{
			if ( string.IsNullOrWhiteSpace( item.Name ) )
			{
				item.Name = new Regex( @"\b(\w)" ).Replace( asset.Name.Replace( ".item", "" ).Replace( "_", " " ), c => c.Value.ToUpper() );
			}
			asset.SaveToDisk( item );
		}
	}


	SceneCustomObject so;
	Scene prefabScene;
	SceneLight light;
	public override async Task InitializeAsset()
	{
		await Task.Yield();

		if ( item.Block?.BlockObject != null )
		{
			var prefab = item.Block.BlockObject;
			prefabScene = new Scene()
			{
				WantsSystemScene = false
			};
			using ( prefabScene.Push() )
			{
				prefab.Clone( prefabScene, Vector3.Zero, Rotation.Identity, Vector3.One );
			}
			this.World = prefabScene.SceneWorld;
			Camera = new SceneCamera();
			Camera.World = World;
			Camera.BackgroundColor = Color.Transparent;
			Camera.Angles = new Angles( 20, 180 + 45, 0 );
			Camera.FieldOfView = 30.0f;
			Camera.ZFar = 15000.0f;
			Camera.World.AmbientLightColor = Color.White * 0.05f;
			SceneSize = new Vector3( 40 );
			SceneCenter = new Vector3( 20 );

			light = new SceneLight( World, Camera.Position + Vector3.Up * 500.0f + Vector3.Backward * 100.0f, 500f, new Color( 1.0f, 0.9f, 0.9f ) * 50.0f );

		}
		else if ( item.Block == null )
		{

		}
		else
		{
			so = new SceneCustomObject( World );
			so.Transform = Transform.Zero;
			so.Flags.CastShadows = false;
			so.Flags.IsOpaque = true;

			so.RenderOverride = RenderObject;

			PrimarySceneObject = so;
		}

	}

	private void RenderObject( SceneObject obj )
	{
		item.Render( Transform.Zero );
		SceneSize = new Vector3( 10 );
		SceneCenter = new Vector3( 5 );
	}

	public override void Dispose()
	{
		if ( so?.IsValid() ?? false )
			so.Delete();
		if ( prefabScene?.IsValid() ?? false )
			prefabScene.Destroy();
		base.Dispose();
	}

	public override void UpdateScene( float cycle, float timeStep )
	{
		Camera.Rotation = Rotation.FromYaw( timeStep * 30 ) * Camera.Rotation;
		if ( prefabScene?.IsValid() ?? false )
		{
			PrimarySceneObject = World.SceneObjects.First();
		}
		if ( PrimarySceneObject?.IsValid() ?? false )
			FrameScene();
		if ( light?.IsValid() ?? false )
		{
			light.Position = Camera.Position + Vector3.Up;
		}
	}
}
