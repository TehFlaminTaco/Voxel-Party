public static class BlockRegistry
{
    public static Dictionary<byte, Block> Blocks { get; } = new Dictionary<byte, Block>();

    public static void UpdateRegistry()
    {
        Blocks.Clear();
        foreach (var member in TypeLibrary.GetTypes().SelectMany(c => c.Members).Where(c => c.Attributes.Any(a => a is RegisterBlockAttribute)))
        {
            if (!member.IsField && !member.IsProperty)
                throw new System.Exception($"Member {member.TypeDescription.Name}.{member.Name} is not a field or property. (Got {member.GetType()})");
            if (member.IsStatic == false)
                throw new System.Exception($"Member {member.TypeDescription.Name}.{member.Name} is not static.");

            object val;
            if (member is FieldDescription fd)
            {
                val = fd.GetValue(null);
            }
            else if (member is PropertyDescription pd)
            {
                val = pd.GetValue(null);
            }
            else
            {
                throw new System.Exception($"Member {member.TypeDescription.Name}.{member.Name} is not a field or property. (Impossible?)");
            }

            if (val is not Block block)
                throw new System.Exception($"Member {member.TypeDescription.Name}.{member.Name} is not a Block.");

            var attr = member.Attributes.OfType<RegisterBlockAttribute>().FirstOrDefault();
            if (attr == null)
                throw new System.Exception($"Member {member.TypeDescription.Name}.{member.Name} does not have a RegisterBlockAttribute. (Impossible?)");

            if (Blocks.ContainsKey((byte)attr.BlockID))
                throw new System.Exception($"Block with ID {attr.BlockID} already registered to {Blocks[attr.BlockID].Name}");

            Blocks.Add((byte)attr.BlockID, block);
            block.BlockID = (byte)attr.BlockID; // Ensure the block has the correct ID set.
            Log.Info($"Registered block {block.Name} with ID {attr.BlockID}");

        }
    }

    public static Block GetBlock(byte blockID)
    {
        if (Blocks.TryGetValue(blockID, out var block))
            return block;
        throw new System.Exception($"Block with ID {blockID} not found in registry.");
    }
}

public class RegisterBlockAttribute : System.Attribute
{
    public byte BlockID { get; }
    public RegisterBlockAttribute(byte blockID)
    {
        BlockID = blockID;
    }
}