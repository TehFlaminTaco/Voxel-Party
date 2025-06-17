using Sandbox.theoretical;

namespace Sandbox;

public class StructureLoader : Component, Component.ExecuteInEditor
{
	[Property, Alias( "Structure" )] public Structure LoadedStructure { get; set; }
	public BlockData BlockData { get; set; }

	[Button]
	public void StampStructure()
	{
		if ( LoadedStructure == null )
		{
			Log.Warning( "No structure loaded to stamp." );
			return;
		}
		if ( !LoadedStructure.IsValid() )
		{
			Log.Warning( "Loaded structure is not valid." );
			return;
		}

		if ( !Game.IsEditor )
		{
			Log.Error( "Stamping structures is only supported in the editor." );
			return;
		}

		var worldPosition = Helpers.WorldToVoxel( WorldPosition );
		World.Active.LoadStructure( worldPosition, LoadedStructure.StructureData );
		GameObject.Destroy();
	}

	protected override void OnEnabled()
	{
		foreach ( var child in GameObject.Children.ToList() )
		{
			child.Destroy();
		}
		if ( !Game.IsEditor )
		{
			World.Active.LoadStructure( Helpers.WorldToVoxel( WorldPosition ), LoadedStructure.StructureData );
			return;
		}

		var oldWorld = World.Active;
		var tempThinker = AddComponent<WorldThinker>();
		tempThinker.TextureAtlas = Material.Load( "materials/textureatlas.vmat" );
		try
		{
			World.Active = tempThinker.World;
			World.Active.LoadStructure( Vector3Int.Zero, LoadedStructure.StructureData );
			// Force load all chunks in the radius around the structure
			var bounds = World.Active.GetStructureBounds( LoadedStructure.StructureData );
			var chunkMax = (bounds / 16f).Ceil();
			for ( int z = 0; z <= chunkMax.z; z++ )
			{
				for ( int y = 0; y <= chunkMax.y; y++ )
				{
					for ( int x = 0; x <= chunkMax.x; x++ )
					{
						var chunkPos = new Vector3Int( x, y, z );
						var chunk = World.Active.GetChunk( chunkPos );
						var chunkObj = chunk.Render( Scene, tempThinker );
						if ( chunkObj == null ) continue; // Skip if chunk is empty
						chunkObj.WorldThinkerInstanceOverride = tempThinker;
						chunkObj.UpdateMesh();
						chunkObj.Destroy();
					}
				}
			}
		}
		finally
		{
			tempThinker.Destroy(); // Clean up the temporary thinker.
			World.Active = oldWorld; // Restore the active world after loading.
		}
		if ( Game.IsEditor ) return;
	}

	protected override void OnValidate()
	{
		base.OnValidate();
		this.OnEnabled(); // Just re-run the OnEnabled logic to ensure the structure is loaded in the editor.
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		if ( !Game.IsEditor ) return;
		foreach ( var child in GameObject.Children.ToList() )
		{
			child.Destroy();
		}
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		var blockPosition = Helpers.WorldToVoxel( WorldPosition ) * World.BlockScale;
		// Move all chunks to the world position of this component
		foreach ( var child in GameObject.Children )
		{
			// Get the coordinates from the name in the form of "Chunk (x, y, z)"
			// THIS IS A DIRTY HACK, but it works for now.
			if ( child.Name.StartsWith( "Chunk (" ) )
			{
				var coords = child.Name.Substring( 7, child.Name.Length - 8 ).Split( ", " );
				if ( coords.Length == 3 && int.TryParse( coords[0], out int x ) && int.TryParse( coords[1], out int y ) && int.TryParse( coords[2], out int z ) )
				{
					child.WorldPosition = blockPosition + new Vector3( x * Chunk.SIZE.x, y * Chunk.SIZE.y, z * Chunk.SIZE.z ) * World.BlockScale;
				}
			}
		}
	}

	protected override void DrawGizmos()
	{
		if ( !Game.IsEditor ) return;

		//World.Active.LoadStructure( Helpers.WorldToVoxel(WorldPosition), LoadedStructure.StructureData );
		//Gizmo.Draw.SolidBox( LoadedStructure.StructureData );
	}
}
