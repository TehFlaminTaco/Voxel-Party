using System;

public class VoxelPlayer : Component
{
    World world => Scene.GetAll<WorldThinker>().First().World;

    public Inventory inventory = new Inventory( 9 + (9 * 3) ); // 9 slots for the hotbar, and 27 slots for the inventory (3 rows of 9 slots each)

    [Property] public bool CreativeMode = true;

    protected override void OnStart()
    {
        SpawnBlockBreakingEffect();
    }

    protected override void OnUpdate()
    {
        if ( IsProxy )
            return;

        ShowHoveredFace();
        HandleBreak();
        HandlePlace();
        HandleHotbar();
    }

    public BlockTraceResult EyeTrace()
    {
        var pc = GetComponent<PlayerController>();
        return Scene.GetAll<WorldThinker>().First().World.Trace(
            pc.EyePosition,
            pc.EyePosition + pc.EyeAngles.Forward * 1024f
        ).Run();
    }

    public float BreakTime = 0f;
    public Vector3Int? BreakingBlock = null;
    public Direction BreakingFace = Direction.None;
    SceneCustomObject blockBreakEffect;

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
            var v1 = new Sandbox.Vertex( p1, rect.BottomLeft, Color.White );
            var v2 = new Sandbox.Vertex( p2, rect.BottomRight, Color.White );
            var v3 = new Sandbox.Vertex( p3, rect.TopLeft, Color.White );
            var v4 = new Sandbox.Vertex( p4, rect.TopRight, Color.White );
            blockBreakEffect.ColorTint = Color.White.WithAlpha( 1f );
            Graphics.Draw( new List<Sandbox.Vertex> { v1, v2, v3, v4 }, 4, Material.Load( "materials/textureatlastranslucent.vmat" ), new RenderAttributes(), Graphics.PrimitiveType.TriangleStrip );
        };
    }
    public void HandleBreak()
    {
        if ( CreativeMode )
        {
            BreakingBlock = null;
            BreakTime = 0f;
            if ( !Input.Pressed( "Attack1" ) )
                return;
            var tr = EyeTrace();
            if ( !tr.Hit )
                return;

            world.Thinker.BreakBlock( tr.HitBlockPosition );
            return;
        }

        blockBreakEffect.Transform = global::Transform.Zero.WithPosition( WorldPosition );
        BreakTime += Time.Delta;
        if ( !Input.Down( "Attack1" ) )
        {
            BreakingBlock = null;
            BreakTime = 0f;
            return;
        }

        var trace = EyeTrace();
        if ( !trace.Hit )
        {
            BreakingBlock = null;
            BreakTime = 0f;
            return;
        }

        if ( trace.HitBlockPosition != BreakingBlock )
        {
            BreakingBlock = trace.HitBlockPosition;
            BreakTime = 0f; // Reset break time if we hit a new block
            return;
        }

        if ( trace.HitFace != BreakingFace )
        {
            BreakingFace = trace.HitFace;
            BreakTime = 0f; // Reset break time if we hit a different face
            return;
        }
        var block = world.GetBlock( BreakingBlock.Value ).GetBlock();
        int progress = Math.Min( (int)(BreakTime * 20 / block.Hardness), 10 );
        if ( progress == 10 )
            world.Thinker.BreakBlock( BreakingBlock.Value );

        // Draw the breaking face

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
        for ( int i = 0; i < 9; i++ )
        {
            if ( Input.Pressed( $"Slot{i + 1}" ) )
                SelectedSlot = i;
        }
        if ( Input.Pressed( "NextSlot" ) )
        {
            SelectedSlot++;
            if ( SelectedSlot >= 9 )
                SelectedSlot = 0;
        }
        if ( Input.Pressed( "LastSlot" ) )
        {
            SelectedSlot--;
            if ( SelectedSlot < 0 )
                SelectedSlot = 8;
        }
    }
}
