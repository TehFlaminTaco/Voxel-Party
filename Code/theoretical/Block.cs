using Sandbox;
using Sandbox.UI;

/** An instance of Block describes the behaviour and properties of a block in the world.
 * All eg. Stone, refer to the same Block instance which is mapped via BlockData.BlockID.
 */
public class Block {
	public int BlockID { get; set; } // Unique ID for this block type.

	public virtual string Name { get; set; } // Name of the block type, e.g. "Stone", "Dirt".
	public virtual bool Opaque { get; set; } = true; // Whether the block is opaque (blocks light).
	public virtual bool IsSolidBlock { get; set; } = true; // Is this block considered a full solid block
	public virtual bool IsSolid { get; set; } = true; // Whether the block is solid (blocks movement).

	public virtual int Hardness { get; set; } = 1; // Hardness of the block, used for mining speed calculations.

	public virtual Vector2Int TextureIndex { get; set; } = Vector2Int.Zero; // Default texture index for the block, used for rendering.
	public virtual Vector2Int? TopTextureIndex { get; set; } = null; // Default texture index for the top face of the block.
	public virtual Vector2Int? SideTextureIndex { get; set; } = null; // Default texture index for the side faces of the block.
	public virtual Vector2Int? NorthTextureIndex { get; set; } = null; // Default texture index for the north face of the block.
	public virtual Vector2Int? SouthTextureIndex { get; set; } = null; // Default texture index for the south face of the block.
	public virtual Vector2Int? EastTextureIndex { get; set; } = null; // Default texture index for the east face of the block.
	public virtual Vector2Int? WestTextureIndex { get; set; } = null; // Default texture index for the west face of the block.
	public virtual Vector2Int? BottomTextureIndex { get; set; } = null; // Default texture index for the bottom face of the block.
	public virtual ItemStack[] Drops { get; set; } = []; // Items that drop when the block is broken, default is empty stack.

	public virtual Vector2Int GetTextureIndex( World world, Vector3Int blockPos, Direction face ) => face switch {
		Direction.North => NorthTextureIndex ?? SideTextureIndex ?? TextureIndex,
		Direction.South => SouthTextureIndex ?? SideTextureIndex ?? TextureIndex,
		Direction.East => EastTextureIndex ?? SideTextureIndex ?? TextureIndex,
		Direction.West => WestTextureIndex ?? SideTextureIndex ?? TextureIndex,
		Direction.Up => TopTextureIndex ?? TextureIndex,
		Direction.Down => BottomTextureIndex ?? SideTextureIndex ?? TextureIndex,
		_ => TextureIndex
	}; // Returns the texture index for the block face at the given position.

	public virtual void AddBlockMesh( World world, Vector3Int blockPos, List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs ) {
		if ( !IsSolidBlock )
			return; // If we are not a solid block, this behaviour should be overridden, otherwise, we don't render anything.
					// For each face, call AddFaceMesh with the appropriate parameters.
		foreach ( var dir in Directions.All ) {
			if ( ShouldFaceBeVisible( world, blockPos, dir ) ) {
				AddFaceMesh( world, blockPos, dir, verts, normals, uvs );
			}
		}
	}

	public virtual BBox GetCollisionAABB( World world, Vector3Int blockPos ) {
		Vector3 pos = blockPos; // Convert block position to world position.
		pos = pos.Modulo( Chunk.SIZE ); // Ensure the position is within the chunk bounds.
		pos += 0.5f; // Offset the position to the center of the block.
		pos *= World.BlockScale;
		return BBox.FromPositionAndSize( pos, World.BlockScale / 2f ); // Create a bounding box from the position and size of the block.
	}

	public virtual ItemStack[] GetDrops( World world, Vector3Int blockPos ) {
		// Returns the items that drop when the block is broken.
		// Default implementation returns the Drops property.
		return Drops;
	}

	public virtual void AddFaceMesh( World world, Vector3Int blockPos, Direction face, List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs ) {
		// This method should be overridden to add the mesh for a specific face of the block.
		// For example, it could add vertices, normals, and UVs for the specified face.
		var textureIndex = GetTextureIndex( world, blockPos, face );
		var forward = face.Forward();
		var up = face.Up();
		var right = face.Right();
		// Add vertices, normals, and UVs based on the face direction.
		var ourVerts = new List<Vector3> {
			forward + up + right,
			forward + up - right,
			forward - up - right,
			forward + up + right,
			forward - up - right,
			forward - up + right,
		};
		// These verts are in the range -1 to 1, so we move them to the range of 0 - 1, add the block position, and scale by the block size.
		verts.AddRange( ourVerts.Select( v => ((v + Vector3.One) / 2f + ((Vector3)blockPos).Modulo( Chunk.SIZE )) * World.BlockScale ) );
		normals.Add( forward );
		normals.Add( forward );
		normals.Add( forward );
		normals.Add( forward );
		normals.Add( forward );
		normals.Add( forward );
		var ourUVs = new List<Vector2> {
			textureIndex + new Vector2(0.9f, 0.1f), // Top right
			textureIndex + new Vector2(0.1f, 0.1f), // Top left
			textureIndex + new Vector2(0.1f, 0.9f), // Bottom left
			textureIndex + new Vector2(0.9f, 0.1f), // Top right
			textureIndex + new Vector2(0.1f, 0.9f), // Bottom left
			textureIndex + new Vector2(0.9f, 0.9f), // Bottom right
		};
		uvs.AddRange( ourUVs.Select( v => v / new Vector2( 16, 16 ) ) ); // Assuming a texture atlas of 16x16, scale UVs to [0, 1].
	}

	public virtual bool IsFaceSolid( World world, Vector3Int blockPos, Direction face ) {
		// Check if the face is solid by checking the block's properties.
		return IsSolidBlock && Opaque;
	}

	public virtual bool ShouldFaceBeVisible( World world, Vector3Int blockPos, Direction face ) {
		// If we are not opaque, we always show the face.
		if ( !Opaque ) return true;
		// Check if the adjacent block in the direction of the face is solid.
		var adjacentPos = blockPos + face.Forward();
		var adjacentBlockData = world.GetBlock( adjacentPos );
		var adjacentBlock = BlockRegistry.GetBlock( adjacentBlockData.BlockID );
		if ( adjacentBlock == null ) {
			return true; // If no adjacent block, show the face.
		}
		if ( adjacentBlock.IsFaceSolid( world, adjacentPos, face.Flip() ) ) {
			return false; // If the adjacent block's opposite face is solid, don't show this face.
		}
		return true; // Otherwise, show the face.
	}
}
