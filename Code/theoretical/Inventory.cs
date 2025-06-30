using System;

public class Inventory
{
    public List<ItemStack> Items { get; private set; } = new();
    public int InventorySize { get; set; }
    public int HotbarSize { get; set; }
    public int TotalSize => HotbarSize + InventorySize + 1;
    public int CursorSlot => InventorySize + HotbarSize;

    public Inventory( int inventorySize = 27, int hotbarSize = 9 )
    {
        InventorySize = inventorySize;
        HotbarSize = hotbarSize;
        for ( int i = 0; i < TotalSize; i++ )
        {
            Items.Add( ItemStack.Empty );
        }
    }

    public ItemStack GetItem( int slot )
    {
        if ( slot < 0 || slot >= TotalSize )
        {
            throw new ArgumentOutOfRangeException( nameof( slot ), "Slot index is out of range." );
        }
        return Items[slot] ?? ItemStack.Empty; // Return empty stack if slot is null
    }

    public void SetItem( int slot, ItemStack stack )
    {
        if ( slot < 0 || slot >= TotalSize )
        {
            throw new ArgumentOutOfRangeException( nameof( slot ), "Slot index is out of range." );
        }
        Items[slot] = stack ?? ItemStack.Empty; // Set to empty stack if null
    }

    public void Clear()
    {
        for ( int i = 0; i < TotalSize; i++ )
        {
            Items[i] = ItemStack.Empty; // Clear all slots
        }
    }

    public IEnumerable<byte> Serialize()
    {
        var serialized = new List<byte>();
        for ( int slot = 0; slot < TotalSize; slot++ )
        {
            var item = GetItem( slot );
            serialized.AddRange( item.Serialize() );
        }
        return serialized;
    }

    public void Deserialize( IEnumerable<byte> data )
    {
        int index = 0;
        var dataList = data.ToList();
        for ( int slot = 0; slot < TotalSize; slot++ )
        {
            if ( index + 8 > dataList.Count )
            {
                Log.Warning( "Inventory.Deserialize: Not enough data to deserialize ItemStack." );
                break; // Not enough data for the next item
            }
            var item = ItemStack.Deserialize( dataList.Skip( index ), out int size );
            SetItem( slot, item );
            index += size; // Move to the next item in the data
        }
    }

    // Try and insert the item into the inventory at the given slot and return all items we cannot insert.
    // If the slot is full, we return the original stack.
    // If simulate is true, we do not modify the inventory nor the original stack.
    // If simulate is false, we modify the inventory and may modify the original stack.
    public ItemStack PutInSlot( int slot, ItemStack stack, bool simulate )
    {
        if ( slot < 0 || slot >= TotalSize )
        {
            throw new ArgumentOutOfRangeException( nameof( slot ), "Slot index is out of range." );
        }

        if ( ItemStack.IsNullOrEmpty( stack ) )
        {
            return stack; // Nothing to insert
        }

        var currentStack = GetItem( slot );

        if ( ItemStack.IsNullOrEmpty( currentStack ) )
        {
            // If the slot is empty, just set the stack
            if ( !simulate )
            {
                SetItem( slot, stack );
            }
            return ItemStack.Empty; // Always works
        }

        if ( currentStack.CanMerge( stack ) )
        {
            var mergedStack = currentStack.Merge( stack, simulate );
            return ItemStack.IsNullOrEmpty( mergedStack ) ? ItemStack.Empty : mergedStack;
        }

        return stack; // Cannot merge, return the original stack
    }

    // Try to insert the item into the first available slot in the inventory.
    public ItemStack PutInFirstAvailableSlot( ItemStack stack, bool hotbarFirst = true, bool simulate = false )
    {
        if ( ItemStack.IsNullOrEmpty( stack ) )
        {
            return stack; // Nothing to insert
        }

        // Check if this itemstack exists in the inventory already, and try to merge it onto it.
        for ( int slot = 0; slot < TotalSize; slot++ )
        {
            int id = slot;
            if ( hotbarFirst )
            {
                slot += InventorySize;
                slot = slot % (InventorySize + HotbarSize);
            }

            if ( GetItem( id ).Item == stack.Item )
            {
                if ( GetItem( id ).Count < 0 )
                    return ItemStack.Empty;
                stack = PutInSlot( id, stack, false );
                if ( ItemStack.IsNullOrEmpty( stack ) )
                    return stack;
            }
        }

        for ( int i = 0; i < InventorySize + HotbarSize; i++ )
        {
            int id = i;
            if ( hotbarFirst )
            {
                i += InventorySize;
                i = i % (InventorySize + HotbarSize);
            }

            var result = PutInSlot( id, stack, simulate );
            if ( ItemStack.IsNullOrEmpty( result ) )
            {
                return result; // Successfully inserted
            }
            stack = result; // Update stack to remaining items
        }

        return stack; // If we reach here, the stack could not be fully inserted
    }

    public ItemStack TakeItem( int slot, int count )
    {
        if ( slot < 0 || slot >= TotalSize )
        {
            throw new ArgumentOutOfRangeException( nameof( slot ), "Slot index is out of range." );
        }

        var currentStack = GetItem( slot );
        if ( ItemStack.IsNullOrEmpty( currentStack ) || currentStack.Count < count )
        {
            return ItemStack.Empty; // Not enough items to take
        }

        var takenStack = currentStack.Clone();
        takenStack.Count = Math.Min( count, takenStack.Count );
        currentStack.Count -= takenStack.Count;
        if ( currentStack.Count <= 0 )
        {
            SetItem( slot, ItemStack.Empty ); // Clear the slot if empty
        }
        else
        {
            SetItem( slot, currentStack ); // Update the slot with the remaining items
        }
        return takenStack; // Return the stack of items taken
    }

    [ConCmd]
    [Rpc.Host]
    public static void GiveItem( int itemID, int amount )
    {
        var player = VoxelPlayer.LocalPlayer;
        if ( !player.IsValid() )
        {
            Log.Warning( "Player not found" );
            return;
        }

        var stack = new ItemStack( ItemRegistry.GetItem( itemID ), amount );
        player.inventory.PutInFirstAvailableSlot( stack );
    }

    [ConCmd]
    public static void GiveItem( string itemName, int amount )
    {
        var player = Game.ActiveScene?.GetAllComponents<VoxelPlayer>()
            .FirstOrDefault( x => x.Network.Owner == Rpc.Caller );
        if ( !player.IsValid() )
        {
            Log.Warning( "Player not found" );
            return;
        }

        var stack = new ItemStack( ItemRegistry.GetItem( itemName ) );
        player.inventory.PutInFirstAvailableSlot( stack );
    }
}
