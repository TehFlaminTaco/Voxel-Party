using Sandbox;

public sealed class SampleTextureEffect : ParticleController
{
	public Texture SampleTexture { get; set; }
	Color[] colors = new Color[8];

	protected override void OnStart()
	{
		if ( !SampleTexture.IsValid() )
		{
			Log.Warning("Texture is fucked");
			colors = new Color[0];
			return;
		}
		
		for ( var i = 0; i < 8; i++ )
		{
			colors[i] = SampleTexture.GetPixel( i, i );
		}
	}

	protected override void OnParticleCreated( Particle p )
	{
		p.Color = colors[Game.Random.Int( 0, 7 )];
	}
}
