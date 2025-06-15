using Boxfish.Library;
using Boxfish.Utility;
using Sandbox;
using Sandbox.Volumes;

public sealed class VoxelBuilder : Component, Component.ExecuteInEditor
{
	NetworkedVoxelVolume Volume { get; set; }
	Vector3Int HighlightPosition;
	Vector3Int NewBlockPosition;

	protected override void OnStart()
	{
		Volume = Components.Get<NetworkedVoxelVolume>();
	}

	protected override void DrawGizmos()
	{
		var plane = new Plane( Vector3.Zero, Vector3.Up );
		var tr = plane.Trace( Gizmo.CurrentRay, true );
		if ( !tr.HasValue ) return;
		
		var position = Volume.WorldToVoxel( tr.Value.WithZ( 0 ).SnapToGrid( Volume.Scale ) );
		var query = Volume.Query( position );

		 var NewBlockPosition = position;
		 var box = BBox.FromPositionAndSize( Volume.VoxelToWorld( NewBlockPosition ), Volume.Scale );
		 Gizmo.Draw.LineBBox( box );
		 
		 if ( Gizmo.WasLeftMousePressed )
		 {
			 Log.Info("hi");
			 var voxel = new Voxel( Color32.White, 1 ); //Volume.Atlas.Items.FirstOrDefault().Index );
			 ChangeVoxel( Volume, NewBlockPosition, voxel );
		 }
	}
	
	void ChangeVoxel( VoxelVolume volume, Vector3Int position, Voxel voxel )
	{
		// Let's query the position, and set the voxel using SetTrackedVoxel.
		var query = volume.Query( position );
		if ( volume.IsValidVoxel( voxel ) && query.HasVoxel ) return;

		if ( volume is NetworkedVoxelVolume netVolume ) // We want to broadcast set the voxel.
			netVolume.BroadcastSet( position, voxel );
		else
			volume.SetTrackedVoxel( position, voxel );

		// Call callback from atlas item.
		if ( !volume.Atlas.TryGet( query.Voxel.TextureIndex, out var item ) )
			return;

		// We don't actually do anything in the base atlas with these, but you can do whatever you want!
		if ( !voxel.Valid ) item.OnBlockBroken?.Invoke( query );
		else item.OnBlockPlaced?.Invoke( query );
	}
}
