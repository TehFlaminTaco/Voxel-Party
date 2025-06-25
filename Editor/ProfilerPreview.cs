using System;

[CustomEditor( typeof( Profiler ) )]
public class ProfilerPreview : ControlObjectWidget
{
    public override bool SupportsMultiEdit => false;
    TextEdit textArea;
    public ProfilerPreview( SerializedProperty property ) : base( property, true )
    {
        Layout = Layout.Column();

        textArea = new TextEdit()
        {
            ReadOnly = true,
            PlainText = "loading...",
            FixedHeight = 20,
            VerticalScrollbarMode = ScrollbarMode.Off
        };
        Layout.Add( textArea );
    }

    protected override void OnPaint()
    {
        base.OnPaint();

        SerializedObject.TryGetProperty( nameof( Profiler.Samples ), out var samplesProperty );
        var samples = samplesProperty.GetValue<Queue<long>>();
        if ( samples.Count == 0 )
            return;
        if ( textArea == null ) Log.Warning( "AAAH" );
        var averageTime = samples.Sum() / samples.Count;
        if ( averageTime < 1000 )
        {
            textArea.PlainText = $"{averageTime} µs";
        }
        else if ( averageTime < 1000000 )
        {
            textArea.PlainText = $"{averageTime / 1000f:N2} ms";
        }
        else
        {
            textArea.PlainText = $"{(averageTime / 1000) / 1000f:N2} s";
        }
    }
}