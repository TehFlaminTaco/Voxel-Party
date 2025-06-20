using System;

public class Inventory
{
    public List<ItemStack> Items { get; private set; } = new();
    public int InventorySize { get; set; }
    public int HotbarSize { get; set; }
    public int TotalSize => HotbarSize + InventorySize;

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

        if ( hotbarFirst )
        {
	        for ( int i = InventorySize; i < TotalSize; i++ )
	        {
		        var result = PutInSlot( i, stack, simulate );
		        if ( ItemStack.IsNullOrEmpty( result ) )
		        {
			        return result; // Successfully inserted
		        }
		        stack = result; // Update stack to remaining items
	        }
        }
        else
        {
	        for ( int i = 0; i < TotalSize; i++ )
	        {
		        var result = PutInSlot( i, stack, simulate );
		        if ( ItemStack.IsNullOrEmpty( result ) )
		        {
			        return result; // Successfully inserted
		        }
		        stack = result; // Update stack to remaining items
	        }
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
    public static void GiveItem( int itemID, int amount )
    {
	    var player = VoxelPlayer.LocalPlayer;
        if ( !player.IsValid() )
        {
            Log.Warning( "Player not found" );
            return;
        }

        var stack = new ItemStack( ItemRegistry.GetItem( itemID ) );
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
