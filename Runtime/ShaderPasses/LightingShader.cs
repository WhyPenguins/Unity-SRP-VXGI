using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using System.Collections.Generic;

public class LightingShader {
  public enum Pass {
    Combine = 0,
    DirectDiffuseSpecular = 1,
    IndirectDiffuse = 2,
    IndirectSpecular = 3,
    Spatial = 4
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

  Pass _pass;
  static RenderTextureDescriptor lightingDesc = new RenderTextureDescriptor
  {
    graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,//Color in rgb, frameCount in a
    dimension = TextureDimension.Tex2D,
    enableRandomWrite = false,
    msaaSamples = 1,
  };

  //Sorry about this.
  Dictionary<Camera, PingPongTexture> CameraLightings = new Dictionary<Camera, PingPongTexture>();

  public LightingShader(Pass pass) {
    _pass = pass;
  }

  public void Execute(CommandBuffer command, Camera camera, RenderTargetIdentifier destination, float scale = 1f) {

    scale = Mathf.Clamp01(scale);
    int lowResWidth = (int)(scale * camera.pixelWidth);
    int lowResHeight = (int)(scale * camera.pixelHeight);


    if (!CameraLightings.ContainsKey(camera))
      CameraLightings[camera] = new PingPongTexture();

    PingPongTexture lighting = CameraLightings[camera];

    lighting.Update(new Vector3Int(lowResWidth, lowResHeight, 1), lightingDesc, true);

    command.BeginSample(_pass.ToString());
    command.GetTemporaryRT(ShaderIDs.Dummy, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);

    //Unity doesn't seem to expose per-render-target blend modes, so have to do a clear.
    command.SetRenderTarget(lighting.current);
    command.ClearRenderTarget(false, true, Color.clear);
    command.SetGlobalTexture(ShaderIDs._previousLighting, lighting.old);
    if (scale == 1f) {
      RenderTargetBinding binding = new RenderTargetBinding(new RenderTargetIdentifier[] { lighting.current, destination }, new RenderBufferLoadAction[] { RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare }, new RenderBufferStoreAction[] { RenderBufferStoreAction.Store, RenderBufferStoreAction.Store }, lighting.current.depthBuffer/*new RenderTargetIdentifier("I don't need a depth buffer, but I can't put null here.")*/, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);

      TextureUtil.Blit(command, ShaderIDs.Dummy, binding, material, (int)_pass);

    } else {

      command.GetTemporaryRT(ShaderIDs.LowResColor, lowResWidth, lowResHeight, 0, FilterMode.Bilinear, RenderTextureFormat.RGB111110Float, RenderTextureReadWrite.Linear);
      command.GetTemporaryRT(ShaderIDs.LowResDepth, lowResWidth, lowResHeight, 16, FilterMode.Bilinear, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);

      command.SetRenderTarget(ShaderIDs.LowResColor, (RenderTargetIdentifier)ShaderIDs.LowResDepth);
      command.ClearRenderTarget(true, true, Color.clear);

      RenderTargetBinding binding = new RenderTargetBinding(new RenderTargetIdentifier[] { lighting.current, ShaderIDs.LowResColor }, new RenderBufferLoadAction[] { RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare }, new RenderBufferStoreAction[] { RenderBufferStoreAction.Store, RenderBufferStoreAction.Store }, lighting.current.depthBuffer, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
      TextureUtil.Blit(command, ShaderIDs.Dummy, binding, material, (int)_pass);
      command.Blit(ShaderIDs.Dummy, ShaderIDs.LowResDepth, UtilityShader.material, (int)UtilityShader.Pass.DepthCopy);
      command.Blit(ShaderIDs.LowResColor, destination, UtilityShader.material, (int)UtilityShader.Pass.LowResComposite);

      command.ReleaseTemporaryRT(ShaderIDs.LowResColor);
      command.ReleaseTemporaryRT(ShaderIDs.LowResDepth);
    }

    command.ReleaseTemporaryRT(ShaderIDs.Dummy);
    command.EndSample(_pass.ToString());
    lighting.Swap();
  }
}
