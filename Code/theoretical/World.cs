using System;
using Sandbox;

public class World : BlockSpace {
	public static readonly int BlockScale = 40; // How many units per block.
	public WorldThinker Thinker => Game.ActiveScene.Get<WorldThinker>();

	public static World Active = null;

	public World() {
		Active = this; // Set the active world to this instance.
		BlockRegistry.UpdateRegistry(); // Ensure the block registry is up to date.
		ItemRegistry.UpdateRegistry(); // Ensure the item registry is up to date.
		MakeSpawnPlatform();
	}

	public void MakeSpawnPlatform() {
		// Create a simple dirt platform at the origin.
		for ( int z = -1; z <= 1; z++ ) {
			for ( int y = -1; y <= 4; y++ ) {
				for ( int x = -1; x <= 4; x++ ) {
					if ( x > 1 && y > 1 ) continue;
					var pos = new Vector3Int( x, y, z );
					SetBlock( pos, new BlockData( z < 1 ? Blocks.Dirt.BlockID : Blocks.Grass.BlockID ) );
				}
			}
		}
	}

	public override BlockData GenerateBlock( Vector3Int position ) {
		return new BlockData( 0 ); // Assuming 0 is the ID for air.
	}
}
