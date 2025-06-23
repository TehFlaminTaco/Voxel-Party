using System;
using Sandbox.Rendering;

public class PointParticleSpriteRenderer : ParticleRenderer, Component.ExecuteInEditor
{

    public enum BillboardAlignment
    {
        //
        // Summary:
        //     Look directly at the camera, apply roll
        [Description( "Look directly at the camera, apply roll" )]
        LookAtCamera,
        //
        // Summary:
        //     Look at the camera but don't pitch up and down, up is always up, can roll
        [Description( "Look at the camera but don't pitch up and down, up is always up, can roll" )]
        RotateToCamera,
        //
        // Summary:
        //     Use rotation provided by the particle, pitch yaw and roll
        [Description( "Use rotation provided by the particle, pitch yaw and roll" )]
        Particle,
        //
        // Summary:
        //     Align to game object rotation, apply pitch yaw and roll
        [Description( "Align to game object rotation, apply pitch yaw and roll" )]
        Object
    }

    public enum ParticleSortMode
    {
        Unsorted,
        ByDistance
    }

    private SceneCustomObject _so;

    [Property]
    public Texture Texture { get; set; }

    [Property]
    [Range( 0f, 1f, 0.01f, true, true )]
    public Vector2 Pivot { get; set; } = 0.5;

    [Property]
    [Range( 0f, 2f, 0.01f, true, true )]
    [DefaultValue( 1f )]
    public float Scale { get; set; } = 1f;

    //
    // Summary:
    //     If opaque there's no need to sort particles, because they will write to the depth
    //     buffer during the opaque pass.
    [Property]
    [Description( "If opaque there's no need to sort particles, because they will write to the depth buffer during the opaque pass." )]
    public bool Opaque { get; set; }

    [Property]
    [ToggleGroup( "FaceVelocity" )]
    public bool FaceVelocity { get; set; }

    [Property]
    [ToggleGroup( "FaceVelocity" )]
    [Range( 0f, 360f, 0.01f, true, true )]
    public float RotationOffset { get; set; }

    [Property]
    [ToggleGroup( "MotionBlur" )]
    public bool MotionBlur { get; set; }

    [Property]
    [ToggleGroup( "MotionBlur" )]
    public bool LeadingTrail { get; set; } = true;

    [Property]
    [ToggleGroup( "MotionBlur" )]
    [Range( 0f, 1f, 0.01f, true, true )]
    public float BlurAmount { get; set; } = 0.5f;

    [Property]
    [ToggleGroup( "MotionBlur" )]
    [Range( 0f, 1f, 0.01f, true, true )]
    public float BlurSpacing { get; set; } = 0.5f;

    [Property]
    [ToggleGroup( "MotionBlur" )]
    [Range( 0f, 1f, 0.01f, true, true )]
    public float BlurOpacity { get; set; } = 0.5f;

    [Property]
    public bool CastShadows { get; set; } = true;

    [Property]
    public bool ReceiveShadows { get; set; } = true;

    //
    // Summary:
    //     Should th
    [Property]
    [DefaultValue( BillboardAlignment.LookAtCamera )]
    [Description( "Should th" )]
    public BillboardAlignment Alignment { get; set; }

    [Property]
    public ParticleSortMode SortMode { get; set; }

    [Property]
    public int SpriteSizeInSheet = 8;
    [Property]
    public int FramesInSheet = 1;

    protected override void OnAwake()
    {
        base.Tags.Add( "particles" );
        base.OnAwake();
    }
    protected override void OnEnabled()
    {
        base.OnEnabled();
        if ( !Application.IsDedicatedServer )
        {
            _so = new SceneCustomObject( base.Scene.SceneWorld );
            _so.RenderingEnabled = false;
            _so.Transform = base.WorldTransform;
            _so.Tags.SetFrom( base.Tags );

            _so.RenderOverride = RenderSprites;
        }
    }

    Material mat;
    private void RenderSprites( SceneObject obj )
    {
        mat ??= Material.Load( "materials/textureatlas.vmat" ).CreateCopy();
        mat.Set( "Abledo", Texture );
        _so.Attributes.Set( "spriteSize", (float)SpriteSizeInSheet );
        _so.Flags.IsOpaque = Opaque;
        _so.Flags.IsTranslucent = !Opaque;
        _so.Flags.CastShadows = CastShadows;

        foreach ( var p in base.ParticleEffect.Particles )
        {
            float timeUsed = p.LifeDelta;
            int sequenceTime = (int)(timeUsed * FramesInSheet);
            var localPoints = new[]{
                new Vector3(0,-0.5f,-0.5f),
                new Vector3(0, 0.5f,-0.5f),
                new Vector3(0,-0.5f, 0.5f),
                new Vector3(0, 0.5f, 0.5f),
                new Vector3(0,-0.5f, 0.5f),
                new Vector3(0, 0.5f,-0.5f),
            };

            var transform = global::Transform.Zero.WithPosition( p.Position )
                .WithRotation( Graphics.CameraRotation * Rotation.FromYaw( 180f ) )
                .WithScale( p.Size * Scale );

            var verts = new[] {
                new Vertex(-WorldPosition + transform.PointToWorld(localPoints[0]), Vector3.Forward, Vector3.Zero, new Vector4(0,1,sequenceTime,0f)),
                new Vertex(-WorldPosition + transform.PointToWorld(localPoints[1]), Vector3.Forward, Vector3.Zero, new Vector4(1,1,sequenceTime,0f)),
                new Vertex(-WorldPosition + transform.PointToWorld(localPoints[2]), Vector3.Forward, Vector3.Zero, new Vector4(0,0,sequenceTime,0f)),
                new Vertex(-WorldPosition + transform.PointToWorld(localPoints[3]), Vector3.Forward, Vector3.Zero, new Vector4(1,0,sequenceTime,0f)),
                new Vertex(-WorldPosition + transform.PointToWorld(localPoints[4]), Vector3.Forward, Vector3.Zero, new Vector4(0,0,sequenceTime,0f)),
                new Vertex(-WorldPosition + transform.PointToWorld(localPoints[5]), Vector3.Forward, Vector3.Zero, new Vector4(1,1,sequenceTime,0f)),
            };
            var attr = new RenderAttributes();
            Graphics.Draw( verts, 6, mat, _so.Attributes );
        }



    }

    protected override void OnDisabled()
    {
        base.OnDisabled();
        mat ??= null;
        _so?.Delete();
        _so = null;
    }
    protected override void OnTagsChanged()
    {
        _so?.Tags.SetFrom( base.Tags );
    }
    protected override void OnUpdate()
    {
        if ( !_so.IsValid() )
        {
            return;
        }

        _so.RenderingEnabled = false;
        ParticleEffect particleEffect = base.ParticleEffect;
        if ( particleEffect.IsValid() && particleEffect.Particles.Count != 0 && !MathF.Abs( Scale * base.WorldScale.x ).AlmostEqual( 0f ) )
        {
            _so.RenderingEnabled = true;
            _so.Transform = global::Transform.Zero.WithPosition( WorldPosition );
            _so.Bounds = particleEffect.ParticleBounds.Grow( 16f + particleEffect.MaxParticleSize * Scale * 2f ).Snap( 16f );
            _so.Flags.IsOpaque = Opaque;
            _so.Flags.IsTranslucent = !Opaque;
        }
    }
}