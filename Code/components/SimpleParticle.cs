using System;

public class SimpleParticle : Component
{
    public Material Material { get; set; }

    public Rect TextureRect { get; set; } = new Rect( 0, 0, 1, 1 ); // TextureRect defines the portion of the texture to use for this particle.
    public int TextureID { get; set; } = 0; // TextureID is used to identify which texture to use for the particle, if applicable.

    public Vector3 Velocity { get; set; } = Vector3.Zero; // Velocity is the speed and direction of the particle's movement.
    public Vector3 Acceleration { get; set; } = Vector3.Zero; // Acceleration is the change in velocity over time, affecting the particle's movement.
    public float Damping { get; set; } = 0.98f; // Damping is a factor that reduces the particle's velocity over time, simulating friction or air resistance.
    public float Lifetime { get; set; } = 1f; // Lifetime is the duration for which the particle exists before it is removed in seconds
    public float MaxLifetime { get; set; } = 1f; // MaxLifetime is the maximum duration for which the particle can exist, used to reset or recycle particles.

    // If Animated:
    // Frames is the number of frames in the animation, and it plays along the course of the particle's lifetime.
    // TilesWide is the number of tiles in the texture atlas that the animation spans horizontally.
    // TileHeight is the height of each tile in the texture atlas.
    public bool Animated { get; set; } = false;
    public int? Frames { get; set; } = null;
    public int TilesWide { get; set; } = 1;
    public int TileHeight { get; set; } = 1;

    public Curve Scale { get; set; } = new Curve( new Curve.Frame( 0, 10 ), new Curve.Frame( 1, 10 ) ); // Scale defines how the particle's size changes over its lifetime.

    private SceneCustomObject sceneObject;
    protected override void OnStart()
    {
        MaxLifetime = Lifetime; // Initialize MaxLifetime to the initial Lifetime value.
        sceneObject = new SceneCustomObject( Scene.SceneWorld );
        sceneObject.Position = WorldPosition; // Set the initial position of the scene object to the particle's world position.
        sceneObject.Flags.CastShadows = false; // Disable shadow casting for the particle.
        sceneObject.Flags.IsOpaque = true;
        sceneObject.Flags.IsTranslucent = false;


        var col = Color.FromBytes( Random.Shared.Int( 0, 255 ), Random.Shared.Int( 0, 255 ), Random.Shared.Int( 0, 255 ) );
        sceneObject.RenderOverride = ( obj ) =>
        {
            Vector3 Right = Graphics.CameraRotation.Right;
            Vector3 Up = Graphics.CameraRotation.Up;
            float scale = Scale.Evaluate( 1 - Lifetime / MaxLifetime ); // Evaluate the scale based on the remaining lifetime.

            var p = Vector3.Zero;
            var p1 = p - Right * scale / 2f - Up * scale / 2f;
            var p2 = p + Right * scale / 2f - Up * scale / 2f;
            var p3 = p1 + Up * scale;
            var p4 = p2 + Up * scale;

            var v1 = new Sandbox.Vertex( p1, new Vector4( new Vector3( TextureRect.BottomLeft, TextureID ), 0f ), Color.White );
            var v2 = new Sandbox.Vertex( p2, new Vector4( new Vector3( TextureRect.BottomRight, TextureID ), 0f ), Color.White );
            var v3 = new Sandbox.Vertex( p3, new Vector4( new Vector3( TextureRect.TopLeft, TextureID ), 0f ), Color.White );
            var v4 = new Sandbox.Vertex( p4, new Vector4( new Vector3( TextureRect.TopRight, TextureID ), 0f ), Color.White );
            Graphics.Draw( new List<Sandbox.Vertex>() { v1, v2, v3, v4 }, 4, Material, new RenderAttributes(), Graphics.PrimitiveType.TriangleStrip );

        };
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        sceneObject.Delete();
    }
    protected override void OnUpdate()
    {
        Lifetime -= Time.Delta; // Decrease the lifetime by the time since the last update.
        if ( Lifetime <= 0 )
        {
            // If the lifetime is less than or equal to zero, we remove the particle.
            GameObject.Destroy();
            return;
        }

        Velocity += Acceleration * Time.Delta; // Update the velocity based on acceleration.
        Velocity *= Damping; // Apply damping to the velocity.

        // Try to move the particle based on its velocity.
        // We do a basic cast
        var trace = Scene.Trace.Ray( WorldPosition, WorldPosition + Velocity * Time.Delta )
            .Run();
        WorldPosition = trace.EndPosition;
        if ( trace.Hit )
        {
            // Cancel out our velocity along the normal of the hit surface.
            var normal = trace.Normal;
            Velocity -= normal * Vector3.Dot( Velocity, normal );
            WorldPosition += trace.Normal * 0.01f; // Move the particle slightly away from the surface to avoid sticking.
        }

        sceneObject.Transform = WorldTransform; // Update the scene object's transform to match the particle's world position and rotation.
    }
}