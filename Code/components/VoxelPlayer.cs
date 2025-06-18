using System;

public class VoxelPlayer : Component
{
    World world => Scene.GetAll<WorldThinker>().First().World;

    public Inventory inventory = new();
    [Property, ReadOnly, Group("InventoryDebug")] public List<int> InventoryItems => inventory.Items.ConvertAll( item => item.Count );

    [Property] public bool CreativeMode { get; set; } = false;
    [Property] public bool GiveBrokenBlocks { get; set; } = false;
    
    [Property] public bool HasInventory { get; set; } = true;
    [Property] public bool HasHotbar { get; set; } = true;

    [Property, Alias( "Reach Distance" ), Description( "In blocks, not inches" )]
    public float ReachDistanceProperty { get; set; } = 3.5f;
    public float ReachDistance => ReachDistanceProperty * World.BlockScale;

    public static VoxelPlayer LocalPlayer { get; set; }
    
    public int BreakingProgress = 0;
    public TimeSince BreakTime = 0;
    public TimeSince TimeSinceLastBreak = 0;
    public Vector3Int? BreakingBlock;
    public Vector3Int? LastBreakingBlock;
    public Direction BreakingFace = Direction.None;
    public Direction LastBreakingFace = Direction.None;
    SceneCustomObject blockBreakEffect;

    protected override void OnStart()
    {
	    LocalPlayer = Scene.GetAllComponents<VoxelPlayer>().FirstOrDefault( x => x.Network.Owner == Connection.Local );
	    
        SpawnBlockBreakingEffect();
    }

    protected override void OnFixedUpdate()
    {
        if ( IsProxy )
            return;
        
        HandleBreak();
        HandlePlace();
        HandleHotbar();
    }

    protected override void OnPreRender()
    {
	    ShowHoveredFace();
    }

    public BlockTraceResult EyeTrace()
    {
        var pc = GetComponent<PlayerController>();
        return Scene.GetAll<WorldThinker>().First().World.Trace(
            pc.EyePosition,
            pc.EyePosition + pc.EyeAngles.Forward * ReachDistance
        ).Run();
    }

    public static int SelectedSlot = 0;
    public void SpawnBlockBreakingEffect()
    {
        blockBreakEffect = new SceneCustomObject( Scene.SceneWorld );
        blockBreakEffect.Flags.CastShadows = false;
        blockBreakEffect.Flags.IsOpaque = false;
        blockBreakEffect.Flags.IsTranslucent = true;
        blockBreakEffect.RenderOverride = ( obj ) =>
        {
            if ( BreakingBlock == null )
                return;
            var block = world.GetBlock( BreakingBlock.Value ).GetBlock();
            var pos = (BreakingBlock.Value + Vector3.One * 0.5f) * World.BlockScale;
            pos += BreakingFace.Forward() * World.BlockScale * 0.501f; // Offset slightly to avoid z-fighting
            pos -= obj.Position;
            var scale = World.BlockScale; // Scale the effect to half the block size
            var right = BreakingFace.Right();
            var up = BreakingFace.Up();
            var p1 = pos - right * scale / 2f - up * scale / 2f;
            var p2 = pos + right * scale / 2f - up * scale / 2f;
            var p3 = p1 + up * scale;
            var p4 = p2 + up * scale;
            int progress = Math.Min( (int)(BreakTime * 20 / block.Hardness), 9 ); // TODO: based on BreakTime
            Vector2Int textureIndex = new Vector2Int( 6 + progress, 15 );
            var rect = Rect.FromPoints( textureIndex / 16f + Vector2.One / 160f, textureIndex / 16f + Vector2.One / 16f - Vector2.One / 160f ); // Assuming a texture atlas of 16x16, each tile is 0.0625 in UV space
            var v1 = new Vertex( p1, rect.BottomLeft, Color.White );
            var v2 = new Vertex( p2, rect.BottomRight, Color.White );
            var v3 = new Vertex( p3, rect.TopLeft, Color.White );
            var v4 = new Vertex( p4, rect.TopRight, Color.White );
            blockBreakEffect.ColorTint = Color.White.WithAlpha( 1f );
            Graphics.Draw( new List<Vertex> { v1, v2, v3, v4 }, 4, Material.Load( "materials/textureatlastranslucent.vmat" ), new RenderAttributes(), Graphics.PrimitiveType.TriangleStrip );
        };
    }
    public void HandleBreak()
    {
	    var trace = EyeTrace();
	    if ( CreativeMode && TimeSinceLastBreak.Relative < 0.2f )
	    {
		    return;
	    }
	    if ( !trace.Hit || !Input.Down( "Attack1" ) )
	    {
		    BreakingBlock = null;
		    LastBreakingBlock = null;
		    BreakingFace = Direction.None;
		    LastBreakingFace = Direction.None;
		    BreakTime = 0f;
		    
		    return;
	    }

	    BreakingBlock = trace.HitBlockPosition;
	    //TODO: fix this shit
	    // if ( LastBreakingBlock != BreakingBlock || LastBreakingFace != BreakingFace )
	    // {
		   //  BreakingBlock = null;
		   //  BreakingFace = trace.HitFace;
		   //  BreakTime = 0f;
		   //  
		   //  return;
	    // }
     
        if ( !CreativeMode )
        {
	        blockBreakEffect.Transform = global::Transform.Zero.WithPosition( WorldPosition );
	        var block = world.GetBlock( BreakingBlock.Value ).GetBlock();
	        BreakingProgress = Math.Min( (int)(BreakTime * 20 / block.Hardness), 10 );
        }
        
        if ( (BreakingProgress == 10 || CreativeMode) && BreakingBlock.HasValue )
        {
	        if ( !GiveBrokenBlocks ) world.Thinker.BreakBlock( BreakingBlock.Value );
	        else
	        {
		        
		        var i = inventory.PutInFirstAvailableSlot( new ItemStack( ItemRegistry.GetItem( BreakingBlock.Value ) ) );
		        world.SetBlock( BreakingBlock.Value, new BlockData( 0 ) );
	        }
        }

        LastBreakingBlock = BreakingBlock;
        LastBreakingFace = BreakingFace;
        TimeSinceLastBreak = 0;
    }

    public void HandlePlace()
    {
        if ( Input.Pressed( "Attack2" ) )
        {
            var item = inventory.GetItem( SelectedSlot );
            if ( ItemStack.IsNullOrEmpty( item ) )
                return;
            if ( item.Item.Block == null )
            {
                return; // TODO: Other item use actions, other placement styles?
            }
            var trace = EyeTrace();
            if ( !trace.Hit )
                return;
            var placePos = trace.HitBlockPosition + trace.HitFace.Forward();
            inventory.TakeItem( SelectedSlot, 1 ); // Remove one item from the hotbar slot
            world.SetBlock( placePos, new BlockData( (byte)item.Item.ID, 0 ) );
        }
    }

    public void ShowHoveredFace()
    {
        var trace = EyeTrace();
        if ( !trace.Hit )
            return;

        // Draw a box on the face hit
        Gizmo.Draw.Color = Color.Black;
        var facePos = (trace.HitBlockPosition + 0.5f) * World.BlockScale + trace.HitFace.Forward() * World.BlockScale * 0.51f;
        Vector3 boxSize = new Vector3( World.BlockScale, World.BlockScale, World.BlockScale );
        switch ( trace.HitFace )
        {
            case Direction.North:
            case Direction.South:
                boxSize = new Vector3( World.BlockScale * 0f, World.BlockScale, World.BlockScale );
                break;
            case Direction.East:
            case Direction.West:
                boxSize = new Vector3( World.BlockScale, World.BlockScale * 0f, World.BlockScale );
                break;
            case Direction.Up:
            case Direction.Down:
                boxSize = new Vector3( World.BlockScale, World.BlockScale, World.BlockScale * 0f );
                break;
        }
        Gizmo.Draw.Color = Color.Black;
        Gizmo.Draw.LineThickness = 2f;
        Gizmo.Draw.LineBBox( BBox.FromPositionAndSize( facePos, boxSize ) );
    }

    public void HandleHotbar()
    {
        for ( int i = inventory.InventorySize; i < inventory.TotalSize; i++ )
        {
            if ( Input.Pressed( $"Slot{i + 1}" ) )
                SelectedSlot = i;
        }

        SelectedSlot += Input.MouseWheel.y.FloorToInt().Clamp( -1, 1 );
        SelectedSlot = SelectedSlot.Clamp( 0, inventory.HotbarSize - 1 );
    }
}
