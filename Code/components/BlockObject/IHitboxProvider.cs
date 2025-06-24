public interface IHitboxProvider
{
    public IEnumerable<BBox> ProvideHitboxes( BlockSpace world, Vector3Int position );
}