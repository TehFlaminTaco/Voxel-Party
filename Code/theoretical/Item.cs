using System;

[GameResource( "Item Definition", "item", "an item", Category = "Voxel Party", Icon = "archive" )]
public partial class Item : GameResource
{
	[ReadOnly] public int ID { get; set; } = -1;
	public string Name { get; set; } // Name of the item, e.g. "Stick".
	public int MaxStackSize { get; set; } = 64; // Maximum stack size for this item, e.g. 64 for most items.

	[Property] public bool InPallete { get; set; } = true;
	[Property, ToggleGroup( "IsBlock" )]
	public bool IsBlock { get; set; }

	[Property, ToggleGroup( "IsBlock" ), InlineEditor]
	public Block Block { get; set; }

	// Is this item spawnable by players?

	[Button]
	public void FixIDConflict()
	{
		if ( ResourceLibrary.GetAll<Item>().Count( c => c.ID == ID ) <= 1 )
		{
			Log.Warning( "No need to regenerate Item ID. Already unique!" );
			return;
		}
		ID = -1;
	}
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

	protected override void PostReload()
	{
		base.PostReload();
		TexArrayTool.Dirty = true;
		ItemRegistry.UpdateRegistry();
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
			ItemRegistry.UpdateRegistry();
			Log.Error( $"Block with ID {ID} not found." );
			return;
		}

		// Draw at -10, -10, -10 to 10 10 10
		var verts = new List<Vertex>();
		// Add vertices for a cube
		foreach ( var dir in Directions.All )
		{
			block.AddFaceMesh( World.Active, Vector3Int.Zero, dir, verts );
		}
		for ( int i = 0; i < verts.Count; i++ )
		{
			var v = verts[i];
			v.Position *= 0.25f;
			v.Position = transform.PointToWorld( v.Position );
			verts[i] = v;
		}

		Graphics.Draw( verts, verts.Count, Material.Load( "materials/textureatlas.vmat" ) );
	}
}
