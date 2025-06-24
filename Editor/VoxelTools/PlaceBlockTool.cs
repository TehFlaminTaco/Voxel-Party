using System;

public class PlaceBlockTool : VoxelTool
{
    public override string Icon => "view_in_ar";
    public override string Name => "Place Block";
    public override string Shortcut => "PlaceBlock";

    public override void DrawGizmos( Vector3Int blockPosition, Direction faceDirection )
    {
        Gizmo.Draw.Color = Color.Black;
        Gizmo.Draw.LineThickness = 2f;
        Gizmo.Draw.LineBBox( GetFaceBox( blockPosition, faceDirection ) );
    }

    public override void LeftMousePressed( Vector3Int blockPosition, Direction faceDirection )
    {
        var lastBlockData = World.Active.GetBlock( blockPosition + faceDirection.Forward() * 1 );
        var newBlockData = BlockData.WithPlacementBlockData( VoxelBuilder.SelectedItemID, faceDirection, Gizmo.Camera.Rotation.Forward );
        SceneEditorSession.Active.AddUndo( "Place Block", () => World.Active.SetBlock( blockPosition + faceDirection.Forward() * 1, lastBlockData ),
            () => World.Active.SetBlock( blockPosition + faceDirection.Forward() * 1, newBlockData ) );
        World.Active.SetBlock( blockPosition + faceDirection.Forward() * 1, newBlockData );
    }
}