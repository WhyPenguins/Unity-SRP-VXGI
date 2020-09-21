Shader "Hidden/VXGI/Lighting"
{
  Properties
  {
    _MainTex("Screen", 2D) = "white" {}
  }

  HLSLINCLUDE
    #pragma multi_compile _ VXGI_AMBIENTCOLOR
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

      data.vecN = mad(gBuffer2, 2.0, -1.0);
      data.vecV = normalize(_WorldSpaceCameraPos - data.worldPosition);

      data.Initialize();

      return data;
    }



    struct output
    {
      float4 lighting : SV_TARGET0;
      float3 combined : SV_TARGET1;
    };
    output DefaultO()
    {
      output o;
      o.lighting = float4(0,0,0,0);
      o.combined = float3(0,0,0);
      return o;
    }

    struct TSSInfo
    {
      float3 lighting;
      float samples;
      float2 uv;
      float3 worldPosition;
    };
    TSSInfo GetOldLighting(float3 worldPosition)
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

      output frag(BlitInput i) : SV_TARGET
      {
        output o = DefaultO();
        float depth = _CameraDepthTexture.Sample(point_clamp_sampler, i.uv).r;

        if (Linear01Depth(depth) >= 1.0) return o;

        float3 emissiveColor = _CameraGBufferTexture3.Sample(point_clamp_sampler, i.uv);
        float3 lighting = LightingBuffer.Sample(point_clamp_sampler, i.uv).rgb;
        lighting *= _CameraGBufferTexture0.Sample(point_clamp_sampler, i.uv);

#ifndef UNITY_HDR_ON
        // Decode value provided by built-in Unity g-buffer generator
        emissiveColor = -log2(emissiveColor);
#endif
        o.combined = emissiveColor + lighting;
        return o;
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
        output o = DefaultO();
        float depth = _CameraDepthTexture.Sample(point_clamp_sampler, i.uv).r;

        if (Linear01Depth(depth) >= 1.0) return o;

        LightingData data = ConstructLightingData(ClipToWorld, i.uv, depth);
        TSSInfo old = GetOldLighting(data.worldPosition);

        float3 lighting = DirectPixelRadiance(data);



        TSSInfo cur = MakeTSSInfo(data.worldPosition, PerPixelShadowRayCounts.y, i.uv, lighting);

        TSSInfo accum = TemporallyAccumulate(old, cur, PerPixelShadowRayCounts.x);
        o.lighting = float4(accum.lighting, accum.samples);
        o.combined = o.lighting.rgb;
        return o;
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

      float IndirectDiffuseModifier;


      output frag(BlitInput i)
      {
        output o = DefaultO();
        float depth = _CameraDepthTexture.Sample(point_clamp_sampler, i.uv).r;

        if (Linear01Depth(depth) >= 1.0) o;

        LightingData data = ConstructLightingData(ClipToWorld, i.uv, depth);

        TSSInfo old = GetOldLighting(data.worldPosition);

        float samplesOutput = 0;
        float3 lighting = IndirectDiffusePixelRadiance(data, old.samples < PerPixelGIRayCounts.x, samplesOutput);


        TSSInfo cur = MakeTSSInfo(data.worldPosition, samplesOutput, i.uv, lighting);

        TSSInfo accum = TemporallyAccumulate(old, cur, PerPixelGIRayCounts.x);
        o.lighting = float4(accum.lighting, accum.samples);
        o.combined = IndirectDiffuseModifier * o.lighting.rgb;

        return o;
      }
      ENDHLSL
    }

    Pass
    {
      Name "IndirectSpecular"

      HLSLPROGRAM
      #pragma vertex BlitVertex
      #pragma fragment frag
      #pragma multi_compile _ VXGI_ANISOTROPIC_VOXEL
      #pragma multi_compile _ VXGI_CASCADES

      float IndirectSpecularModifier;

      float3 frag(BlitInput i) : SV_TARGET
      {
        float depth = _CameraDepthTexture.Sample(point_clamp_sampler, i.uv).r;

        if (Linear01Depth(depth) >= 1.0) return 0.0;

        return IndirectSpecularModifier * IndirectSpecularPixelRadiance(ConstructLightingData(ClipToWorld, i.uv, depth));
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

        Texture2D LightingBuffer;
        float stepwidth = 1;

        output frag(BlitInput i) : SV_TARGET
        {
          output o = DefaultO();

          float depth = _CameraDepthTexture.Sample(point_clamp_sampler, i.uv).r;

          if (Linear01Depth(depth) >= 1.0) return o;

          LightingData data = ConstructLightingData(ClipToWorld, i.uv, depth);



          float3 lighting = LightingBuffer.Sample(point_clamp_sampler, i.uv).rgb;


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
            lightingsum += LightingBuffer.Sample(point_clamp_sampler, uv).rgb * weight;
            totalWeight += weight;
          }

          o.combined = lightingsum.xyz / totalWeight;

          return o;
        }
        ENDHLSL
      }
  }

  Fallback Off
}
