using System;

public class Inventory
{
    public List<ItemStack> Items { get; private set; } = new();
    public int MaxSize { get; set; }

    public Inventory( int maxSize = 36 )
    {
        MaxSize = maxSize;
        for ( int i = 0; i < MaxSize; i++ )
        {
            Items.Add( ItemStack.Empty );
        }
    }

    public ItemStack GetItem( int slot )
    {
        if ( slot < 0 || slot >= MaxSize )
        {
            throw new ArgumentOutOfRangeException( nameof( slot ), "Slot index is out of range." );
        }
        return Items[slot] ?? ItemStack.Empty; // Return empty stack if slot is null
    }

    public void SetItem( int slot, ItemStack stack )
    {
        if ( slot < 0 || slot >= MaxSize )
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
        if ( slot < 0 || slot >= MaxSize )
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
            if ( !simulate )
            {
                SetItem( slot, mergedStack );
            }
            return ItemStack.IsNullOrEmpty( mergedStack ) ? ItemStack.Empty : mergedStack;
        }

        return stack; // Cannot merge, return the original stack
    }

    // Try to insert the item into the first available slot in the inventory.
    public ItemStack PutInFirstAvailableSlot( ItemStack stack, bool simulate = false )
    {
        if ( ItemStack.IsNullOrEmpty( stack ) )
        {
            return stack; // Nothing to insert
        }

        for ( int i = 0; i < MaxSize; i++ )
        {
            var result = PutInSlot( i, stack, simulate );
            if ( ItemStack.IsNullOrEmpty( result ) || result == stack )
            {
                return result; // Successfully inserted or no change
            }
            stack = result; // Update stack to remaining items
        }

        return stack; // If we reach here, the stack could not be fully inserted
    }

    [ConCmd]
    public static void GiveItem( int itemID, int amount )
    {
        var player = Game.ActiveScene?.GetAllComponents<VoxelPlayer>()
            .FirstOrDefault( x => x.Network.Owner == Rpc.Caller );
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
