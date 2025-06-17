using Sandbox.theoretical;

namespace Sandbox;

public class StructureLoader : Component, Component.ExecuteInEditor
{
	[Property, Alias("Structure")] public Structure LoadedStructure { get; set; }
	public BlockData BlockData { get; set; }
	
	protected override void OnEnabled()
	{
		var data = World.Active.LoadStructure( Helpers.WorldToVoxel(WorldPosition), LoadedStructure.StructureData );
		if ( Game.IsEditor ) return;
	}

	protected override void DrawGizmos()
	{
		if ( !Game.IsEditor ) return;

		//World.Active.LoadStructure( Helpers.WorldToVoxel(WorldPosition), LoadedStructure.StructureData );
		//Gizmo.Draw.SolidBox( LoadedStructure.StructureData );
	}
}
