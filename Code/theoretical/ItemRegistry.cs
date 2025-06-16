public static class ItemRegistry {
    public static List<Item> Items { get; } = new();

    public static void UpdateRegistry() {
	    var items = ResourceLibrary.GetAll<Item>()
		    .OrderBy( x => x.ID );

	    foreach ( var i in items )
	    {
		    Items.Add( i );
		    Log.Info( $"Registered item {i.Name} with ID {i.ID}" );
	    }
    }

    public static Item GetItem( int ID ) {
	    var item = Items.FirstOrDefault( x => x.ID == ID );
	    if ( item.IsValid() )
		    return item;
        throw new System.Exception( $"Item with ID {ID} not found in registry." );
    }
    
    public static Item GetItem( string name ) {
	    var item = Items.FirstOrDefault( x => x.Name == name );
	    if ( item.IsValid() )
		    return item;
	    throw new System.Exception( $"Item with name {name} not found in registry." );
    }

    public static Block GetBlock( int itemID )
    {
	    var item = Items.FirstOrDefault( x => x.ID == itemID );
	    if ( item.IsValid() )
		    return item.Block;
	    throw new System.Exception( $"Item with ID {itemID} not found in registry." );
    }
    
    public static Block GetBlock( string name )
    {
	    var item = Items.FirstOrDefault( x => x.Name == name );
	    if ( item.IsValid() )
		    return item.Block;
	    throw new System.Exception( $"Item with ID {name} not found in registry." );
    }
}

public class RegisterItemAttribute : System.Attribute {
    public int ItemID { get; }

    public RegisterItemAttribute( int itemID ) {
        ItemID = itemID;
    }
}
