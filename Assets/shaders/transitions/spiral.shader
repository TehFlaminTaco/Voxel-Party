MODES
{
    Default();
    Forward();
}

FEATURES
{
    
}

COMMON
{
    #include "postprocess/shared.hlsl"
}

struct VertexInput
{
    float3 vPositionOs : POSITION < Semantic( PosXyz ); >;
    float2 vTexCoord : TEXCOORD0 < Semantic( LowPrecisionUv ); >;
};

struct PixelInput
{
    float2 vTexCoord : TEXCOORD0;

	#if ( PROGRAM == VFX_PROGRAM_VS )
		float4 vPositionPs		: SV_Position;
	#endif

	#if ( ( PROGRAM == VFX_PROGRAM_PS ) )
		float4 vPositionSs		: SV_Position;
	#endif
};

VS
{
    PixelInput MainVs( VertexInput i )
    {
        PixelInput o;
        
        o.vPositionPs = float4( i.vPositionOs.xy, 0.0f, 1.0f );
        o.vTexCoord = i.vTexCoord;
        return o;
    }
}

PS
{
    RenderState( DepthWriteEnable, false );
    RenderState( DepthEnable, false );

    // Passed framebuffer if you want to sample it
    Texture2D g_tColorBuffer < Attribute( "ColorBuffer" ); SrgbRead( true ); >;
    float TransitionProgress < Attribute("TransitionProgress"); >;
    SamplerState g_sSampler0 < Filter( POINT ); AddressU( WRAP ); AddressV( WRAP ); >;

    float4 MainPs( PixelInput i ) : SV_Target0
    {
        const float PI = 3.14159;
        const float NUM_TURNS = 10.0;
        float t =  1.0 - TransitionProgress;

        float4 buff = Tex2DS(g_tColorBuffer, g_sSampler0, i.vTexCoord.xy);

        // Normalize coordinates to range (-0.5, 0.5), aspect-corrected
        float2 uv = i.vPositionSs.xy / g_vViewportSize;
        uv -= 0.5;
        uv.y *= g_vViewportSize.y / g_vViewportSize.x;

        // Convert to polar coordinates
        float radius = length(uv);
        float angle = atan2(uv.y, uv.x);
        angle = (angle + PI) / (2.0 * PI); // Normalize angle to [0, 1)

        // Spiral math
        float radiansPerTurn = 0.71 / NUM_TURNS;
        float spiralProgress = t * NUM_TURNS;

        int turnIndex = (int)spiralProgress - 1;
        float turnFraction = frac(spiralProgress);

        // Determine current spiral boundary
        float currentThreshold = (float(turnIndex) + (turnFraction > angle ? 1.0 : 0.0) + angle) * radiansPerTurn;
        float4 pixelValue = radius > currentThreshold ? buff : float4(0,0,0,1);
        return pixelValue;
    }
}