namespace Sandbox;

public partial class TextPanel : PanelComponent
{
    protected override void OnPreRender()
    {
        WorldRotation = Scene.Camera.WorldRotation * Rotation.FromYaw( 180f );
    }

    public static TextPanel Make( Vector3 Position, string Message )
    {
        var go = new GameObject();
        go.WorldPosition = Position;
        go.WorldScale *= 3f;
        var worldPanel = go.AddComponent<WorldPanel>();
        worldPanel.RenderScale = 0.5f;
        worldPanel.PanelSize = new Vector2( 2048, 512 );
        worldPanel.RenderOptions.Overlay = true;
        var textPanel = go.AddComponent<TextPanel>();
        textPanel.Text = Message;
        return textPanel;
    }
}