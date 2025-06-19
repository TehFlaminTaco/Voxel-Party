
using Sandbox.theoretical;
using Sandbox.Utility;

public class SelectTool : VoxelTool
{
    public override string Icon => "crop_free";

    public override string Name => "Select";
    public override string Shortcut => "SelectTool";

    public static Vector3Int? FirstPosition = null;
    public static Vector3Int? SecondPosition = null;
    public TimeSince startHold = 0f;

    static BBox selectBox;

    public override void DrawGizmos( Vector3Int blockPosition, Direction faceDirection )
    {


        if ( WasKeyPressed( "editor.paste" ) )
        {
            PasteRegion();
        }

        Gizmo.Draw.IgnoreDepth = true;
        if ( FirstPosition.HasValue && SecondPosition.HasValue )
        {
            var first = FirstPosition.Value;
            var second = SecondPosition.Value;
            if ( WasKeyPressed( "editor.cut" ) )
                CutRegion( first, second );
            else if ( WasKeyPressed( "editor.copy" ) )
                CopyRegion( first, second );
            else if ( WasKeyPressed( "editor.delete" ) )
                ClearRegion( first, second );
            else if ( WasKeyPressed( "editor.duplicate" ) )
                DuplicateRegion();

            if ( Gizmo.Control.BoundingBox( "SelectBox", selectBox, out BBox oBox ) )
            {
                selectBox = oBox;

                // Adjust FirstPosition to be the mins (Rounded) and SecondPosition to be the Maxs (Rounded)
                var mins = selectBox.Mins / World.BlockScale;
                var maxs = selectBox.Maxs / World.BlockScale;
                FirstPosition = new Vector3Int( (int)System.MathF.Floor( mins.x + 0.5f ), (int)System.MathF.Floor( mins.y + 0.5f ), (int)System.MathF.Floor( mins.z + 0.5f ) );
                SecondPosition = new Vector3Int( (int)System.MathF.Ceiling( maxs.x - 1.5f ), (int)System.MathF.Ceiling( maxs.y - 1.5f ), (int)System.MathF.Ceiling( maxs.z - 1.5f ) );

                return;
            }
            if ( !Gizmo.Pressed.Any )
            {
                selectBox = BBox.FromPoints( [FirstPosition.Value * World.BlockScale, SecondPosition.Value * World.BlockScale, (FirstPosition.Value + Vector3Int.One) * World.BlockScale, (SecondPosition.Value + Vector3Int.One) * World.BlockScale] );
            }
        }


        if ( !Gizmo.Pressed.Any && Gizmo.WasLeftMousePressed )
        {
            FirstPosition = blockPosition;
            startHold = 0f;
            SecondPosition = null; // Reset second position when starting a new selection
        }
        if ( !FirstPosition.HasValue )
        {
            Gizmo.Draw.Color = Color.Black;
            Gizmo.Draw.LineThickness = 2f;
            Gizmo.Draw.LineBBox( GetBlockBox( blockPosition ) );
        }
        else
        {
            if ( !Gizmo.Pressed.Any && Gizmo.IsLeftMouseDown )
            {
                SecondPosition = blockPosition;
                selectBox = BBox.FromPoints( [FirstPosition.Value * World.BlockScale, SecondPosition.Value * World.BlockScale, (FirstPosition.Value + Vector3.One) * World.BlockScale, (SecondPosition.Value + Vector3.One) * World.BlockScale] );
            }

            Gizmo.Draw.IgnoreDepth = true;
            Gizmo.Draw.Color = Color.Blue;
            Gizmo.Draw.LineThickness = 2f;
            Gizmo.Draw.LineBBox( selectBox );

            Gizmo.Draw.Color = Color.Blue.WithAlpha( 0.2f );
            Gizmo.Draw.LineThickness = 1f;
            Gizmo.Draw.SolidBox( GetBoxFromBlocks( FirstPosition.Value, SecondPosition.HasValue ? SecondPosition.Value : blockPosition ) );
        }

        if ( !Gizmo.Pressed.Any && (Gizmo.WasLeftMouseReleased && startHold < 0.1f) )
        {
            FirstPosition = null; // Reset selection on quick release
            SecondPosition = null;
        }
    }

    private void NormalizePositions()
    {
        var minX = System.Math.Min( FirstPosition.Value.x, SecondPosition.Value.x );
        var minY = System.Math.Min( FirstPosition.Value.y, SecondPosition.Value.y );
        var minZ = System.Math.Min( FirstPosition.Value.z, SecondPosition.Value.z );
        var maxX = System.Math.Max( FirstPosition.Value.x, SecondPosition.Value.x );
        var maxY = System.Math.Max( FirstPosition.Value.y, SecondPosition.Value.y );
        var maxZ = System.Math.Max( FirstPosition.Value.z, SecondPosition.Value.z );
        FirstPosition = new Vector3Int( minX, minY, minZ );
        SecondPosition = new Vector3Int( maxX, maxY, maxZ );
    }

    public void CutRegion( Vector3Int first, Vector3Int second )
    {
        NormalizePositions();
        var area = BlockData.GetAreaInBox( first, second - first + Vector3Int.One );
        EditorUtility.Clipboard.Copy( World.Active.SerializeRegion( first, second ) );
        SceneEditorSession.Active.QuickAddUndo( "Cut Region", () =>
        {
            for ( int x = 0; x < area.GetLength( 0 ); x++ )
            {
                for ( int y = 0; y < area.GetLength( 1 ); y++ )
                {
                    for ( int z = 0; z < area.GetLength( 2 ); z++ )
                    {
                        var pos = new Vector3Int( first.x + x, first.y + y, first.z + z );
                        World.Active.SetBlock( pos, area[x, y, z] );
                    }
                }
            }
        }, () =>
        {
            for ( int x = 0; x < area.GetLength( 0 ); x++ )
            {
                for ( int y = 0; y < area.GetLength( 1 ); y++ )
                {
                    for ( int z = 0; z < area.GetLength( 2 ); z++ )
                    {
                        var pos = new Vector3Int( first.x + x, first.y + y, first.z + z );
                        World.Active.SetBlock( pos, new BlockData( 0 ) );
                    }
                }
            }
        } );
    }

    public void CopyRegion( Vector3Int first, Vector3Int second )
    {
        NormalizePositions();
        EditorUtility.Clipboard.Copy( World.Active.SerializeRegion( first, second ) );
        Log.Info( World.Active.GetStructureBounds( EditorUtility.Clipboard.Paste() ) );
    }

    public void PasteRegion()
    {
        // Prefer pasting as a Structure?

        var str = EditorUtility.Clipboard.Paste();
        var blockData = World.Active.GetStructureData( str );

        if ( blockData == null )
        {
            return;
        }

        VoxelBuilder.SelectTool( new PasteStructureTool( str, this ) );
    }

    public void DuplicateRegion()
    {
        NormalizePositions();
        var first = FirstPosition.Value;
        var second = SecondPosition.Value;
        var str = World.Active.SerializeRegion( first, second );

        VoxelBuilder.SelectTool( new PasteStructureTool( str, this, first ) );
    }

    public void ClearRegion( Vector3Int first, Vector3Int second )
    {
        NormalizePositions();
        var area = BlockData.GetAreaInBox( first, second - first + Vector3Int.One );
        SceneEditorSession.Active.QuickAddUndo( "Clear Region", () =>
        {
            for ( int x = 0; x < area.GetLength( 0 ); x++ )
            {
                for ( int y = 0; y < area.GetLength( 1 ); y++ )
                {
                    for ( int z = 0; z < area.GetLength( 2 ); z++ )
                    {
                        var pos = new Vector3Int( first.x + x, first.y + y, first.z + z );
                        World.Active.SetBlock( pos, area[x, y, z] );
                    }
                }
            }
        }, () =>
        {
            for ( int x = 0; x < area.GetLength( 0 ); x++ )
            {
                for ( int y = 0; y < area.GetLength( 1 ); y++ )
                {
                    for ( int z = 0; z < area.GetLength( 2 ); z++ )
                    {
                        var pos = new Vector3Int( first.x + x, first.y + y, first.z + z );
                        World.Active.SetBlock( pos, new BlockData( 0 ) );
                    }
                }
            }
        } );
    }
}