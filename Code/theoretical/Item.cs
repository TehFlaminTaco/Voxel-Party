using System;

[GameResource( "Item Definition", "item", "an item", Category = "Voxel Party", Icon = "archive" )]
public partial class Item : GameResource
{
	[ReadOnly] public int ID { get; set; } = -1;
	public string Name { get; set; } // Name of the item, e.g. "Stick".
	public int MaxStackSize { get; set; } = 64; // Maximum stack size for this item, e.g. 64 for most items.

	[Property, ToggleGroup( "IsBlock" )]
	public bool IsBlock { get; set; }

	[Property, ToggleGroup( "IsBlock" ), InlineEditor]
	public Block Block { get; set; }

	public bool TryGiveID()
	{
		if ( ID == -1 )
		{
			var allitems = ResourceLibrary.GetAll<Item>();
			Log.Info( $"Generated new item ID: {allitems.Count()}/256 used!" );
			var usedIDs = allitems.Select( c => c.ID );
			for ( int i = 0; i < 255; i++ )
			{
				if ( !usedIDs.Contains( i ) )
				{
					ID = i;
					return true;
				}
			}
		}
		return false;
	}

	protected override void PostLoad()
	{
		base.PostLoad();
		TryGiveID();
	}

	public void Render( Transform transform )
	{
		// Draw a cube with the block's textures
		var block = ItemRegistry.GetBlock( ID );
		if ( block == null )
		{
			Log.Error( $"Block with ID {ID} not found." );
			return;
		}

		// Draw at -10, -10, -10 to 10 10 10
		var verts = new List<Vector3>();
		var normals = new List<Vector3>();
		var uvs = new List<Vector3>();
		var tangents = new List<Vector4>();
		// Add vertices for a cube
		foreach ( var dir in Directions.All )
		{
			block.AddFaceMesh( World.Active, Vector3Int.Zero, dir, verts, normals, uvs, tangents );
		}
		for ( int i = 0; i < verts.Count; i++ )
		{
			verts[i] *= 0.25f;
		}

		Vertex[] vertexes = new Vertex[verts.Count];
		for ( int i = 0; i < verts.Count; i++ )
		{
			var pos = verts[i];
			var normal = normals[i];
			var uv = uvs[i];
			var tangent = tangents[i];
			vertexes[i] = new(
				transform.PointToWorld( pos ),
				normal,
				tangent,
				new Vector4( uv, 0f )
			);
		}

		Graphics.Draw( vertexes, vertexes.Length, Material.Load( "materials/textureatlas.vmat" ) );
	}
}
