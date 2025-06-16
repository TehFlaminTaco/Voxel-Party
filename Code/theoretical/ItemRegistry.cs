public static class ItemRegistry {
    public static Dictionary<short, Item> Items { get; } = new Dictionary<short, Item>();

    public static void UpdateRegistry() {
        Items.Clear();
        foreach ( var member in TypeLibrary.GetTypes().SelectMany( c => c.Members ).Where( c => c.Attributes.Any( a => a is RegisterItemAttribute ) ) ) {
            if ( !member.IsField && !member.IsProperty )
                throw new System.Exception( $"Member {member.TypeDescription.Name}.{member.Name} is not a field or property. (Got {member.GetType()})" );
            if ( member.IsStatic == false )
                throw new System.Exception( $"Member {member.TypeDescription.Name}.{member.Name} is not static." );

            object val;
            if ( member is FieldDescription fd ) {
                val = fd.GetValue( null );
            } else if ( member is PropertyDescription pd ) {
                val = pd.GetValue( null );
            } else {
                throw new System.Exception( $"Member {member.TypeDescription.Name}.{member.Name} is not a field or property. (Impossible?)" );
            }

            if ( val is not Item item )
                throw new System.Exception( $"Member {member.TypeDescription.Name}.{member.Name} is not an Item." );

            var attr = member.Attributes.OfType<RegisterItemAttribute>().FirstOrDefault();
            if ( attr == null )
                throw new System.Exception( $"Member {member.TypeDescription.Name}.{member.Name} does not have a RegisterItemAttribute. (Impossible?)" );

            if ( Items.ContainsKey( (short)attr.ItemID ) )
                throw new System.Exception( $"Item with ID {attr.ItemID} already registered to {Items[attr.ItemID].Name}" );

            Items.Add( (short)attr.ItemID, item );
            item.ID = (short)attr.ItemID; // Ensure the item has the correct ID set.
            Log.Info( $"Registered item {item.Name} with ID {attr.ItemID}" );
        }
    }

    public static Item GetItem( short itemID ) {
        if ( Items.TryGetValue( itemID, out var item ) )
            return item;
        throw new System.Exception( $"Item with ID {itemID} not found in registry." );
    }
}

public class RegisterItemAttribute : System.Attribute {
    public short ItemID { get; }

    public RegisterItemAttribute( short itemID ) {
        ItemID = itemID;
    }
}