using System.Threading.Tasks;

public abstract class Gamemode : Component
{
    public NetList<VoxelPlayer> Players { get; set; } = new();

    [Property] public GameObject Spawn { get; set; }

    public virtual GamemodeHud Hud => GamemodeHud.Instance;

    public bool IsPlaying = false;

    protected override void OnStart()
    {
        if ( Networking.IsHost )
        {
            if ( IsPlaying ) return;
            IsPlaying = true;
            GameModeLogic();
        }
    }

    public abstract Task GameModeLogic();
}