using Sandbox.UI;

public partial class SpeedBuildHud : PanelComponent
{
    // a HUD used for Speed Build specific stuff
    GameTimer timer;
    protected override void OnTreeBuilt()
    {
        timer.BindClass( "hidden", () => !Input.Down( "score" ) );
    }
}