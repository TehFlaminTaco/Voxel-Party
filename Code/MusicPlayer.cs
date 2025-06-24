using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Audio;

public sealed class MusicPlayer : Component
{
    [Property] List<SoundEvent> MusicFiles { get; set; }
    SoundHandle Music { get; set; }

    protected override void OnStart()
    {
	    var sound = MusicFiles[Game.Random.Int( 0, MusicFiles.Count - 1 )];
	    sound.UI = true;
	    
	    Music = Sound.PlayFile( sound.Sounds.FirstOrDefault() );
	    Music.TargetMixer = Mixer.FindMixerByName( "Music" );
	    Music.Volume = 0.5f;
    }
    
    protected override void OnFixedUpdate()
    {
	    if ( Music.Finished )
	    {
		    var sound = MusicFiles[ Game.Random.Int( 0, MusicFiles.Count - 1 )];
		    sound.UI = true;
	    
		    Music = Sound.PlayFile( sound.Sounds.FirstOrDefault() );
		    Music.TargetMixer = Mixer.FindMixerByName( "Music" );
		    Music.Volume = 0.5f;
	    }
    }
}
