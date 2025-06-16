[GameResource("Item Definition", "item", "an item", Category = "Voxel Party", Icon = "archive")]
public partial class Item : GameResource {
    public int ID { get; set; }
    public string Name { get; set; } // Name of the item, e.g. "Stick".
    public int MaxStackSize { get; set; } = 64; // Maximum stack size for this item, e.g. 64 for most items.
    
    [Property, ToggleGroup("IsBlock")]
    public bool IsBlock { get; set; }
    [Property, ToggleGroup("IsBlock"), InlineEditor]
    public Block Block { get; set; }
    
    public void Render( Transform transform ) {
	    // Draw a cube with the block's textures
	    var block = ItemRegistry.GetBlock( ID );
	    if ( block == null ) {
		    Log.Error( $"Block with ID {ID} not found." );
		    return;
	    }

	    // Draw at -10, -10, -10 to 10 10 10
	    var verts = new List<Vector3>();
	    var normals = new List<Vector3>();
	    var uvs = new List<Vector2>();
	    // Add vertices for a cube
	    foreach ( var dir in Directions.All ) {
		    block.AddFaceMesh( World.Active, Vector3Int.Zero, dir, verts, normals, uvs );
	    }
	    for ( int i = 0; i < verts.Count; i++ ) {
		    verts[i] *= 0.25f;
	    }
	    var vertexes = verts.Zip( normals, uvs )
		    .Select( v => new Vertex( transform.PointToWorld( v.First ), v.Second, Vector3.Zero, v.Third ) )
		    .ToList();
	    Graphics.Draw( vertexes, vertexes.Count, Material.Load( "materials/textureatlas.vmat" ) );
    }
}
