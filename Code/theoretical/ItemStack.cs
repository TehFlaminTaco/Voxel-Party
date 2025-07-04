// Defines a stack of items as well as their data
using System;
using Sandbox;

public class ItemStack
{
    public static readonly ItemStack Empty = new ItemStack { ItemID = 0, Count = 0 };

    public Item Item { get; set; } // The ID of the item in the stack, e.g. 0x01 for Stone.
    public int Count { get; set; } // The number of items in the stack

    [Hide]
    public int ItemID
    {
        get
        {
            return Item?.ID ?? 0; // Return the item ID if the item is not null, otherwise return 0
        }
        set
        {
            Item = ItemRegistry.GetItem( value ); // Get the item from the registry using the provided ID
        }
    }

    public IEnumerable<byte> Serialize()
    {
        var serialized = new List<byte>();
        serialized.AddRange( BitConverter.GetBytes( ItemID ) ); // Serialize the item ID
        serialized.AddRange( BitConverter.GetBytes( Count ) ); // Serialize the count
        return serialized; // Return the serialized data as a byte array
    }

    public static ItemStack Deserialize( IEnumerable<byte> data, out int size )
    {
        var dataList = data.ToList(); // Convert the IEnumerable to a List for easier access
        if ( dataList.Count < 8 )
        {
            Log.Warning( "ItemStack.Deserialize: Not enough data to deserialize ItemStack." );
            size = 0; // Set size to 0 if there is not enough data
            return Empty; // Return an empty stack if there is not enough data
        }

        int itemID = BitConverter.ToInt32( dataList.Take( 4 ).ToArray(), 0 ); // Deserialize the item ID
        int count = BitConverter.ToInt32( dataList.Skip( 4 ).Take( 4 ).ToArray(), 0 ); // Deserialize the count
        size = 8; // Set the size to the number of bytes read (4 for item ID + 4 for count)
        var item = ItemRegistry.GetItem( itemID ); // Get the item from the registry using the deserialized ID
        if ( item == null )
        {
            Log.Warning( $"ItemStack.Deserialize: Item with ID {itemID} not found in registry." );
            return Empty; // Return an empty stack if the item is not found
        }
        // This might change in future if we add more properties to ItemStack, or if ItemStack has variable size data.
        return new ItemStack( item, count ); // Return a new ItemStack with the deserialized data
    }

    public static bool IsNullOrEmpty( ItemStack stack )
    {
        return stack == null || stack.ItemID == 0 || stack.Count == 0;
    }

    // Spawn an instance of this item in the world at the specified position
    public WorldItem Spawn( Vector3 position )
    {
        var go = new GameObject();
        go.WorldPosition = position;
        go.WorldRotation = Rotation.Random;
        var wi = go.AddComponent<WorldItem>();
        wi.stack = this; // Set the stack data
        return wi; // Return the WorldItem component
    }

    public ItemStack()
    {
        ItemID = 0; // Default item ID
        Count = 0; // Default count
    }

    public ItemStack( Item item, int count = 1 )
    {
        if ( item == null )
        {
            Item = null;
            Count = 0;
        }
        else
        {
            Item = item; // Set the item
            Count = count; // Set the count
        }
    }

    public ItemStack Clone()
    {
        return new ItemStack
        {
            Item = this.Item,
            Count = this.Count
        }; // Create a new ItemStack with the same data
    }

    // Can we accept the other stack into this one?
    public bool CanMerge( ItemStack other )
    {
        if ( IsNullOrEmpty( other ) )
            return false; // Cannot merge with an empty stack
        if ( IsNullOrEmpty( this ) )
            return false; // Cannot merge with an empty stack
        if ( this.ItemID != other.ItemID )
            return false; // Cannot merge stacks with different item IDs
        if ( this.Count >= Item.MaxStackSize )
            return false; // Cannot merge if this stack is already at max capacity
        return true;
    }

    public ItemStack Merge( ItemStack other, bool simulate = false )
    {
        if ( !CanMerge( other ) )
            return other; // Do not mutate the original stack, return the other stack if they cannot be merged
        int newValue = Math.Min( Count + other.Count, Item.MaxStackSize );
        int delta = newValue - Count; // Calculate the difference to determine how many items to add
        if ( !simulate )
        {
            Count = newValue; // Update the count of this stack
        }
        else
        {
            other = other.Clone(); // If simulating, clone the other stack to avoid modifying it
        }
        other.Count -= delta; // Decrease the count of the other stack by the delta
        if ( other.Count <= 0 )
        {
            return ItemStack.Empty; // If the other stack is empty after merging, return an empty stack
        }
        return other; // Return the other stack, which now has a reduced count
    }

}
