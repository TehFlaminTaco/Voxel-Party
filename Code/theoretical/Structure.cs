using System.Threading;
using System.Threading.Tasks;

namespace Sandbox.theoretical;

[GameResource( "Structure", "struct", "a structure that can be placed", Category = "Voxel Party", Icon = "archive" )]
public partial class Structure : GameResource
{
	[Property, ToggleGroup( "SpeedBuildStructure" )]
	public bool SpeedBuildStructure { get; set; } = false;

	[Property, ToggleGroup( "SpeedBuildStructure" )]
	public int SecondsToBuild { get; set; } = 60;

	[Property, ToggleGroup( "SpeedBuildStructure" )]
	public int Difficulty { get; set; } = 0;
	
	[Hide]
	public string StructureData { get; set; }
}
