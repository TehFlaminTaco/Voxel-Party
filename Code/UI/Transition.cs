using System;
using System.Threading.Tasks;
using Sandbox.Rendering;

[Title( "Transition" )]
[Category( "Post Processing" )]
[Icon( "grain" )]
public class Transition : PostProcess, Component.ExecuteInEditor
{
    public bool Outro = false;
    public bool Intro = false;
    public TimeSince Started;
    public TimeUntil EndTransition;

    [Property] public float TransitionTime = 1f;
    [Property] public bool UseOutro = true;
    [Property] public bool UseIntro = true;

    [Property] Shader[] IntroShaders = new Shader[] { };
    [Property] Shader[] OutroShaders = new Shader[] { };
    [Property] Shader[] CommonShaders = new Shader[] { };

    Shader pickedIntroShader;
    Shader pickedOutroShader;

    public void RollOutroShader()
    {
        pickedOutroShader = Random.Shared.FromList( OutroShaders.Concat( CommonShaders ).ToList() );
    }
    public void RollIntroShader()
    {
        pickedIntroShader = Random.Shared.FromList( IntroShaders.Concat( CommonShaders ).ToList() );
    }
    public static Transition Instance = null;
    protected override void OnStart()
    {
        Instance = this;
        RollIntroShader();
        RollOutroShader();
        base.OnStart();
        if ( UseIntro )
        {
            Intro = true;
            Started = -3f;
        }
    }

    public static async Task Run( Func<Task> after, bool replayIntro = false )
    {
        Instance.RollOutroShader();
        Instance.Outro = true;
        Instance.EndTransition = Instance.TransitionTime;
        await GameTask.DelayRealtimeSeconds( Instance.EndTransition );
        await after();
        Instance.Outro = false;
        if ( replayIntro )
        {
            Instance.RollIntroShader();
            Instance.Started = 0f;
            Instance.Intro = true;
            await GameTask.DelayRealtimeSeconds( Instance.TransitionTime );
        }
    }
    [Button]
    public void TestRandom()
    {
        _ = Run( () => { }, true );
    }

    public static async Task Run( Action after, bool replayIntro = false )
    {
        Instance.RollOutroShader();
        Instance.Outro = true;
        Instance.EndTransition = Instance.TransitionTime;
        await GameTask.DelayRealtimeSeconds( Instance.EndTransition );
        after();
        Instance.Outro = false;
        if ( replayIntro )
        {
            Instance.RollIntroShader();
            Instance.Started = 0f;
            Instance.Intro = true;
            await GameTask.DelayRealtimeSeconds( Instance.TransitionTime );
        }
    }

    protected override Stage RenderStage => Stage.AfterUI;
    protected override int RenderOrder => 100;
    protected override void UpdateCommandList()
    {
        base.UpdateCommandList();
        if ( !Intro && !Outro )
            return;
        float t = Intro ? Started / TransitionTime : EndTransition / TransitionTime;
        if ( Intro && Started > TransitionTime )
        {

            Intro = false;
            return;
        }
        t = MathF.Min( MathF.Max( t, 0 ), 1 );
        CommandList.GrabFrameTexture( "ColorBuffer" );
        CommandList.Attributes.Set( "TransitionProgress", t );
        CommandList.Blit( Material.FromShader( Intro ? pickedIntroShader : pickedOutroShader ) );
    }
}