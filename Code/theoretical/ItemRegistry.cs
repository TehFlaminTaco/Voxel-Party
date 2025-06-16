public static class ItemRegistry
{
	public static void UpdateRegistry()
	{

	}

	public static Item GetItem( int ID )
	{
		var item = ResourceLibrary.GetAll<Item>().FirstOrDefault( x => x.ID == ID );
		if ( item != null && item.IsValid() )
			return item;
		return null;
	}

	public static Item GetItem( string name )
	{
		var item = ResourceLibrary.GetAll<Item>().FirstOrDefault( x => x.Name == name );
		if ( item is not null && item.IsValid() )
			return item;
		return null;
	}

	public static Block GetBlock( int itemID )
	{
		var item = ResourceLibrary.GetAll<Item>().FirstOrDefault( x => x.ID == itemID );
		if ( item != null && item.IsValid() )
			return item.Block;
		return null;
	}

	public static Block GetBlock( string name )
	{
		var item = ResourceLibrary.GetAll<Item>().FirstOrDefault( x => x.Name == name );
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
