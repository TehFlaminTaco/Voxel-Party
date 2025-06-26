
public class SlabObject : Component, IHitboxProvider, IBlockDataReceiver
{
    BlockData data;
    public void AcceptBlockData( BlockSpace world, Vector3Int pos, BlockData blockData )
    {
        data = blockData;

        if ( data.FacingFromData() == Direction.Up )
        {
            GameObject.Children.First().LocalPosition += new Vector3( 0, 0, World.BlockScale / 2f );
        }
    }

    public IEnumerable<BBox> ProvideHitboxes( BlockSpace world, Vector3Int position )
    {
        switch ( data.FacingFromData() )
        {
            case Direction.Up:
                yield return new BBox( new Vector3( 0, 0, 0.5f ), Vector3.One );
                break;
            default:
                yield return new BBox( Vector3.Zero, new Vector3( 1f, 1f, 0.5f ) );
                break;
        }
    }
}