#define EnvironmentNotSane
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using System.Collections.Generic;

public class LightingShader {

  public class PerCameraInstance
  {
    public int lastFrameUsed;

    public bool SplitSpecularDiffuse = true;

    public bool temporallyFilterDiffuse;
    public bool temporallyFilterSpecular;

    public PingPongTexture Diffuse = new PingPongTexture();
    public PingPongTexture Specular = new PingPongTexture();

    RenderTargetBinding binding = new RenderTargetBinding();

    public void Execute(Pass _pass, CommandBuffer command, Camera camera, RenderTargetIdentifier diffuseTarget, RenderTargetIdentifier specularTarget, float scale = 1f)
    {
      bool useDiffuseTemp = temporallyFilterDiffuse;
#if EnvironmentNotSane
      if (!SplitSpecularDiffuse && !temporallyFilterSpecular && !temporallyFilterDiffuse)
        useDiffuseTemp = true;
#endif

        lastFrameUsed = Time.renderedFrameCount;
      UtilityShader.SetKeyword(command, "VXGI_TEMPORAL_DIFFUSE", useDiffuseTemp);
      UtilityShader.SetKeyword(command, "VXGI_TEMPORAL_SPECULAR", temporallyFilterSpecular);
      UtilityShader.SetKeyword(command, "VXGI_TEMPORAL_SEPARATE", SplitSpecularDiffuse);

      scale = Mathf.Clamp01(scale);
      int lowResWidth = (int)(scale * camera.pixelWidth);
      int lowResHeight = (int)(scale * camera.pixelHeight);


      Diffuse.Update(new Vector3Int(lowResWidth, lowResHeight, 1), lightingDesc, useDiffuseTemp);
      Specular.Update(new Vector3Int(lowResWidth, lowResHeight, 1), lightingDesc, temporallyFilterSpecular && SplitSpecularDiffuse);

      command.GetTemporaryRT(ShaderIDs.Dummy, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);

      //Unity doesn't seem to expose per-render-target blend modes, so have to do a clear.
      if (useDiffuseTemp)
      {
        command.SetRenderTarget(Diffuse.current);
        command.ClearRenderTarget(false, true, Color.clear);
        command.SetGlobalTexture(ShaderIDs._previousDiffuseLighting, Diffuse.old);
      }
      if (temporallyFilterSpecular && SplitSpecularDiffuse)
      {
        command.SetRenderTarget(Specular.current);
        command.ClearRenderTarget(false, true, Color.clear);
        command.SetGlobalTexture(ShaderIDs._previousSpecularLighting, Specular.old);
      }

      binding.depthLoadAction = RenderBufferLoadAction.DontCare;
      binding.depthStoreAction = RenderBufferStoreAction.DontCare;
      binding.depthRenderTarget = diffuseTarget;

      int rtcount = 0;
      rtcount += SplitSpecularDiffuse ? 2 : 1;
      rtcount += useDiffuseTemp ? 1 : 0;
      rtcount += temporallyFilterSpecular && SplitSpecularDiffuse ? 1 : 0;

      if (binding.colorRenderTargets == null || rtcount != binding.colorRenderTargets.Length)
      {
        binding.colorRenderTargets = new RenderTargetIdentifier[rtcount];
        binding.colorLoadActions = new RenderBufferLoadAction[rtcount];
        binding.colorStoreActions = new RenderBufferStoreAction[rtcount];
        for (int i = 0; i < rtcount; i++)
        {
          binding.colorLoadActions[i] = RenderBufferLoadAction.Load;
          binding.colorStoreActions[i] = RenderBufferStoreAction.Store;
        }
      }

      if (scale == 1f) {
        int rtindex = 0;
        binding.colorRenderTargets[rtindex] = diffuseTarget; rtindex+=1;
        if (SplitSpecularDiffuse){binding.colorRenderTargets[rtindex] = specularTarget;rtindex += 1;}
        if (useDiffuseTemp) {binding.colorRenderTargets[rtindex] = Diffuse.current; rtindex += 1;}
        if (temporallyFilterSpecular && SplitSpecularDiffuse) {binding.colorRenderTargets[rtindex] = Specular.current; rtindex += 1;}

        TextureUtil.Blit(command, ShaderIDs.Dummy, binding, material, (int) _pass);

      } else {
        int rtindex = 0;
        binding.colorRenderTargets[rtindex] = ShaderIDs.LowResDiffuse; rtindex += 1;
        if (SplitSpecularDiffuse) { binding.colorRenderTargets[rtindex] = ShaderIDs.LowResSpecular; rtindex += 1; }
        if (useDiffuseTemp) { binding.colorRenderTargets[rtindex] = Diffuse.current; rtindex += 1; }
        if (temporallyFilterSpecular && SplitSpecularDiffuse) { binding.colorRenderTargets[rtindex] = Specular.current; rtindex += 1; }
        binding.depthRenderTarget = ShaderIDs.LowResDiffuse;

        command.GetTemporaryRT(ShaderIDs.LowResDiffuse, lowResWidth, lowResHeight, 0, FilterMode.Bilinear, RenderTextureFormat.RGB111110Float, RenderTextureReadWrite.Linear);
        command.GetTemporaryRT(ShaderIDs.LowResSpecular, lowResWidth, lowResHeight, 0, FilterMode.Bilinear, RenderTextureFormat.RGB111110Float, RenderTextureReadWrite.Linear);
        command.GetTemporaryRT(ShaderIDs.LowResDepth, lowResWidth, lowResHeight, 16, FilterMode.Bilinear, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);

        command.SetRenderTarget(new RenderTargetBinding(new RenderTargetIdentifier[] { ShaderIDs.LowResDiffuse, ShaderIDs.LowResSpecular }, new RenderBufferLoadAction[] { RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare }, new RenderBufferStoreAction[] { RenderBufferStoreAction.Store, RenderBufferStoreAction.Store }, ShaderIDs.LowResDepth, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare));
        command.ClearRenderTarget(true, true, Color.clear);

        TextureUtil.Blit(command, ShaderIDs.Dummy, binding, material, (int) _pass);
        command.Blit(ShaderIDs.Dummy, ShaderIDs.LowResDepth, UtilityShader.material, (int) UtilityShader.Pass.DepthCopy);
        //Ideally can be done in one blit
        command.SetGlobalTexture(ShaderIDs.LowResColor, ShaderIDs.LowResDiffuse);
        command.Blit(ShaderIDs.LowResDiffuse, diffuseTarget, UtilityShader.material, (int) UtilityShader.Pass.LowResComposite);
        command.SetGlobalTexture(ShaderIDs.LowResColor, ShaderIDs.LowResSpecular);
        command.Blit(ShaderIDs.LowResSpecular, specularTarget, UtilityShader.material, (int) UtilityShader.Pass.LowResComposite);

        command.ReleaseTemporaryRT(ShaderIDs.LowResDiffuse);
        command.ReleaseTemporaryRT(ShaderIDs.LowResSpecular);
        command.ReleaseTemporaryRT(ShaderIDs.LowResDepth);
      }

      command.ReleaseTemporaryRT(ShaderIDs.Dummy);
      Diffuse.Swap();
      Specular.Swap();
    }
  };

  public enum Pass {
    Combine = 0,
    DirectDiffuseSpecular = 1,
    IndirectDiffuseSpecular = 2,
    Spatial = 3
  }

  public static Material material {
    get {
      if (_material == null) _material = new Material(shader);

      return _material;
    }
  }
  public static Shader shader {
    get { return (Shader)Resources.Load("VXGI/Shader/Lighting"); }
  }

  static Material _material;

  public bool temporallyFilterDiffuse = true;
  public bool temporallyFilterSpecular = true;
  public bool SplitSpecularDiffuse = true;

  Pass _pass;
  static RenderTextureDescriptor lightingDesc = new RenderTextureDescriptor
  {
    graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,//Color in rgb, frameCount in a
    dimension = TextureDimension.Tex2D,
    enableRandomWrite = false,
    msaaSamples = 1,
  };

  Dictionary<Camera, PerCameraInstance> perCamera = new Dictionary<Camera, PerCameraInstance>();

  public LightingShader(Pass pass, bool temporallyfilterDiffuse, bool temporallyfilterSpecular) {
    _pass = pass;
    temporallyFilterDiffuse = temporallyfilterDiffuse;
    temporallyFilterSpecular = temporallyfilterSpecular;
    if (pass == Pass.Combine || pass == Pass.Spatial)
      SplitSpecularDiffuse = false;
  }

  public void Execute(CommandBuffer command, Camera camera, RenderTargetIdentifier diffuseTarget, RenderTargetIdentifier specularTarget, float scale = 1f) {

    command.BeginSample(_pass.ToString());

    if (!perCamera.ContainsKey(camera))
    {
      perCamera[camera] = new PerCameraInstance();
    }

    perCamera[camera].SplitSpecularDiffuse = SplitSpecularDiffuse;
    perCamera[camera].temporallyFilterDiffuse = temporallyFilterDiffuse;
    perCamera[camera].temporallyFilterSpecular = temporallyFilterSpecular;

    perCamera[camera].Execute(_pass, command, camera, diffuseTarget, specularTarget, scale);

    //Ideally the following would only execute once at the end of each frame, if so that + 2 can be made a + 1
    //Also this doesn't work properly in the editor because Time.renderedFrameCount doesn't increase when not in playmode
    List<Camera> toDelete = new List<Camera>();
    foreach (KeyValuePair<Camera, PerCameraInstance> per in perCamera)
    {
      if ((per.Value.lastFrameUsed + 2) < Time.renderedFrameCount)
      {
        toDelete.Add(per.Key);
      }
    }
    foreach (var cam in toDelete)
    {
      perCamera.Remove(cam);
    }

      
    command.EndSample(_pass.ToString());
  }
}
