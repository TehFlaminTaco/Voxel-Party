
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
	#define S_TRANSLUCENT 1
	#endif
	
	#include "common/shared.hlsl"
	#include "procedural.hlsl"

	#define S_UV2 1
	#define CUSTOM_MATERIAL_INPUTS
}

struct VertexInput
{
	// Common Vertex Shader Attributes

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
// Geometric
//-------------------------------------------------------------------------------------------------------------------------------------------------------------
float3 vPositionOs : POSITION < Semantic( PosXyz ); >;
float4 vTexCoord : TEXCOORD0 < Semantic( Uvwx ); >;
float2 vTexCoord2 : TEXCOORD1 < Semantic( LowPrecisionUv1 ); >;	
float4 vNormalOs : NORMAL < Semantic( OptionallyCompressedTangentFrame ); >;	

#ifdef VS_INPUT_HAS_TANGENT_BASIS
float4 vTangentUOs_flTangentVSign : TANGENT	< Semantic( TangentU_SignV ); >;
#endif

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
// Skinning
//-------------------------------------------------------------------------------------------------------------------------------------------------------------
#if ( D_SKINNING > 0 )
	uint4 vBlendIndices : BLENDINDICES 	< Semantic( BlendIndices ); >;
	float4 vBlendWeight : BLENDWEIGHT 	< Semantic( BlendWeight ); >;
#endif	
	
//-------------------------------------------------------------------------------------------------------------------------------------------------------------
// SSS Curvature
//-------------------------------------------------------------------------------------------------------------------------------------------------------------
#if ( S_USE_PER_VERTEX_CURVATURE )
	float flSSSCurvature : TEXCOORD2 < Semantic( Curvature ); >;
#endif

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
// Morph
//-------------------------------------------------------------------------------------------------------------------------------------------------------------
#if ( D_MORPH ) || ( D_CS_VERTEX_ANIMATION )
	float nVertexIndex : TEXCOORD14 < Semantic( MorphIndex ); >;
	float nVertexCacheIndex : TEXCOORD15 < Semantic( MorphIndex ); >;
#endif

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
// Instancing data
//-------------------------------------------------------------------------------------------------------------------------------------------------------------
uint nInstanceTransformID : TEXCOORD13 < Semantic( InstanceTransformUv ); >;

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
// Baked lighting
//-------------------------------------------------------------------------------------------------------------------------------------------------------------
#if ( D_BAKED_LIGHTING_FROM_LIGHTMAP )	
	float2 vLightmapUV : TEXCOORD3 < Semantic( LightmapUV ); > ;
#endif

//-------------------------------------------------------------------------------------------------------------------------------------------------------------

#ifndef COMMON_VS_INPUT_DEFINED
#define COMMON_VS_INPUT_DEFINED
#endif

#ifndef SHARED_STANDARD_VS_INPUT_DEFINED
#define SHARED_STANDARD_VS_INPUT_DEFINED
#endif
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
		i.vTextureCoords = v.vTexCoord;
		
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
		float3 l_0 = i.vTextureCoords.xyz;
		l_0.xy = clamp(l_0.xy, 0.05, 0.95);
		int index = int(l_0.z+0.1);
		float2 texSize;
        g_tAbledo.GetDimensions(texSize.x, texSize.y);
		int tilesWide = int(texSize.x / 16);
		int tileX = index % tilesWide;
		int tileY = index / tilesWide;
		l_0.x = (l_0.x + tileX) / tilesWide;
		l_0.y = (l_0.y + tileY) / tilesWide;

		float4 l_10 = Tex2DS( g_tAbledo, g_sSampler0, l_0.xy );

		if(l_10.r == 1.0 && l_10.g == 0.0 && l_10.b == 1.0)
			discard;
		
		//float light = lerp(0.5, 1.0, (dot(i.vNormalOs, normalize(float3(0.1, 0.2, 0.3))) + 1) / 2);
		Material m = Material::Init();
		m.Albedo = l_10.xyz;
		m.Roughness = 1;
		m.Metalness = 0;
		m.TintMask = 1;
		m.Opacity = l_10.a;
		m.Emission = 0;
		m.Transmission = 0;

		return ShadingModelStandard::Shade( i, m );
	}
}