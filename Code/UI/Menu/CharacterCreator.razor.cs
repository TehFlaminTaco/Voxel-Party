using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sandbox.UI;

public partial class CharacterCreator : Panel
{
    public static List<Skin> Skins { get; set; } = new();
    public static int Selected { get; set; }
    public TextEntry Name { get; set; }

    public class Skin
    {
        public string Username { get; set; }
        public string BaseSkinName { get; set; }
        public Texture Texture;
    }

    public CharacterCreator()
    {
        var path = "materials/models/skins/";
        foreach ( var i in FileSystem.Mounted.FindFile( path, "*.png", true ) )
        {
            if ( Skins.Any( c => c.BaseSkinName == i ) )
                continue;
            Skins.Add( new Skin { BaseSkinName = i, Texture = Texture.Load( $"{path}{i}" ) } );
        }
    }

    public override void Tick()
    {
        if ( lastBadName != null && Name.Value != lastBadName )
        {
            lastBadName = null;
            LastBadReason = "";
            Name.RemoveClass( "bad" );
        }
        Selected = Selected.Clamp( 0, Skins.Count - 1 );
    }
    string lastBadName = null;
    string LastBadReason = "";
    public void Buzz( string reason )
    { // BAD
        lastBadName = Name.Value;
        LastBadReason = reason;
        Name.AddClass( "bad" );
    }

    public async Task AddSkin( string name )
    {
        Name.Disabled = true;
        name = new Regex( @"\W" ).Replace( name.ToLower(), "" );
        if ( Skins.Any( c => c.Username == name ) )
        {
            Buzz( "Skin already added!" );
            return;
        }

        var tex = await VoxelPlayer.GetTextureFromSkin( name );
        if ( Skins.Any( c => c.Texture == tex ) )
        {
            Buzz( "Skin already added!" );
            return;
        }

        if ( tex == null )
        {
            Buzz( "No skin with that name exists!" );
            return;
        }


        Skins.Add( new Skin { Username = name, Texture = tex } );
        Name.Disabled = false;
    }

    protected override int BuildHash() => HashCode.Combine( Skins.GetHashCode(), Selected );
}
