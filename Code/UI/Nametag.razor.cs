using System;
namespace Sandbox;

public partial class Nametag : PanelComponent
{
    public const float MinVisibleDistance = 250f;
    public const float FadeRange = 400f;
    protected override void OnPreRender()
    {
        Panel.Style.Opacity = MathF.Min( MathF.Max( FadeRange - (WorldPosition.Distance( Scene.Camera.WorldPosition ) - MinVisibleDistance), 0f ), FadeRange ) / FadeRange;
    }
}