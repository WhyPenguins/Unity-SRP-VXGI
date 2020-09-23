#ifndef VXGI_SHADERLIBRARY_RADIANCES_PIXEL
#define VXGI_SHADERLIBRARY_RADIANCES_PIXEL

#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Variables.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Utilities.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Visibility.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/BRDFs/General.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Sampler.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Structs/LightingData.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Raytracing.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Noise.cginc"

void DirectPixelRadiance(LightingData data, out float3 diffuse, out float3 specular)
{
  float level = MinSampleLevel(data.voxelPosition);
  float voxelSize = VoxelSize(level);
  diffuse = 0.0;
  specular = 0.0;

  for (uint i = 0; i < LightCount; i++) {
    LightSource lightSource = LightSources[i];

    bool notInRange;
    float3 localPosition;

    [branch]
    if (lightSource.type == LIGHT_SOURCE_TYPE_DIRECTIONAL) {
      localPosition = -lightSource.direction;
      notInRange = false;
      lightSource.voxelPosition = mad(1.732, localPosition, data.voxelPosition);
    } else {
      localPosition = lightSource.worldposition - data.worldPosition;
      notInRange = lightSource.NotInRange(localPosition);
    }

    data.Prepare(normalize(localPosition));

    float spotFalloff = lightSource.SpotFalloff(-data.vecL);

    if (notInRange || (spotFalloff <= 0.0) || (data.NdotL <= 0.0)) continue;

    float influence = //VoxelVisibility(mad(2.0 * voxelSize, data.vecN, data.voxelPosition), lightSource.voxelPosition) * 
      /*GeneralBRDF(data)
      **/ data.NdotL
      * spotFalloff
      * lightSource.Attenuation(localPosition);
    if (influence > 0)
    {
      float shadow = 0;
      float shadowSamples = lightSource.radius.y;
      if (shadowSamples > 0)
      {
        for (float s = 0; s < shadowSamples; s++)
        {
          float3 samplePos = SphereSample(lightSource.type == LIGHT_SOURCE_TYPE_DIRECTIONAL ? data.worldPosition + normalize(data.vecL) * 5 : lightSource.worldposition, lightSource.radius.x, hash3(data.screenPosition * 293.2983 + s * 3.4492 + NoiseNum));
          float3 castDir = normalize(samplePos - data.worldPosition);
          float3 samplePosVoxel = WorldSpaceToNormalizedVoxelSpace(samplePos);
          raycastResult raycast = VoxelRaycastBias(data.worldPosition, castDir, normalize(data.vecN), 60, lightSource.type == LIGHT_SOURCE_TYPE_DIRECTIONAL ? 0 : (distance(data.voxelPosition, samplePosVoxel) * BinaryResolution));
          shadow += (raycast.distlimit || raycast.sky) ? 0 : 1;
        }

        shadow = shadow / (float)shadowSamples;
      }

      influence *= 1.0 - shadow;

      diffuse += DiffuseBRDF(data) * influence * lightSource.color;
      specular += SpecularBRDF(data) * influence * lightSource.color;
    }
  }

}

float3 IndirectSpecularPixelRadiance(LightingData data)
{
  return 0.0;
}
inline half3 FresnelLerp(half3 F0, half3 F90, half cosA)
{
  half t = Pow5(1 - cosA);   // ala Schlick interpoliation
  return lerp(F0, F90, t);
}
void IndirectPixelRadiance(LightingData data, bool newArea, out float samplesOutput, out float3 diffuse, out float3 specular, out float3 specularHitPosAvg)
{
  float sampleCountSqrt = newArea ? PerPixelGIRayCountsSqrt.z : PerPixelGIRayCountsSqrt.y;
  samplesOutput = newArea ? PerPixelGIRayCounts.z : PerPixelGIRayCounts.y;
  if (TextureSDF(data.voxelPosition) < 0.0) { diffuse = 0; specular = 0; return; }

  half grazingTerm = saturate((1-data.roughness) + (data.specularColor.r));
  float specularChance = FresnelLerp(data.specularColor, grazingTerm, data.NdotV);

  /*diffuse = StratifiedHemisphereSample(data.worldPosition, normalize(data.vecN), 25, (uint)sampleCountSqrt, hash(float3(data.screenPosition, NoiseNum)));
  diffuse *= 1 - specularChance;
  specular = StratifiedSpecularSample(data.roughness, normalize(data.vecR), data.worldPosition, normalize(data.vecN), 70, (uint)sampleCountSqrt, hash(float3(data.screenPosition, NoiseNum)));
  specular *= specularChance;*/

  StratifiedHemisphereSpecularSample(specularChance, data.roughness, normalize(data.vecR), data.worldPosition, normalize(data.vecN), 25, 70, (uint)sampleCountSqrt, hash(float3(data.screenPosition, NoiseNum)), diffuse, specular, specularHitPosAvg);
}
#endif // VXGI_SHADERLIBRARY_RADIANCES_PIXEL
