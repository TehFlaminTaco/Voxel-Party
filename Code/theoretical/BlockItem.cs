
public class BlockItem : Item {
    public byte BlockID { get; set; } // The ID of the block this item represents, e.g. 0 for Air, 1 for Stone, etc.

    public override string Name {
        get {
            var block = BlockRegistry.GetBlock( BlockID );
            return block?.Name ?? "Unknown Block";
        }
        set { }
    }

    public override void Render( Transform transform ) {
        // Draw a cube with the block's textures
        var block = BlockRegistry.GetBlock( BlockID );
        if ( block == null ) {
            Log.Error( $"Block with ID {BlockID} not found." );
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