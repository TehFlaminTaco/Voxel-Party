using System;

public static class TexArrayTool
{
    public static bool Dirty = true;
    public static void UpdateMaterialTexture( Material material )
    {
        if ( !Dirty && last != null && last.IsValid() )
        {
            material.Set( "Abledo", last );
            return;
        }

        //Log.Info( "Updating material texture with texture atlas." );
        material.Set( "Abledo", BuildTextureArray() );
        Dirty = false;
    }

    private static Texture last;

    public static Texture BuildTextureArray()
    {
        if ( last != null )
        {
            last.Dispose();
            last = null; // Clear the last texture to allow for garbage collection.
        }
        List<Texture> textures = new List<Texture>();

        int? TryAdd( Texture tex )
        {
            if ( tex == null )
            {
                return null;
            }
            if ( textures.Contains( tex ) )
            {
                return textures.IndexOf( tex );
            }
            textures.Add( tex );
            return textures.Count - 1; // Return the index of the newly added texture.
        }
        var items = ResourceLibrary.GetAll<Item>().Where( c => c.Block != null );
        if ( Game.IsEditor ) items = items.OrderBy( c => c.ID ); // Sort by ID in editor for consistency.
        foreach ( var item in items )
        {
            // For each block texture, add it to the list and set the index in the atlas.
            // If it already is in the atlas, skip it, but set the index.
            item.Block.TextureIndex = TryAdd( item.Block.Texture ) ?? 0;
            item.Block.TopTextureIndex = TryAdd( item.Block.TopTexture );
            item.Block.SideTextureIndex = TryAdd( item.Block.SideTexture );
            item.Block.NorthTextureIndex = TryAdd( item.Block.NorthTexture );
            item.Block.SouthTextureIndex = TryAdd( item.Block.SouthTexture );
            item.Block.EastTextureIndex = TryAdd( item.Block.EastTexture );
            item.Block.WestTextureIndex = TryAdd( item.Block.WestTexture );
            item.Block.BottomTextureIndex = TryAdd( item.Block.BottomTexture );
        }

        int tilesWide = 1;
        while ( Math.Pow( Math.Pow( 2, tilesWide ), 2 ) < textures.Count )
        {
            tilesWide++;
        }

        tilesWide = (int)Math.Pow( 2, tilesWide );

        int pixelsWide = 16 * tilesWide;

        last = Texture.Create( pixelsWide, pixelsWide )
            .WithFormat( ImageFormat.RGBA8888 )
            .WithName( "TextureAtlas" )
            .Finish();
        for ( int i = 0; i < textures.Count; i++ )
        {
            if ( textures[i] != null )
            {
                last.Update( textures[i].GetPixels().SelectMany( p => new[] { p.r, p.g, p.b, p.a } ).ToArray(), (i % tilesWide) * 16, (i / tilesWide) * 16, 16, 16 );
            }
        }
        last.MarkUsed();
        return last;
    }
}
