public class Item {
    public short ID { get; set; }
    public virtual string Name { get; set; } // Name of the item, e.g. "Stick".

    public virtual short MaxStackSize { get; set; } = 64; // Maximum stack size for this item, e.g. 64 for most items.

    public virtual void Render( Transform transform ) {

    }
}