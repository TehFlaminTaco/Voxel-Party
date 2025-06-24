
public class TorchObject : Component, Component.ExecuteInEditor, IHitboxProvider, IBlockDataReceiver
{
    BlockData data;
    public void AcceptBlockData( BlockSpace world, Vector3Int pos, BlockData blockData ) => this.data = blockData;

    public IEnumerable<BBox> ProvideHitboxes( BlockSpace world, Vector3Int position )
    {
        var facing = data.FacingFromData();
        var offsetPosition = new Vector3( 0.5f, 0.5f, 0f );
        if ( facing != Direction.Up )
        {
            offsetPosition = new Vector3( 0.5f, 0.5f, 0.25f ) - facing.Forward() * 6f / 16f;
        }
        yield return new BBox(
                new Vector3( 6, 6, 0 ) / 16f,
                new Vector3( 10, 10, 9 ) / 16f
            ).Translate( new Vector3( -0.5f, -0.5f, 0f ) + offsetPosition );
    }

    protected override void OnUpdate()
    {
        base.OnUpdate();
        var facing = data.FacingFromData();
        var mesh = GameObject.Children.First();
        if ( facing != Direction.Up )
        {
            // Move the candle to the blockface we're anti-facing
            Vector3 mbp = new Vector3( 0.5f, 0.5f, 0.25f );
            mbp -= facing.Forward() / 2f;
            mesh.LocalPosition = mbp * World.BlockScale;
            mesh.LocalRotation = Rotation.FromYaw( facing.Yaw() ) * Rotation.FromPitch( 35f );
        }
        else
        {
            mesh.LocalPosition = new Vector3( 0.5f, 0.5f, 0f ) * World.BlockScale;
            mesh.LocalRotation = Rotation.Identity;
        }
    }
}