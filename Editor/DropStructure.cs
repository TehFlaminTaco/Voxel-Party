using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sandbox.theoretical;

namespace Sandbox;
using Editor;

[DropObject( "structure", "struct" )]
public class DropStructure : BaseDropObject
{
	Structure _structure;
	GameObject obj;
	StructureLoader loader;
	
	protected override Task Initialize( string dragData, CancellationToken token )
	{
		_structure = InstallAsset( dragData, token ).Result.LoadResource<Structure>();
		
		obj = Game.ActiveScene?.CreateObject();
		obj.WorldPosition = traceTransform.Position;
		obj.Name = _structure.ResourceName;
		
		loader = obj.AddComponent<StructureLoader>();
		loader.LoadedStructure = _structure;
		loader.Enabled = false;
		loader.Enabled = true;
		
		return Task.CompletedTask;
	}

	public override void UpdateDrag( SceneTraceResult tr, Gizmo.SceneSettings settings )
	{
		obj.WorldPosition = tr.HitPosition;
	}
}
