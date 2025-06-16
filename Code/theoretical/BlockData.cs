using Sandbox;

public struct BlockData {
	public byte BlockID;
	public byte BlockDataValue;

	public BlockData( byte blockID, byte blockDataValue ) {
		BlockID = blockID;
		BlockDataValue = blockDataValue;
	}

	public BlockData( int blockType ) : this( (byte)blockType, 0 ) { }

	public Block GetBlock() {
		return BlockRegistry.GetBlock( BlockID );
	}
}
