using System.Threading;
using System.Threading.Tasks;

namespace Sandbox.theoretical;

[GameResource( "Structure", "struct", "a structure that can be placed", Category = "Voxel Party", Icon = "archive" )]
public partial class Structure : GameResource
{
	public enum StructureDifficulty
	{
		Easy,
		Standard,
		Hard
	}

	[Hide]
	public string StructureData { get; set; }

	public StructureDifficulty ReplicateDifficulty = StructureDifficulty.Standard;
}
