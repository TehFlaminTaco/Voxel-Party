using System;
using System.Security.Cryptography.X509Certificates;

public partial class VoxelPlayer : Component
{
    World world => Scene.GetAll<WorldThinker>().First().World;

    [Sync( SyncFlags.FromHost )]
    public byte[] InventoryData
    {
        get => inventory.Serialize().ToArray();
        set
        {
            if ( value == null || value.Length == 0 )
                return; // Do not deserialize if the value is null or empty
            inventory.Deserialize( value );
        }
    }

    public Inventory inventory = new();
    [Property, ReadOnly, Group( "InventoryDebug" )] public List<int> InventoryItems => inventory.Items.ConvertAll( item => item.Count );

    [Property] public bool CreativeMode { get; set; } = false;
    [Property] public bool GiveBrokenBlocks { get; set; } = false;
    [Property] public bool HasInventory { get; set; } = true;
    [Property] public bool HasHotbar { get; set; } = true;

    [Property, Alias( "Reach Distance" ), Description( "In blocks, not inches" )]
    public float ReachDistanceProperty { get; set; } = 3.5f;
    public float ReachDistance => ReachDistanceProperty * World.BlockScale;

    [RequireComponent] PlayerController Controller { get; set; }

    public static VoxelPlayer LocalPlayer { get; set; }

    public int BreakingProgress = 0;
    public TimeSince BreakTime = 0;
    public TimeSince TimeSinceLastBreak = 0;
    public Vector3Int? BreakingBlock;
    public Vector3Int? LastBreakingBlock;
    public Direction BreakingFace = Direction.None;
    public Direction LastBreakingFace = Direction.None;
    public Item GroundBlockL = null;
    public Item GroundBlockR = null;
    public bool IsFlying;
    SceneCustomObject blockBreakEffect;

    // Gamemode stuff
    [Sync( SyncFlags.FromHost )] public bool HasBuildVolume { get; set; } = false;
    [Sync( SyncFlags.FromHost )] public Vector3Int BuildAreaMins { get; set; } = Vector3Int.Zero;
    [Sync( SyncFlags.FromHost )] public Vector3Int BuildAreaMaxs { get; set; } = Vector3Int.Zero;
    [Sync( SyncFlags.FromHost )] public bool CanBuild { get; set; } = false;

    [Sync( SyncFlags.FromHost )] public int TotalBlockArea { get; set; } = 0; // Total area of the blocks in the build area, used for gamemode scoring
    [Sync( SyncFlags.FromHost )] public int CorrectBlocksPlaced { get; set; } = 0; // Number of blocks placed correctly by the player, used for gamemode scoring
    [Sync( SyncFlags.FromHost )] public int IncorrectBlocksPlaced { get; set; } = 0; // Number of blocks placed incorrectly by the player, used for gamemode scoring

    [Sync] public bool IsReady { get; set; } = false; // Whether the player is ready for the gamemode to start

    protected override void OnStart()
    {
        LocalPlayer = Scene.GetAllComponents<VoxelPlayer>().FirstOrDefault( x => x.Network.Owner == Connection.Local );

        SpawnBlockBreakingEffect();
    }

    public TimeSince TimeSinceLastJump { get; set; } = 0;
    protected override void OnUpdate()
    {
        if ( Input.Pressed( "use" ) ) IsReady = !IsReady;
        
        if ( Input.Pressed( "jump" ) && TimeSinceLastJump.Relative < .25 && !Controller.IsOnGround ) IsFlying = !IsFlying;
        if ( Input.Pressed( "jump" ) ) TimeSinceLastJump = 0;
        if ( Controller.IsOnGround ) IsFlying = false;
    }

    protected override void OnFixedUpdate()
    {
        if ( IsProxy )
            return;

        if ( CanBuild )
        {
            HandleBreak();
            HandlePlace();
        }
        HandleHotbar();
        DoFootsteps();
    }

    protected override void OnPreRender()
    {
	    ShowHoveredFace();
	    
	    if ( !IsProxy && HasBuildVolume )
	    {
		    Gizmo.Draw.Color = Color.Green.WithAlpha( 0.5f );
		    Gizmo.Draw.LineThickness = 8f;
		    var bbox = BBox.FromPoints( new[]{
			    BuildAreaMins * World.BlockScale,
			    (BuildAreaMaxs + Vector3.One) * World.BlockScale
		    } );
		    Gizmo.Draw.LineBBox( bbox );
	    }
    }

    public BlockTraceResult EyeTrace()
    {
        var pc = GetComponent<PlayerController>();

        var trace = Scene.GetAll<WorldThinker>().First().World.Trace(
            pc.EyePosition,
            pc.EyePosition + pc.EyeAngles.Forward * ReachDistance
        ).Run();

        if ( HasBuildVolume )
        {
            var ray = new Ray(
                pc.EyePosition,
                pc.EyeAngles.Forward
            );

            var walls = new (float distance, Direction face)[]{
                (BuildAreaMins.x, Direction.South),
                (BuildAreaMaxs.x+1f, Direction.North),
                (BuildAreaMins.y, Direction.East),
                (BuildAreaMaxs.y+1f, Direction.West),
                (BuildAreaMins.z, Direction.Down),
                (BuildAreaMaxs.z+1f, Direction.Up)
                // This big ugly function turns the plane distance for each bound into a casted point. if it ever does actually hit.
            }.Where( w => w.face.Forward().Dot( ray.Forward ) > 0 ) // Only consider faces that are facing the ray direction
                .Select( distanceface => (new Plane( distanceface.face.Forward().Abs(), distanceface.distance * World.BlockScale ).Trace( ray, true, ReachDistance * World.BlockScale ), distanceface.face) )
                .Where( x => x.Item1.HasValue )
                .Select( p => (p.Item1.Value, p.Item2, p.Item1.Value.Distance( ray.Position )) )
                .Where( wall =>
                {
                    // Check that this point is within the bounds of the build area
                    var pos = (wall.Item1 / World.BlockScale).Floor() - wall.Item2.Forward();
                    return pos.x >= BuildAreaMins.x && pos.x <= BuildAreaMaxs.x &&
                           pos.y >= BuildAreaMins.y && pos.y <= BuildAreaMaxs.y &&
                           pos.z >= BuildAreaMins.z && pos.z <= BuildAreaMaxs.z;
                } );
            if ( !walls.Any( c => c.Item3 < (trace.Distance * World.BlockScale) && c.Item3 < ReachDistance * World.BlockScale ) )
                return trace; // If no walls are closer than the trace, return the trace as is.
            var closestWall = walls.OrderBy( c => c.Item3 ).First();
            // Mutate the trace to hit the closest wall instead of the block.
            trace.Hit = true;
            trace.HitBlockPosition = (closestWall.Item1 / World.BlockScale).Floor() + closestWall.Item2.Forward();
            if ( closestWall.Item2 is Direction.North or Direction.West or Direction.Up )
            {
                trace.HitBlockPosition -= closestWall.Item2.Forward();
            }
            trace.HitFace = closestWall.Item2.Flip();
            trace.Distance = closestWall.Item3;
            trace.EndPosition = closestWall.Item1;

            return trace;
        }

        return trace;
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

        // If trace.HitBlockPosition is not in the player's build area, do not break blocks
        var hitPos = trace.HitBlockPosition;
        if ( HasBuildVolume && (hitPos.x < BuildAreaMins.x || hitPos.y < BuildAreaMins.y || hitPos.z < BuildAreaMins.z || hitPos.x > BuildAreaMaxs.x || hitPos.y > BuildAreaMaxs.y || hitPos.z > BuildAreaMaxs.z) )
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
            if ( !GiveBrokenBlocks )
            {
                world.SpawnBreakParticles( BreakingBlock.Value );
                world.Thinker.BreakBlock( BreakingBlock.Value );
            }
            else
            {
                var i = inventory.PutInFirstAvailableSlot( new ItemStack( ItemRegistry.GetItem( BreakingBlock.Value ) ) );
                world.SpawnBreakParticles( BreakingBlock.Value );
                world.Thinker.PlaceBlock( BreakingBlock.Value, new BlockData( 0 ) );
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
            // If placePos is not in the player's build area, do not place blocks
            if ( HasBuildVolume && (placePos.x < BuildAreaMins.x || placePos.y < BuildAreaMins.y || placePos.z < BuildAreaMins.z || placePos.x > BuildAreaMaxs.x || placePos.y > BuildAreaMaxs.y || placePos.z > BuildAreaMaxs.z) )
            {
                return;
            }
            inventory.TakeItem( SelectedSlot, 1 ); // Remove one item from the hotbar slot
            world.Thinker.PlaceBlock( placePos, new BlockData( (byte)item.Item.ID, 0 ) );
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
            if ( Input.Pressed( $"Slot{(i - inventory.InventorySize) + 1}" ) )
                SelectedSlot = i;
        }

        SelectedSlot += -Input.MouseWheel.y.FloorToInt().Clamp( -1, 1 );
        SelectedSlot = SelectedSlot.Clamp( inventory.InventorySize, inventory.InventorySize + inventory.HotbarSize - 1 );
    }
}
