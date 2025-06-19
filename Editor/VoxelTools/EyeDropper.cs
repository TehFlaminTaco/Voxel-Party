public class EyeDropper : VoxelTool
{
    public override string Icon => "colorize";
    public override string Name => "Eye Dropper";
    public override string Shortcut => "EyeDropper";


    public override void DrawGizmos( Vector3Int blockPosition, Direction faceDirection )
    {
        base.DrawGizmos( blockPosition, faceDirection );
        Gizmo.Draw.Color = Color.Cyan;
        Gizmo.Draw.LineThickness = 2f;
        Gizmo.Draw.LineBBox( GetBlockBox( blockPosition ) );
    }

    public override void LeftMousePressed( Vector3Int blockPosition, Direction faceDirection )
    {
        base.LeftMousePressed( blockPosition, faceDirection );
        VoxelBuilder.SelectItem( World.Active.GetBlock( blockPosition ).BlockID );
    }
}