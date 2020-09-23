Shader "Hidden/VXGI/Lighting"
{
  Properties
  {
    _MainTex("Screen", 2D) = "white" {}
  }

  HLSLINCLUDE
    #pragma multi_compile _ VXGI_AMBIENTCOLOR
    #pragma multi_compile _ VXGI_TEMPORAL_DIFFUSE
    #pragma multi_compile _ VXGI_TEMPORAL_SPECULAR
    #pragma multi_compile _ VXGI_TEMPORAL_SEPARATE
    #include "UnityCG.cginc"
    #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/BlitSupport.hlsl"
    #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Pixel.cginc"

    float4x4 ClipToVoxel;
    float4x4 ClipToWorld;
    float4x4 ClipToWorldPrev;
    Texture2D<float> _CameraDepthTexture;
    Texture2D<float> _CameraDepthTexture_LastFrame;
    Texture2D<float3> _CameraGBufferTexture0;
    Texture2D<float4> _CameraGBufferTexture1;
    Texture2D<float3> _CameraGBufferTexture2;
    Texture2D<float3> _CameraGBufferTexture3;

    Texture2D _previousLighting;
    Texture2D _previousDiffuseLighting;
    Texture2D _previousSpecularLighting;

    float4x4 _lastFrameViewProj;



    LightingData ConstructLightingData(float4x4 ClipToWorld2, float2 uv, float depth)
    {
      LightingData data;

      float4 worldPosition = mul(ClipToWorld2, float4(mad(2.0, uv, -1.0), DEPTH_TO_CLIP_Z(depth), 1.0));
      data.worldPosition = worldPosition.xyz / worldPosition.w;
      data.screenPosition = uv;

      float3 gBuffer0 = _CameraGBufferTexture0.Sample(point_clamp_sampler, uv);
      float4 gBuffer1 = _CameraGBufferTexture1.Sample(point_clamp_sampler, uv);
      float3 gBuffer2 = _CameraGBufferTexture2.Sample(point_clamp_sampler, uv);

      data.diffuseColor = gBuffer0;
      data.specularColor = gBuffer1.rgb;
      data.glossiness = gBuffer1.a;

      data.vecN = normalize(mad(gBuffer2, 2.0, -1.0));
      data.vecV = normalize(_WorldSpaceCameraPos - data.worldPosition);

      data.Initialize();

      return data;
    }


    struct PotentialOutput
    {
      float4 combinedFeedback;
      float4 diffuseFeedback;
      float4 specularFeedback;
      float3 combinedSum;
      float3 diffuseSum;
      float3 specularSum;
    };
    struct output
    {
      #ifdef VXGI_TEMPORAL_SEPARATE
        float3 diffuseSum : SV_TARGET0;
        float3 specularSum : SV_TARGET1;
        #if defined(VXGI_TEMPORAL_DIFFUSE) && defined(VXGI_TEMPORAL_SPECULAR)
          float4 diffuseFeedback : SV_TARGET2;
          float4 specularFeedback : SV_TARGET3;
        #endif
        #if defined(VXGI_TEMPORAL_DIFFUSE) && !defined(VXGI_TEMPORAL_SPECULAR)
          float4 diffuseFeedback : SV_TARGET2;
        #endif
        #if !defined(VXGI_TEMPORAL_DIFFUSE) && defined(VXGI_TEMPORAL_SPECULAR)
          float4 specularFeedback : SV_TARGET2;
        #endif
      #else
        float3 combinedSum : SV_TARGET0;
        #if defined(VXGI_TEMPORAL_DIFFUSE)
          float4 combinedFeedback : SV_TARGET1;
        #endif
      #endif
    };
    output MakeOutput(PotentialOutput po)
    {
      output o;
      #ifdef VXGI_TEMPORAL_SEPARATE
        o.diffuseSum = po.diffuseSum;
        o.specularSum = po.specularSum;
        #if defined(VXGI_TEMPORAL_DIFFUSE)
          o.diffuseFeedback = po.diffuseFeedback;
        #endif
        #if defined(VXGI_TEMPORAL_SPECULAR)
          o.specularFeedback = po.specularFeedback;
        #endif
      #else
        o.combinedSum = po.combinedSum;
        #if defined(VXGI_TEMPORAL_DIFFUSE)
          o.combinedFeedback = po.combinedFeedback;
        #endif
      #endif
      return o;
    }
    PotentialOutput DefaultPO()
    {
      PotentialOutput po;
      po.combinedFeedback = float4(0, 0, 0, 0);
      po.diffuseFeedback = float4(0, 0, 0, 0);
      po.specularFeedback = float4(0, 0, 0, 0);
      po.combinedSum = float3(0, 0, 0);
      po.diffuseSum = float3(0, 0, 0);
      po.specularSum = float3(0, 0, 0);
      return po;
    }

#if UNITY_UV_STARTS_AT_TOP
  #define UV_FLIP i.uv.y = 1.0 - i.uv.y;
#else
  #define UV_FLIP
#endif
    struct TSSInfo
    {
      float3 lighting;
      float samples;
      float2 uv;
      float3 worldPosition;
    };
    TSSInfo GetOldLighting(Texture2D _previousLighting, float3 worldPosition)
    {
      float4 previousProj = mul(_lastFrameViewProj, float4(worldPosition, 1));
      float2 previousUV = previousProj.xy / previousProj.w;
      previousUV = previousUV * 0.5 + 0.5;
      float4 prevLighting = _previousLighting.Sample(point_clamp_sampler, float2(previousUV.x, previousUV.y));

      TSSInfo info;
      info.lighting = prevLighting.rgb;
      info.samples = prevLighting.w;
      info.uv = previousUV;
      info.worldPosition = worldPosition;

        if (dot(float3(1, 1, 1), previousUV - saturate(previousUV)) != 0)info.samples = 0;
        if (info.samples > 0)
        {
          float depth2 = _CameraDepthTexture_LastFrame.Sample(point_clamp_sampler, previousUV).r;
          if (Linear01Depth(depth2) >= 1.0) {
            info.samples = 0;
          }
          else
          {
            LightingData reprojdata = ConstructLightingData(ClipToWorldPrev, previousUV, depth2);
            if (distance(reprojdata.worldPosition, info.worldPosition) > 0.1)//Should also check against normal, but that's just another pain to keep track of.
            {
              info.samples = 0;
            }
          }
        }

      return info;
    }



    TSSInfo GetOldLighting(Texture2D _previousLighting, float3 samplePosition, float3 worldPosition)
    {
      float4 previousProj = mul(_lastFrameViewProj, float4(worldPosition, 1));
      float2 previousUV = previousProj.xy / previousProj.w;
      previousUV = previousUV * 0.5 + 0.5;
      float4 prevLighting = _previousLighting.Sample(point_clamp_sampler, float2(previousUV.x, previousUV.y));

      TSSInfo info;
      info.lighting = prevLighting.rgb;
      info.samples = prevLighting.w;
      info.uv = previousUV;
      info.worldPosition = worldPosition;

      float4 previousSampleProj = mul(_lastFrameViewProj, float4(samplePosition, 1));
      float2 previousSampleUV = previousSampleProj.xy / previousSampleProj.w;
      previousSampleUV = previousSampleUV * 0.5 + 0.5;

        if (dot(float3(1, 1, 1), previousSampleUV - saturate(previousSampleUV)) != 0)info.samples = 0;
        if (info.samples > 0)
        {
          float depth2 = _CameraDepthTexture_LastFrame.Sample(point_clamp_sampler, previousSampleUV).r;
          if (Linear01Depth(depth2) >= 1.0) {
            info.samples = 0;
          }
          else
          {
            LightingData reprojdata = ConstructLightingData(ClipToWorldPrev, previousSampleUV, depth2);
            if (distance(reprojdata.worldPosition, samplePosition) > 0.1)//Should also check against normal, but that's just another pain to keep track of.
            {
              info.samples = 0;
            }
          }
        }

      return info;
    }








    TSSInfo TemporallyAccumulate(TSSInfo old, TSSInfo cur, float sampleLimit)
    {
      TSSInfo info;
      //The max(...,0) shouldn't be neccessary (it implies a budget higher than target), but just in case for now.
      info.samples = min(old.samples, max(sampleLimit - cur.samples, 0));

      float factor = 1.0 / max(1,(info.samples + cur.samples));

      info.worldPosition = cur.worldPosition;
      info.uv = cur.uv;

      float2 weights = float2(info.samples, cur.samples) * factor;
      info.lighting = (old.lighting.xyz * weights.x + cur.lighting * weights.y);

      info.samples += cur.samples;

      return info;
    }
    TSSInfo MakeTSSInfo(float3 worldPosition, float samples, float2 uv, float3 lighting)
    {
      TSSInfo cur;
      cur.worldPosition = worldPosition;
      cur.samples = samples;
      cur.uv = uv;
      cur.lighting = lighting;

      return cur;
    }
  ENDHLSL

  SubShader
  {
    Blend One One
    ZWrite Off

    Pass
    {
      Name "Combine"

      HLSLPROGRAM
      #pragma vertex BlitVertex
      #pragma fragment frag
      #pragma multi_compile _ UNITY_HDR_ON
      
      Texture2D LightingBuffer;
      Texture2D DiffuseBuffer;
      Texture2D SpecularBuffer;

      output frag(BlitInput i)
      {
        UV_FLIP

        PotentialOutput o = DefaultPO();
        float depth = _CameraDepthTexture.Sample(point_clamp_sampler, i.uv).r;

        if (Linear01Depth(depth) >= 1.0) return MakeOutput(o);

        float3 emissiveColor = _CameraGBufferTexture3.Sample(point_clamp_sampler, i.uv);
        float3 diffuse = DiffuseBuffer.Sample(point_clamp_sampler, i.uv).rgb;
        diffuse *= _CameraGBufferTexture0.Sample(point_clamp_sampler, i.uv);

        float3 specular = SpecularBuffer.Sample(point_clamp_sampler, i.uv).rgb;

#ifndef UNITY_HDR_ON
        // Decode value provided by built-in Unity g-buffer generator
        emissiveColor = -log2(emissiveColor);
#endif
        o.combinedSum = emissiveColor + diffuse + specular;
        return MakeOutput(o);
      }
      ENDHLSL
    }

    Pass
    {
      Name "DirectDiffuseSpecular"

      HLSLPROGRAM
      #pragma vertex BlitVertex
      #pragma fragment frag
      #pragma multi_compile _ VXGI_ANISOTROPIC_VOXEL
      #pragma multi_compile _ VXGI_CASCADES

      output frag(BlitInput i)
      {
        UV_FLIP
        PotentialOutput o = DefaultPO();
        float depth = _CameraDepthTexture.Sample(point_clamp_sampler, i.uv).r;

        if (Linear01Depth(depth) >= 1.0) return MakeOutput(o);

        LightingData data = ConstructLightingData(ClipToWorld, i.uv, depth);
        TSSInfo old = GetOldLighting(_previousDiffuseLighting, data.worldPosition);

        float3 specular;
        float3 diffuse;
        DirectPixelRadiance(data, diffuse, specular);



        TSSInfo cur = MakeTSSInfo(data.worldPosition, max(1, PerPixelShadowRayCounts.y), i.uv, diffuse);

        TSSInfo accum = TemporallyAccumulate(old, cur, PerPixelShadowRayCounts.x);
        o.diffuseFeedback = float4(accum.lighting, accum.samples);
        o.diffuseSum = o.diffuseFeedback.rgb;
        o.specularFeedback = float4(specular, 1);
        o.specularSum = specular;
        o.combinedFeedback = o.diffuseFeedback;
        o.combinedSum = o.diffuseSum;
        return MakeOutput(o);
      }
      ENDHLSL
    }

    Pass
    {
      Name "IndirectDiffuse"

      HLSLPROGRAM
      #pragma vertex BlitVertex
      #pragma fragment frag
      #pragma multi_compile _ VXGI_ANISOTROPIC_VOXEL
      #pragma multi_compile _ VXGI_CASCADES
      #pragma multi_compile _ VXGI_TEMPORAL_EXPERIMENTALSPECULAR

      float IndirectDiffuseModifier;
      float IndirectSpecularModifier;


      output frag(BlitInput i)
      {
        UV_FLIP
        PotentialOutput o = DefaultPO();
        float depth = _CameraDepthTexture.Sample(point_clamp_sampler, i.uv).r;

        if (Linear01Depth(depth) >= 1.0) return MakeOutput(o);

        LightingData data = ConstructLightingData(ClipToWorld, i.uv, depth);

        TSSInfo old = GetOldLighting(_previousDiffuseLighting, data.worldPosition);

        float samplesOutput = 0;
        float3 specular;
        float3 diffuse;
        float3 specularHitPosAvg;
        IndirectPixelRadiance(data, old.samples < PerPixelGIRayCounts.x, samplesOutput, diffuse, specular, specularHitPosAvg);


        TSSInfo cur = MakeTSSInfo(data.worldPosition, samplesOutput, i.uv, diffuse);
        TSSInfo accum = TemporallyAccumulate(old, cur, PerPixelGIRayCounts.x);

#ifdef VXGI_TEMPORAL_EXPERIMENTALSPECULAR
        specularHitPosAvg = data.worldPosition - data.vecV * distance(specularHitPosAvg, data.worldPosition);
        TSSInfo oldSpec = GetOldLighting(_previousSpecularLighting, data.worldPosition, specularHitPosAvg);
#else
        TSSInfo oldSpec = GetOldLighting(_previousSpecularLighting, data.worldPosition);
#endif
        TSSInfo curSpec = MakeTSSInfo(data.worldPosition, samplesOutput, i.uv, specular);
        TSSInfo accumSpec = TemporallyAccumulate(oldSpec, curSpec, PerPixelGIRayCounts.x);

        float4 previousProj = mul(_lastFrameViewProj, float4(specularHitPosAvg, 1));
        float2 previousUV = previousProj.xy / previousProj.w;
        previousUV = previousUV * 0.5 + 0.5;

        o.diffuseFeedback = float4(accum.lighting, accum.samples);
        o.diffuseSum = IndirectDiffuseModifier * o.diffuseFeedback.rgb;
        o.specularFeedback = float4(accumSpec.lighting, accumSpec.samples);
        o.specularSum = IndirectSpecularModifier * o.specularFeedback.rgb;
        o.combinedFeedback = o.diffuseFeedback;
        o.combinedSum = o.diffuseSum;

        return MakeOutput(o);
      }
      ENDHLSL
    }


      Blend Off
      ZWrite Off

      Pass
      {
        Name "Spatial"

        HLSLPROGRAM
        #pragma vertex BlitVertex
        #pragma fragment frag
        #pragma multi_compile _ UNITY_HDR_ON

        Texture2D DiffuseBuffer;
        float stepwidth = 1;

        output frag(BlitInput i)
        {
          UV_FLIP
          PotentialOutput o = DefaultPO();

          float depth = _CameraDepthTexture.Sample(point_clamp_sampler, i.uv).r;

          if (Linear01Depth(depth) >= 1.0) return MakeOutput(o);

          LightingData data = ConstructLightingData(ClipToWorld, i.uv, depth);



          float3 lighting = DiffuseBuffer.Sample(point_clamp_sampler, i.uv).rgb;


          float invstepwidth = 1.0 / (stepwidth * stepwidth);

          float2 weightsPosNor = float2(-1, -1) / float2(0.01, 0.001);

          float2 offset[] = { float2(-1.0, -1.0), float2(0.0, -1.0), float2(1.0, -1.0),
            float2(-1.0, -0.0), float2(1.0, -0.0),
            float2(-1.0, 1.0), float2(0.0, 1.0), float2(1.0, 1.0) };

          float kernel[] = { 0.25 * 0.25, 0.375 * 0.25, 0.25 * 0.25,
            0.25 * 0.375, 0.25 * 0.375,
            0.25 * 0.25, 0.375 * 0.25, 0.25 * 0.25
          };

          float3 lightingsum = lighting * 0.375 * 0.375;
          float totalWeight = 0.375 * 0.375;
          float2 step = float2(1,1) / _ScreenParams.xy;

          float2 iuv = i.uv;
          [unroll]
          for (int i = 0; i < 8; i++) {
            float2 uv = iuv + offset[i] * step * stepwidth;

            float sampledepth = _CameraDepthTexture.Sample(point_clamp_sampler, uv).r;
            if (Linear01Depth(sampledepth) >= 1.0) continue;
            LightingData sampledata = ConstructLightingData(ClipToWorld, uv, sampledepth);

            float2 distPosNor;

            float3 diff = data.vecN - sampledata.vecN;
            distPosNor.x = dot(diff, diff) * invstepwidth;

            diff = data.worldPosition - sampledata.worldPosition;
            distPosNor.y = dot(diff, diff);

            float2 sampleweights = saturate(exp(distPosNor * weightsPosNor));

            float weight = sampleweights.x * sampleweights.y * kernel[i];
            lightingsum += DiffuseBuffer.Sample(point_clamp_sampler, uv).rgb * weight;
            totalWeight += weight;
          }

          o.combinedSum = lightingsum.xyz / totalWeight;

          return MakeOutput(o);
        }
        ENDHLSL
      }
  }

  Fallback Off
}
