using System.Threading.Tasks;
using Editor.Assets;

namespace Sandbox;

[AssetPreview( "item" )]
public class ItemPreview : AssetPreview
{
	SceneDynamicObject so;
	Item item;
	Texture texture;

	// We're just rendering a still image, so we don't need to render video thumbnails
	public override bool IsAnimatedPreview => item.IsBlock;

	public ItemPreview( Asset asset ) : base( asset )
	{
		item = asset.LoadResource<Item>();
		texture = item.Block.Texture;
	}

	public override Task InitializeAsset()
	{
		// Create a new object that we will use to render the texture
		so = new SceneDynamicObject( World );
		so.Transform = Transform.Zero;
		so.Material = Material.FromShader( "shaders/sprite.shader" );
		so.Flags.CastShadows = false;
		so.Attributes.Set( "BaseTexture", texture );

		// Set the primary scene object so the Camera keeps it in view
		PrimarySceneObject = so;

		return Task.CompletedTask;
	}
}
