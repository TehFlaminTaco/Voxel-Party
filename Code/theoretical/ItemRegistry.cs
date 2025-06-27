public static class ItemRegistry
{

	public static bool FinishedLoading = false;

	public static Dictionary<int, Item> CachedRegistry = new();
	public static void UpdateRegistry()
	{
		var newRegistry = new Dictionary<int, Item>();
		foreach ( var item in ResourceLibrary.GetAll<Item>() )
		{
			if ( newRegistry.ContainsKey( item.ID ) )
				Log.Warning( $"Duplicate registry entry: {item.ID} = {item.ResourcePath} vs {newRegistry[item.ID].ResourcePath}" );
			else
				newRegistry[item.ID] = item;
		}
		CachedRegistry = newRegistry;
	}

	public static Item GetItem( int ID )
	{
		if ( CachedRegistry.TryGetValue( ID, out Item i ) )
			return i;
		return null;
	}

	public static Item GetItem( string name )
	{
		var item = ResourceLibrary.GetAll<Item>().FirstOrDefault( x => x.Name.Equals( name, System.StringComparison.CurrentCultureIgnoreCase ) );
		if ( item is not null && item.IsValid() )
			return item;
		return null;
	}

	public static Item GetItem( Vector3Int position )
	{
		var item = GetItem( World.Active.GetBlock( position ).BlockID );
		if ( item is not null && item.IsValid() )
			return item;
		return null;
	}

	public static Block GetBlock( int itemID )
	{
		if ( CachedRegistry.TryGetValue( itemID, out Item i ) )
			return i.Block;
		return null;
	}

	public static Block GetBlock( string name )
	{
		var item = ResourceLibrary.GetAll<Item>().FirstOrDefault( x => x.Name.Equals( name, System.StringComparison.CurrentCultureIgnoreCase ) );
		if ( item != null && item.IsValid() )
			return item.Block;
		return null;
	}
}

public class RegisterItemAttribute : System.Attribute
{
	public int ItemID { get; }

	public RegisterItemAttribute( int itemID )
	{
		ItemID = itemID;
	}
}
