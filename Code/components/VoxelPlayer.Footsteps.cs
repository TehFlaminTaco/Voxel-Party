using System;
using System.Security.Cryptography.X509Certificates;
using Sandbox.Audio;

public partial class VoxelPlayer
{
	[Property] public bool EnableFootstepSounds { get; set; } = true;
	[Property] public MixerHandle FootstepMixer { get; set; }
	
	public bool DebugFootsteps;
	TimeSince _timeSinceStep;
	
	public void DoFootsteps()
	{
		var footL = Controller.Renderer.SceneModel.GetBoneWorldTransform( "calf_L" );
		GroundBlockL = ItemRegistry.GetItem(new BlockTrace().WithDirection( Vector3.Down )
			.WithStart( footL.Position ).WithDistance( 2 ).WithWorld( world ).Run().HitBlockPosition);
		
		var footR = Controller.Renderer.SceneModel.GetBoneWorldTransform( "calf_R" );
		GroundBlockR = ItemRegistry.GetItem(new BlockTrace().WithDirection( Vector3.Down )
			.WithStart( footR.Position ).WithDistance( 2 ).WithWorld( world ).Run().HitBlockPosition );
		
		if ( EnableFootstepSounds ) Controller.Renderer.SceneModel.OnFootstepEvent += OnFootstepEvent;
	}
	
	void OnFootstepEvent( SceneModel.FootstepEvent e )
	{
		if ( !Controller.IsOnGround ) return;
		if ( !EnableFootstepSounds ) return;
		if ( _timeSinceStep < 0.2f ) return;

		_timeSinceStep = 0;

		PlayFootstepSound( e.Transform.Position, e.Volume, e.FootId );
	}

	public void PlayFootstepSound( Vector3 worldPosition, float volume, int foot )
	{
		volume *= Controller.WishVelocity.Length.Remap( 0, 400 );
		if ( volume <= 0.1f ) return;
		if ( GroundBlockL is null && GroundBlockR is null ) return;

		SoundEvent sound;
		if ( Input.Down(Controller.AltMoveButton) ) 
			sound = foot == 0 ? GroundBlockL.Block.RunStepSound : GroundBlockR.Block.RunStepSound;
		else if ( Input.Down("crouch") ) 
			sound = foot == 0 ? GroundBlockL.Block.CrouchStepSound : GroundBlockR.Block.CrouchStepSound;
		else sound = foot == 0 ? GroundBlockL.Block.WalkStepSound : GroundBlockR.Block.WalkStepSound;
		
		if ( sound is null )
		{
			if ( DebugFootsteps )
			{
				DebugOverlay.Sphere( new Sphere( worldPosition, volume ), duration: 10, color: Color.Orange, overlay: true );
			}

			return;
		}

		var handle = GameObject.PlaySound( sound, 0 );
		handle.FollowParent = false;
		handle.TargetMixer = FootstepMixer.GetOrDefault();

		if ( DebugFootsteps )
		{
			DebugOverlay.Sphere( new Sphere( worldPosition, volume ), duration: 10, overlay: true );
			DebugOverlay.Text( worldPosition, $"{sound.ResourceName}", size: 14, flags: TextFlag.LeftTop, duration: 10, overlay: true );
		}
	}
}
