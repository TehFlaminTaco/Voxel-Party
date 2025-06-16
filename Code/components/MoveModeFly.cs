using System;
using Sandbox.Movement;
public class MoveModeFly : MoveMode
{
    public override int Score( PlayerController controller )
    {
        return (!VoxPly.CreativeMode || Controller.IsOnGround) ? -1 : 10;
    }

    [Property]
    public float GroundAngle { get; set; } = 45f;

    [Property]
    public float StepUpHeight { get; set; } = 18f;

    [Property]
    public float StepDownHeight { get; set; } = 18f;

    public override bool AllowGrounding => true;

    public override bool AllowFalling => true;

    private VoxelPlayer VoxPly => Controller.GetComponent<VoxelPlayer>();


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

    private Vector3.SmoothDamped smoothedMovement;
    public override Vector3 UpdateMove( Rotation eyes, Vector3 input )
    {
        Angles value = eyes.Angles();
        value.pitch = 0f;
        eyes = value;
        var updown =
              (Input.Down( "jump" ) ? Vector3.Up : Vector3.Zero)
            + (Input.Down( "duck" ) ? Vector3.Down : Vector3.Zero);
        input += updown;
        input = input.ClampLength( 1f );
        Vector3 vector = eyes * input;
        bool flag = Input.Down( Controller.AltMoveButton );
        if ( Controller.RunByDefault )
        {
            flag = !flag;
        }

        float num = (flag ? Controller.RunSpeed : Controller.WalkSpeed);
        if ( vector.IsNearlyZero( 0.1f ) )
        {
            vector = 0f;
        }
        else
        {
            smoothedMovement.Current = vector.Normal * smoothedMovement.Current.Length;
        }

        smoothedMovement.Target = vector * num;
        smoothedMovement.SmoothTime = ((smoothedMovement.Target.Length < smoothedMovement.Current.Length) ? Controller.DeaccelerationTime : Controller.AccelerationTime);
        smoothedMovement.Update( Time.Delta );
        if ( smoothedMovement.Current.IsNearlyZero( 0.01f ) )
        {
            smoothedMovement.Current = 0f;
        }

        return smoothedMovement.Current;
    }

    public override void UpdateRigidBody( Rigidbody body )
    {
        base.UpdateRigidBody( body );
        body.Gravity = false;
        body.LinearDamping = 5f;
    }
}