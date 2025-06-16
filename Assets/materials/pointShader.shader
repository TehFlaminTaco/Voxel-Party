
HEADER
{
	Description = "";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
	Depth( S_MODE_DEPTH );
	ToolsShadingComplexity( "tools_shading_complexity.shader" );
}

COMMON
{
	#ifndef S_ALPHA_TEST
	#define S_ALPHA_TEST 0
	#endif
	#ifndef S_TRANSLUCENT
	#define S_TRANSLUCENT 0
	#endif
	
	#include "common/shared.hlsl"
	#include "procedural.hlsl"

	#define S_UV2 1
	#define CUSTOM_MATERIAL_INPUTS
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
	float4 vColor : COLOR0 < Semantic( Color ); >;
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
	float3 vPositionOs : TEXCOORD14;
	float3 vNormalOs : TEXCOORD15;
	float4 vTangentUOs_flTangentVSign : TANGENT	< Semantic( TangentU_SignV ); >;
	float4 vColor : COLOR0;
	float4 vTintColor : COLOR1;
	#if ( PROGRAM == VFX_PROGRAM_PS )
		bool vFrontFacing : SV_IsFrontFace;
	#endif
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v )
	{
		
		PixelInput i = ProcessVertex( v );
		i.vPositionOs = v.vPositionOs.xyz;
		i.vColor = v.vColor;
		
		ExtraShaderData_t extraShaderData = GetExtraPerInstanceShaderData( v );
		i.vTintColor = extraShaderData.vTint;
		
		VS_DecodeObjectSpaceNormalAndTangent( v, i.vNormalOs, i.vTangentUOs_flTangentVSign );
		return FinalizeVertex( i );
		
	}
}

PS
{
	#include "common/pixel.hlsl"
	
	SamplerState g_sSampler0 < Filter( POINT ); AddressU( CLAMP ); AddressV( CLAMP ); >;
	CreateInputTexture2D( Abledo, Srgb, 8, "None", "_color", ",0/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) );
	Texture2D g_tAbledo < Channel( RGBA, Box( Abledo ), Srgb ); OutputFormat( DXT5 ); SrgbRead( True ); >;
	
	

	float4 MainPs( PixelInput i ) : SV_Target0
	{

		
		float2 l_0 = i.vTextureCoords.xy * float2( 1, 1 );
		float2 l_1 = l_0 * float2( 16, 16 );
		float2 l_2 = floor( l_1 );
		float2 l_3 = frac( l_1 );
		float2 l_4 = l_3 - float2( 0.1, 0.1 );
		float2 l_5 = l_4 / float2( 0.8, 0.8 );
		float2 l_6 = min( l_5, 0.95 );
		float2 l_7 = max( l_6, 0.05 );
		float2 l_8 = l_2 + l_7;
		float2 l_9 = l_8 / float2( 16, 16 );
		float4 l_10 = Tex2DS( g_tAbledo, g_sSampler0, l_9 );

		if(l_10.r == 1.0 && l_10.g == 0.0 && l_10.b == 1.0)
			discard;
		
		float light = lerp(0.5, 1.0, (dot(i.vNormalOs, normalize(float3(0.1, 0.2, 0.3))) + 1) / 2);
		return float4( l_10.xyz * light, l_10.a ) * i.vTintColor;
	}
}
