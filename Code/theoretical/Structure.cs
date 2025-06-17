using System.Threading;
using System.Threading.Tasks;
using Editor;

namespace Sandbox.theoretical;

[GameResource( "Structure", "struct", "a structure that can be placed", Category = "Voxel Party", Icon = "archive" )]
public partial class Structure : GameResource
{
	[Hide]
	public string StructureData { get; set; }
}

[DropObject("structure", "struct")]
public class DropStructure : BaseDropObject
{
	Structure _structure;
	protected override Task Initialize( string dragData, CancellationToken token )
	{
		PackageStatus = "Loading Structure";
		_structure = Json.Deserialize<Structure>( dragData );
		return Task.CompletedTask;
	}

	public override Task OnDrop()
	{
		var obj = new GameObject();
		obj.AddComponent<StructureLoader>().LoadedStructure = _structure;
		return Task.CompletedTask;
	}
}
