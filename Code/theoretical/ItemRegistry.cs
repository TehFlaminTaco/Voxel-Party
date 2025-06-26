public static class ItemRegistry
{

	public static bool FinishedLoading = false;

	public static Dictionary<int, Item> CachedRegistry = new();
	public static void UpdateRegistry()
	{
		CachedRegistry.Clear();
		foreach ( var item in ResourceLibrary.GetAll<Item>() )
		{
			if ( CachedRegistry.ContainsKey( item.ID ) )
				Log.Warning( $"Duplicate registry entry: {item.ID} = {item.ResourcePath} vs {CachedRegistry[item.ID].ResourcePath}" );
			else
				CachedRegistry[item.ID] = item;
		}
	}

	public static Item GetItem( int ID )
	{
		if ( CachedRegistry.ContainsKey( ID ) )
			return CachedRegistry[ID];
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
		if ( CachedRegistry.ContainsKey( itemID ) )
			return CachedRegistry[itemID].Block;
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
