using Sandbox.theoretical;

namespace Sandbox;

public class StructureLoader : Component, Component.ExecuteInEditor
{
	[Property, Alias("Structure")] public Structure LoadedStructure { get; set; }
	
	protected override void OnStart()
	{
		World.Active.LoadStructure( Helpers.WorldToVoxel(WorldPosition), LoadedStructure.StructureData );
	}

	protected override void OnPreRender()
	{
		//if ( !Game.IsEditor ) return;

		
	}
}
