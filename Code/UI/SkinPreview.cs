using System.Threading.Tasks;
using Sandbox.UI;

public class SkinPreview : Panel
{
    public Texture RenderTarget;
    public Scene PreviewScene;
    public CameraComponent Camera;
    public SkinnedModelRenderer Renderer;
    public PointLight Light;
    public bool Dirty = true;

    public Texture SkinTexture = null;
    public string SkinName = null;

    public SkinPreview()
    {
        PreviewScene = new Scene()
        {
            WantsSystemScene = false
        };
        using ( PreviewScene.Push() )
        {
            Camera = new GameObject().AddComponent<CameraComponent>();
            Camera.BackgroundColor = Color.Transparent;
            Renderer = new GameObject().AddComponent<SkinnedModelRenderer>();
            Renderer.Model = Model.Load( "models/player/player.vmdl" );

            Light = new GameObject().AddComponent<PointLight>();
            Light.WorldPosition = Vector3.Forward * 300f;
            Light.Radius = 600f;
            Light.LightColor = Color.White * 2f;
        }

    }

    public override void Tick()
    {
        var size = Box.Rect.Size;
        if ( size.IsNearZeroLength )
            return;
        if ( RenderTarget == null || !RenderTarget.IsValid || RenderTarget.Size != size )
        {
            RenderTarget?.Dispose(); // Destroy the old one;
            RenderTarget = Texture.CreateRenderTarget()
                .WithSize( size )
                .Create();
            Dirty = true;
        }
        if ( RenderTarget == null || !RenderTarget.IsValid )
        {
            Log.Warning( "Failed to recover RenderTarget!" );
            return;
        }

        if ( Dirty )
        {
            if ( SkinName != null )
            {
                async Task Update()
                {
                    SkinTexture = await VoxelPlayer.GetTextureFromSkin( SkinName );
                }
                _ = Update();
                SkinName = null;
            }
            if ( SkinTexture != null )
            {
                Renderer.MaterialOverride ??= Renderer.Model.Materials.First().CreateCopy();
                Renderer.MaterialOverride.Set( "Albedo", SkinTexture );
                SkinTexture = null;
            }
            Camera.WorldPosition = Renderer.GameObject.GetBounds().Center + Vector3.Forward * 100f;
            Camera.WorldRotation = Rotation.FromYaw( 180f );
            Camera.FieldOfView = 25f;
            using ( PreviewScene.Push() )
            {
                Camera.RenderToTexture( RenderTarget );
            }
            Style.BackgroundImage = RenderTarget;
            Dirty = false;
        }
    }

    public override void OnDeleted()
    {
        RenderTarget?.Dispose();
        PreviewScene?.Destroy();
    }

}
