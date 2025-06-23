using System;
using Sandbox.Movement;
public class MoveModeFly : MoveMode
{
    public override int Score( PlayerController controller )
    {
        return !VoxPly.IsFlying ? -1 : 10;
    }
    
    [Property, Group("Movement")] public float FlyingWalkSpeed { get; set; } = 200;
    [Property, Group("Movement")] public float FlyingRunSpeed { get; set; } = 400;
    [Property, Group("Movement")] public float AscendSpeed { get; set; } = 200;
    [Property, Group("Movement")] public float DescendSpeed { get; set; } = 150;

    [Property, Group("Angles")] public float GroundAngle { get; set; } = 45f;
    [Property, Group("Angles")] public float StepUpHeight { get; set; } = 18f;
    [Property, Group("Angles")] public float StepDownHeight { get; set; } = 18f;
    
    [RequireComponent] VoxelPlayer VoxPly { get; set; }

    public override bool AllowGrounding => true;
    public override bool AllowFalling => true;


    public override void AddVelocity()
    {
        Rigidbody body = Controller.Body;
        Vector3 wishVelocity = Controller.WishVelocity;
        if ( !wishVelocity.IsNearZeroLength )
        {
            float z = body.Velocity.z;
            Vector3 vector = body.Velocity;
            float length = vector.Length;
            float num2 = MathF.Max( wishVelocity.Length, length );
            vector = vector.AddClamped( wishVelocity * 1f, wishVelocity.Length );

            if ( vector.Length > num2 )
            {
                vector = vector.Normal * num2;
            }
            if ( Controller.IsOnGround )
            {
                vector.z = z;
            }

            body.Velocity = vector;
        }
    }

    public override void PrePhysicsStep()
    {
        base.PrePhysicsStep();
        if ( StepUpHeight > 0f )
        {
            TrySteppingUp( StepUpHeight );
        }
    }

    public override void PostPhysicsStep()
    {
        base.PostPhysicsStep();
        if ( StepDownHeight > 0f )
        {
            StickToGround( StepDownHeight );
        }
    }

    public override bool IsStandableSurface( in SceneTraceResult result )
    {
        if ( Vector3.GetAngle( in Vector3.Up, in result.Normal ) > GroundAngle )
        {
            return false;
        }

        return true;
    }

    Vector3.SmoothDamped smoothedMovement;
    public override Vector3 UpdateMove( Rotation eyes, Vector3 input )
    {
        eyes = eyes.Angles().WithPitch( 0 );
        
        var upDown =
              (Input.Down( "jump" ) ? Vector3.Up : Vector3.Zero)
            + (Input.Down( "duck" ) ? Vector3.Down : Vector3.Zero);
        input += upDown;
        input = input.ClampLength( 1f );
        
        Vector3 moveDir = eyes * input;
        bool isRunning = Input.Down( Controller.AltMoveButton );
        if ( Controller.RunByDefault )
        {
            isRunning = !isRunning;
        }

        float horizontalSpeed = isRunning ? FlyingRunSpeed : FlyingWalkSpeed;
        
        if ( moveDir.IsNearlyZero( 0.1f ) ) moveDir = 0f;
        else smoothedMovement.Current = moveDir.Normal * smoothedMovement.Current.Length;

        var target = (moveDir.WithZ( 0 ) * horizontalSpeed) + 
                     Vector3.Zero.WithZ( moveDir.z > 0 ? moveDir.z * AscendSpeed : moveDir.z * DescendSpeed );
        smoothedMovement.Target = target;
        smoothedMovement.SmoothTime = smoothedMovement.Target.Length < smoothedMovement.Current.Length
	        ? Controller.DeaccelerationTime 
	        : Controller.AccelerationTime;
        
        smoothedMovement.Update( Time.Delta );
        if ( smoothedMovement.Current.IsNearlyZero( 0.01f ) ) smoothedMovement.Current = 0f;

        return smoothedMovement.Current;
    }

    public override void UpdateRigidBody( Rigidbody body )
    {
        base.UpdateRigidBody( body );
        body.Gravity = false;
        body.LinearDamping = 5f;
    }
}
