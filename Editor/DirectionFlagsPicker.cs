
using System;

[CustomEditor( typeof( DirectionFlags ) )]
public class DirectionFlagsPicker : ControlObjectWidget
{
    public override bool SupportsMultiEdit => false;
    public DirectionFlagsPicker( SerializedProperty property ) : base( property, true )
    {
        Layout = Layout.Grid();
        Layout.Spacing = 2;
        var grid = Layout as GridLayout;
        if ( !SerializedObject.TryGetProperty( nameof( DirectionFlags.Settings ), out var settings ) )
            Log.Warning( "Failed to get property!" );
        bool[] settingsArray = settings.GetValue<bool[]>( [] );
        if ( settingsArray.Length < 6 )
        {
            Log.Error( "Failed to get the settings!" );
            return;
        }
        for ( int _i = 1; _i <= 6; _i++ )
        {
            int i = _i;
            grid.AddCell( 0, i - 1, new Label( Enum.GetName( (Direction)i ) ) );
            var checkBox = new Checkbox();
            checkBox.Value = settingsArray[i - 1];
            checkBox.Toggled = () =>
            {
                settingsArray[i - 1] = checkBox.Value;
                // Do we need to manually re-set this? I hope not.
            };
            grid.AddCell( 1, i - 1, checkBox );
        }


    }
}