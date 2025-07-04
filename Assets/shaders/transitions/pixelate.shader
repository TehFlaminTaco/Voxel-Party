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
        float pixelSize = pow(2.0,(1.0-TransitionProgress) * ceil(log2(g_vViewportSize.x)));
        float2 pixelPoint = (round(i.vPositionSs.xy / pixelSize) * pixelSize) / g_vViewportSize.xy;

        float4 buff = Tex2DS(g_tColorBuffer, g_sSampler0, pixelPoint);
        return float4(buff.xyz * TransitionProgress, 1.0);
    }
}