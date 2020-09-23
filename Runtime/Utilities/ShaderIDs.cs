using System.Collections.ObjectModel;
using UnityEngine;

internal static class ShaderIDs {
  internal static readonly Collection<int> Radiance = new Collection<int>(new [] {
    Shader.PropertyToID("Radiance0"),
    Shader.PropertyToID("Radiance1"),
    Shader.PropertyToID("Radiance2"),
    Shader.PropertyToID("Radiance3"),
    Shader.PropertyToID("Radiance4"),
    Shader.PropertyToID("Radiance5"),
    Shader.PropertyToID("Radiance6"),
    Shader.PropertyToID("Radiance7"),
    Shader.PropertyToID("Radiance8"),
    Shader.PropertyToID("Radiance9"),
  });
  internal static readonly int _CameraDepthNormalsTexture = Shader.PropertyToID("_CameraDepthNormalsTexture");
  internal static readonly int _CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");
  internal static readonly int _CameraGBufferTexture0 = Shader.PropertyToID("_CameraGBufferTexture0");
  internal static readonly int _CameraGBufferTexture1 = Shader.PropertyToID("_CameraGBufferTexture1");
  internal static readonly int _CameraGBufferTexture2 = Shader.PropertyToID("_CameraGBufferTexture2");
  internal static readonly int _CameraGBufferTexture3 = Shader.PropertyToID("_CameraGBufferTexture3");
  internal static readonly int Arguments = Shader.PropertyToID("Arguments");
  internal static readonly int BlitViewport = Shader.PropertyToID("BlitViewport");
  internal static readonly int ClipToVoxel = Shader.PropertyToID("ClipToVoxel");
  internal static readonly int ClipToWorld = Shader.PropertyToID("ClipToWorld");
  internal static readonly int Displacement = Shader.PropertyToID("Displacement");
  internal static readonly int Dummy = Shader.PropertyToID("Dummy");
  internal static readonly int FrameBuffer = Shader.PropertyToID("FrameBuffer");
  internal static readonly int DiffuseBuffer = Shader.PropertyToID("DiffuseBuffer");
  internal static readonly int SpecularBuffer = Shader.PropertyToID("SpecularBuffer");
  internal static readonly int LightingBufferSwap = Shader.PropertyToID("LightingBufferSwap");
  internal static readonly int stepwidth = Shader.PropertyToID("stepwidth");
  internal static readonly int IndirectDiffuseModifier = Shader.PropertyToID("IndirectDiffuseModifier");
  internal static readonly int IndirectSpecularModifier = Shader.PropertyToID("IndirectSpecularModifier");
  internal static readonly int LightCount = Shader.PropertyToID("LightCount");
  internal static readonly int LightSources = Shader.PropertyToID("LightSources");
  internal static readonly int LowResColor = Shader.PropertyToID("LowResColor");
  internal static readonly int LowResDiffuse = Shader.PropertyToID("LowResDiffuse");
  internal static readonly int LowResSpecular = Shader.PropertyToID("LowResSpecular");
  internal static readonly int LowResDepth = Shader.PropertyToID("LowResDepth");
  internal static readonly int MipmapLevel = Shader.PropertyToID("MipmapLevel");
  internal static readonly int NumThreads = Shader.PropertyToID("NumThreads");
  internal static readonly int FragmentPointers = Shader.PropertyToID("FragmentPointers");
  internal static readonly int RayTracingStep = Shader.PropertyToID("RayTracingStep");
  internal static readonly int Resolution = Shader.PropertyToID("Resolution");
  internal static readonly int BinaryResolution = Shader.PropertyToID("BinaryResolution");
  internal static readonly int StepMapResolution = Shader.PropertyToID("StepMapResolution");
  internal static readonly int BinaryVoxelSize = Shader.PropertyToID("BinaryVoxelSize");
  internal static readonly int Source = Shader.PropertyToID("Source");
  internal static readonly int Target = Shader.PropertyToID("Target");
  internal static readonly int TargetDownscale = Shader.PropertyToID("TargetDownscale");
  internal static readonly int StepMap = Shader.PropertyToID("StepMap");
  internal static readonly int StepMapFine2x2x2Encode = Shader.PropertyToID("StepMapFine2x2x2Encode");
  internal static readonly int Binary = Shader.PropertyToID("Binary");
  internal static readonly int VoxelBuffer = Shader.PropertyToID("VoxelBuffer");
  internal static readonly int VoxelToWorld = Shader.PropertyToID("VoxelToWorld");
  internal static readonly int VXGI_CascadeIndex = Shader.PropertyToID("VXGI_CascadeIndex");
  internal static readonly int VXGI_CascadesCount = Shader.PropertyToID("VXGI_CascadesCount");
  internal static readonly int VXGI_VolumeCenter = Shader.PropertyToID("VXGI_VolumeCenter");
  internal static readonly int VXGI_VolumeExtent = Shader.PropertyToID("VXGI_VolumeExtent");
  internal static readonly int VXGI_VolumeSize = Shader.PropertyToID("VXGI_VolumeSize");
  internal static readonly int VXGI_VoxelFragmentsCountBuffer = Shader.PropertyToID("VXGI_VoxelFragmentsCountBuffer");
  internal static readonly int WorldToVoxel = Shader.PropertyToID("WorldToVoxel");
  internal static readonly int PerPixelGIRayCounts = Shader.PropertyToID("PerPixelGIRayCounts");
  internal static readonly int PerPixelShadowRayCounts = Shader.PropertyToID("PerPixelShadowRayCounts");
  internal static readonly int PerVoxelGIRayCount = Shader.PropertyToID("PerVoxelGIRayCount");
  //Useful for stratified sampling
  internal static readonly int PerPixelGIRayCountsSqrt = Shader.PropertyToID("PerPixelGIRayCountsSqrt");
  internal static readonly int PerVoxelGIRayCountSqrt = Shader.PropertyToID("PerVoxelGIRayCountSqrt");
  internal static readonly int SkyColor = Shader.PropertyToID("SkyColor");
  internal static readonly int NoiseNum = Shader.PropertyToID("NoiseNum");
  internal static readonly int _previousLighting = Shader.PropertyToID("_previousLighting");
  internal static readonly int _lastFrameViewProj = Shader.PropertyToID("_lastFrameViewProj");
  internal static readonly int ClipToWorldPrev = Shader.PropertyToID("ClipToWorldPrev");
  internal static readonly int _CameraDepthTexture_LastFrame = Shader.PropertyToID("_CameraDepthTexture_LastFrame");
  internal static readonly int NoiseRGB16bit = Shader.PropertyToID("NoiseRGB16bit");
  internal static readonly int SkyProbe = Shader.PropertyToID("SkyProbe");
  internal static readonly int SkyProbeHDRInfo = Shader.PropertyToID("SkyProbeHDRInfo");
  internal static readonly int _previousDiffuseLighting = Shader.PropertyToID("_previousDiffuseLighting");
  internal static readonly int _previousSpecularLighting = Shader.PropertyToID("_previousSpecularLighting");
  internal static readonly int bounceAttenuation = Shader.PropertyToID("bounceAttenuation");
}
