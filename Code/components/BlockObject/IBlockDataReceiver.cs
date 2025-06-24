// An ultra-simple interface for receiving block data from a block object.
// We use this for custom block objects so they can update their state based on the block data they represent.
public interface IBlockDataReceiver
{
    /// <summary>
    /// Accepts block data from a block object.
    /// </summary>
    /// <param name="blockData">The block data to accept.</param>
    void AcceptBlockData( BlockSpace world, Vector3Int pos, BlockData blockData );
}