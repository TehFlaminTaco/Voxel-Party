using Sandbox.UI;

public partial class ItemIcon : Panel
{
    public Inventory Inventory => VoxelPlayer.LocalPlayer.inventory;
    public int Slot { get; set; } = -1;

    public Texture RenderTexture;

    public override void OnDeleted()
    {
        RenderTexture?.Dispose();
        base.OnDeleted();
    }

    public void Render()
    {
        var inv = Inventory;
        var item = inv.GetItem( Slot ).Item;
        if ( item == null ) return;
        RenderTexture = Texture.CreateRenderTarget( "Item", ImageFormat.RGBA8888, new Vector2( 100, 100 ) );
        Scene scene = new Scene();
        using ( scene.Push() )
        {
            var _so = new SceneCustomObject( scene.SceneWorld );
            _so.Transform = global::Transform.Zero;
            _so.Flags.CastShadows = false;
            _so.Flags.IsOpaque = true;
            _so.Flags.IsTranslucent = false;

            _so.RenderOverride = ( obj ) =>
            {
                item.Render( global::Transform.Zero.WithPosition( new Vector3( 0f, -5f, -2f ) ).WithRotation( Rotation.FromAxis( Vector3.Right, 35f ) * Rotation.FromAxis( Vector3.Up, 45 ) ) );
            };

            var light = new GameObject().AddComponent<DirectionalLight>();
            light.LightColor = Color.White * 2f;
            light.WorldRotation = Rotation.From( 45, 45, 0 );

            var camera = new GameObject().AddComponent<CameraComponent>();
            camera.Orthographic = true;
            camera.OrthographicHeight = 250f;
            camera.WorldPosition = new Vector3( -50, 0, 0 );
            camera.WorldRotation = Rotation.From( 0, 0, 0 );
            camera.BackgroundColor = Color.Transparent;
            camera.RenderTarget = RenderTexture;
            camera.RenderToTexture( RenderTexture );
            _so.Delete();
        }
        scene.Destroy();
    }

    ItemStack lastKnownStack = ItemStack.Empty;
    public override void Tick()
    {
        var item = Inventory.GetItem( Slot );
        if ( RenderTexture == null || item.Count != lastKnownStack?.Count || item.ItemID != lastKnownStack?.ItemID )
        {
            lastKnownStack = item;
            Render();
        }
        if ( ItemStack.IsNullOrEmpty( item ) )
        {
            Style.BackgroundImage = null;
            return;
        }
        Style.BackgroundImage = RenderTexture;

    }
}
