using System;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public abstract class VoxelTool
{
    public abstract string Icon { get; }
    public abstract string Name { get; }
    public virtual string Shortcut => null; // Optional shortcut key for the tool
    public int ToolID { get; set; } // Unique ID for the tool, used for selection

    public virtual void OnSelected() // Called when the tool is selected
    {
        VoxelBuilder.ToolOptionsWindow?.Layout.Clear( true );
        this.MakeOptions( VoxelBuilder.ToolOptionsWindow?.Layout );
    }
    public virtual void OnDeselected() { } // Called when the tool is deselected

    public virtual void DrawGizmos( Vector3Int blockPosition, Direction faceDirection ) { } // Optional method to draw any overlay UI for the tool
    public virtual void LeftMousePressed( Vector3Int blockPosition, Direction faceDirection )
    {
        // Default implementation does nothing
    }
    public virtual void LeftMouseUp( Vector3Int blockPosition, Direction faceDirection )
    {
        // Default implementation does nothing
    }
    // Called every frame while the left mouse button is held down
    public virtual void LeftMouseDown( Vector3Int blockPosition, Direction faceDirection )
    {
        // Default implementation does nothing
    }

    public virtual void MakeOptions( Layout parent )
    {

        foreach ( var prop in this.GetType().GetProperties().Where( p => p.GetCustomAttribute<ToolPropertyAttribute>() != null ) )
        {
            var attr = prop.GetCustomAttribute<ToolPropertyAttribute>();
            if ( attr == null )
            {
                Log.Warning( $"Property {prop.Name} does not have a ToolPropertyAttribute (Impossible?)" );
                continue;
            }

            if ( prop.PropertyType == typeof( bool ) )
            {
                var toggle = new Checkbox( GetCleanName( prop.Name ) );
                toggle.Watch( () => (bool)prop.GetValue( this ), b => toggle.Value = b );
                toggle.Value = (bool)prop.GetValue( this );
                toggle.Clicked = () =>
                {
                    prop.SetValue( this, toggle.Value );
                };
                parent.Add( toggle );
            }
            else
            {
                Log.Warning( $"Property {prop.Name} of type {prop.PropertyType} is not supported for tool options." );
            }
        }

        foreach ( var button in this.GetType().GetMethods().Where( c => c.GetCustomAttribute<VoxelToolButtonAttribute>() != null ) )
        {
            var buttonWidget = new Button( GetCleanName( button.Name ) );
            buttonWidget.Clicked = () =>
            {
                button.Invoke( this, [] ); // Invoke the method with no parameters
            };
            parent.Add( buttonWidget );
        }
    }

    public string GetCleanName( string name )
    {
        // Split on camel case and underscores, then capitalize the first letter of each word
        return System.Text.RegularExpressions.Regex.Replace( name, "([a-z])([A-Z])", "$1 $2" )
            .Replace( "_", " " )
            .Replace( "  ", " " )
            .Trim()
            .Split( ' ' )
            .Select( word => char.ToUpper( word[0] ) + word.Substring( 1 ).ToLower() )
            .Aggregate( ( current, next ) => current + " " + next );
    }

    public BBox GetFaceBox( Vector3Int blockPosition, Direction faceDirection )
    {
        var world = World.Active;
        var block = world.GetBlock( blockPosition ).GetBlock();
        var box = block.GetCollisionAABBWorld( world, blockPosition ).Grow( 0.1f ); // This gets the box in world coordinates relative to the chunk
                                                                                    // Shrink the box down on one axis depending on faceDirection.
        switch ( faceDirection )
        {
            case Direction.North: box.Mins.x = box.Maxs.x; break;
            case Direction.South: box.Maxs.x = box.Mins.x; break;
            case Direction.East: box.Maxs.y = box.Mins.y; break;
            case Direction.West: box.Mins.y = box.Maxs.y; break;
            case Direction.Down: box.Maxs.z = box.Mins.z; break;
            case Direction.Up: box.Mins.z = box.Maxs.z; break;

        }
        return box;
    }

    public BBox GetBlockBox( Vector3Int blockPosition )
    {
        var center = (blockPosition + 0.5f) * World.BlockScale;
        return BBox.FromPositionAndSize( center, new Vector3( World.BlockScale, World.BlockScale, World.BlockScale ) );
    }

    public BBox GetBoxFromBlocks( params Vector3Int[] positions )
    {
        return BBox.FromPoints(
            positions.Select( pos => pos * World.BlockScale ).Concat(
                positions.Select( pos => (pos + Vector3Int.One) * World.BlockScale )
            ).Select( pos => (Vector3)pos )
        );
    }

    public bool IsKeyDown( string identifier )
    {
        return EditorShortcuts.IsDown( identifier );
    }

    public bool WasKeyPressed( string identifier )
    {
        return EditorShortcuts.IsDown( identifier ) && !VoxelBuilder.WasKeyDown.GetValueOrDefault( identifier, false );
    }

    public bool WasKeyReleased( string identifier )
    {
        return !EditorShortcuts.IsDown( identifier ) && VoxelBuilder.WasKeyDown.GetValueOrDefault( identifier, false );
    }

}