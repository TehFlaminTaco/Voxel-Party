
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

	// Hash function (2D to 1D)
	float hash(float2 p) {
		float3 p3  = frac(float3(p.x, p.y, p.x) * 0.1031);
		p3 += dot(p3, p3.yzx + 33.33);
		return frac((p3.x + p3.y) * p3.z);
	}

	// Value noise (interpolated hash)
	float valueNoise(float2 uv) {
		float2 i = floor(uv);
		float2 f = frac(uv);

		// Four corners
		float a = hash(i);
		float b = hash(i + float2(1, 0));
		float c = hash(i + float2(0, 1));
		float d = hash(i + float2(1, 1));

		// Smooth interpolation
		float2 u = f * f * (3.0 - 2.0 * f);

		return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
	}

	// Fractal Octave Noise
	float fractalNoise(float2 uv, int octaves, float lacunarity, float gain) {
		float amplitude = 0.5;
		float frequency = 1.0;
		float sum = 0.0;

		for (int i = 0; i < octaves; i++) {
			sum += amplitude * valueNoise(uv * frequency);
			frequency *= lacunarity;
			amplitude *= gain;
		}

		return sum;
	}

	bool sampleCloud(int2 pos){
		return fractalNoise(float2(pos), 8, 0.9, 2) > 72.0;
	}

	bool traverseVoxelGrid(float3 rayOrigin, int maxHeight, int minHeight, float3 rayDir, bool flipTrace, out float hitDistance, out int3 hitVoxel, out int3 hitNormal)
	{
		int3 voxel = int3(floor(rayOrigin));
		if(sampleCloud(voxel.xy) ^ flipTrace){
			hitVoxel = voxel;
			hitDistance = 0.0f;
			hitNormal = float3(0.0,0.0,sign(rayDir.z));
			return true;
		}
		float3 invDir = 1.0 / rayDir;

		// Step direction (either -1 or 1)
		int3 step = int3(sign(rayDir));

		// Next boundary in world space
		float3 voxelBoundary = (rayDir > 0.0) ? (voxel + 1) : voxel;

		// Distance to first boundary
		float3 tMax = (voxelBoundary - rayOrigin) * invDir;

		// Distance to cross one voxel
		float3 tDelta = abs(invDir);

		hitDistance = 0;

		// Max steps to prevent infinite loop
		[loop]
		for (int i = 0; i < 512; ++i)
		{
			if(voxel.z > maxHeight || voxel.z < minHeight){
				hitVoxel = voxel;
				return flipTrace;
			}
			if (sampleCloud(voxel.xy) ^ flipTrace)
			{
				hitVoxel = voxel;
				return true;
			}
			hitDistance = min(tMax.x, min(tMax.y, tMax.z));

			// Advance in smallest tMax direction
			if (tMax.x < tMax.y)
			{
				if (tMax.x < tMax.z)
				{
					voxel.x += step.x;
					tMax.x += tDelta.x;
					hitNormal = int3(-step.x, 0, 0);
				}
				else
				{
					voxel.z += step.z;
					tMax.z += tDelta.z;
					hitNormal = int3(0, 0, -step.z);
				}
			}
			else
			{
				if (tMax.y < tMax.z)
				{
					voxel.y += step.y;
					tMax.y += tDelta.y;
					hitNormal = int3(0, -step.y, 0);
				}
				else
				{
					voxel.z += step.z;
					tMax.z += tDelta.z;
					hitNormal = int3(0, 0, -step.z);
				}
			}
		}

		return false; // Didn't hit anything
	}
	
	float traceCloud(float3 pos, float3 direction, int minHeight, int maxHeight, out float3 hitNormal){
		int3 hitVoxel;
		float hitDistance;
		return traverseVoxelGrid(pos, maxHeight, minHeight, direction, false, hitDistance, hitVoxel, hitNormal) ? 1.0 : 0.0;
	}

	float blurTrace(float3 pos, float3 direction, int minHeight, int maxHeight, int kSize, float kStep, out float3 hitNormal){
		float3 normalSum = float3(0.0,0.0,0.0);
		float normalContribution = 0.0;

		float3 rightProbably = float3(-direction.y, direction.x, direction.z) * kStep;
		float3 upProbably = float3(-direction.z, direction.y, direction.x) * kStep;
		float totalContribution = 0.0;
		for(int y=-kSize; y<=kSize; y++){
			for(int x=-kSize; x<=kSize; x++){
				float3 norm;
				float hitContribution = traceCloud(pos + (rightProbably*x) + (upProbably*y), direction, minHeight, maxHeight, norm);
				normalContribution += hitContribution;
				totalContribution += 1;
				normalSum += norm * hitContribution;
			}
		}
		hitNormal = normalSum / normalContribution;
		return normalContribution / totalContribution;
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float3 worldPos = (i.vPositionWithOffsetWs.xyz + g_vHighPrecisionLightingOffsetWs.xyz);
		float3 rayDir = normalize((worldPos - g_vCameraPositionWs) * float3(1.0,1.0,2.0));
		worldPos.x += g_flTime * 40.0;

		worldPos /= 400.0;
		float cloudCoverage = 0.0;
		int maxHeight = int(worldPos.z + 1.0);
		int minHeight = int(worldPos.z - 1.0);

		float3 hitNormal;
		float alpha = blurTrace(worldPos, rayDir, minHeight, maxHeight, 0, 0.02, hitNormal);
		Material m = Material::Init();
		m.Albedo = float3(1.0, 1.0, 1.0);
		m.Roughness = 1 ;
		m.Metalness = 0;
		m.TintMask = 1;
		m.Opacity = alpha;
		m.Emission = 0;
		m.Transmission = 0;
		m.AmbientOcclusion = 1;
		m.Normal = hitNormal;

		return ShadingModelStandard::Shade( i, m );
	}
}