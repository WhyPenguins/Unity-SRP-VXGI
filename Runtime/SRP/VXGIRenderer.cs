using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

public class VXGIRenderer : System.IDisposable {
  const string _sampleCameraEvent = "CameraEvent.";
  const string _samplePostProcessRender = "PostProcess.Render";
  const string _samplePostProcessRenderOpaqueOnly = "PostProcess.Render.OpaqueOnly";
  const string _sampleRenderLighting = "Render.Lighting";

  public VXGIRenderPipeline RenderPipeline { get; }

  float[] _renderScale;
  CommandBuffer _command;
  CommandBuffer _eventCommand;
  ComputeBuffer _lightSources;
  CullingResults _cullingResults;
  FilteringSettings _filteringSettings;
  LightingShader[] _lightingPasses;
  PostProcessRenderContext _postProcessRenderContext;
  RenderTargetBinding _gBufferBinding;
  ScriptableCullingParameters _cullingParameters;


  public VXGIRenderer(VXGIRenderPipeline renderPipeline) {
    RenderPipeline = renderPipeline;

    _command = new CommandBuffer { name = "VXGI.Renderer" };
    _eventCommand = new CommandBuffer();
    _filteringSettings = FilteringSettings.defaultValue;

    _gBufferBinding = new RenderTargetBinding(
      new RenderTargetIdentifier[] { ShaderIDs._CameraGBufferTexture0, ShaderIDs._CameraGBufferTexture1, ShaderIDs._CameraGBufferTexture2, ShaderIDs._CameraGBufferTexture3 },
      new[] { RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare },
      new[] { RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare },
      ShaderIDs._CameraDepthTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare
    );

    _renderScale = new float[] { 1f, 1f, 1f, 1f };

    _lightingPasses = new LightingShader[] {
      new LightingShader(LightingShader.Pass.Emission),
      new LightingShader(LightingShader.Pass.DirectDiffuseSpecular),
      new LightingShader(LightingShader.Pass.IndirectDiffuse),
      new LightingShader(LightingShader.Pass.IndirectSpecular)
    };

    _lightSources = new ComputeBuffer(128, LightSource.size);
    _postProcessRenderContext = new PostProcessRenderContext();
  }

  public void Dispose() {
    _command.Dispose();
    _eventCommand.Dispose();
    _lightSources.Dispose();
  }

  //I hate to do this but I have no idea how else to track the state of each camera seperately.
  //This will leak memory whenever cameras are destroyed, but honestly what else can be done?
  Dictionary<Camera, PingPongTexture> CameraDepthTextures = new Dictionary<Camera, PingPongTexture>();
  Dictionary<Camera, Matrix4x4> clipToWorldLast = new Dictionary<Camera, Matrix4x4>();
  Dictionary<Camera, Matrix4x4> previousProj = new Dictionary<Camera, Matrix4x4>();

  public void RenderDeferred(ScriptableRenderContext renderContext, Camera camera, VXGI vxgi) {
    if (!camera.TryGetCullingParameters(out _cullingParameters)) return;

    _cullingResults = renderContext.Cull(ref _cullingParameters);

    renderContext.SetupCameraProperties(camera);
    if (!previousProj.ContainsKey(camera))
      previousProj[camera] = new Matrix4x4();

    _command.SetGlobalTexture(ShaderIDs.NoiseRGB16bit, TextureUtil.NoiseRGB16bit);
    _command.SetGlobalMatrix(ShaderIDs._lastFrameViewProj, previousProj[camera]);
    

    int width = camera.pixelWidth;
    int height = camera.pixelHeight;

    _command.BeginSample(_command.name);

    SetupLightSources(_command, vxgi);

    if (camera.cameraType != CameraType.SceneView) {
      _command.EnableShaderKeyword("PROJECTION_PARAMS_X");
    } else {
      _command.DisableShaderKeyword("PROJECTION_PARAMS_X");
    }

    TriggerCameraEvent(renderContext, camera, CameraEvent.BeforeGBuffer, vxgi);

    bool RenderWithTemporal = true;// camera == Camera.main;

    if (!CameraDepthTextures.ContainsKey(camera))
      CameraDepthTextures[camera] = new PingPongTexture();

    PingPongTexture CameraDepthTexture = CameraDepthTextures[camera];

    //Debug.Log(RenderWithTemporal);
    if (RenderWithTemporal)
    {
      CameraDepthTexture.Update(new Vector3Int(width, height, 1), new RenderTextureDescriptor
      {
        colorFormat = RenderTextureFormat.Depth,
        depthBufferBits = 24,
        dimension = TextureDimension.Tex2D,
        sRGB = false,
        msaaSamples = 1
      }, true);
      CameraDepthTexture.current.filterMode = FilterMode.Point;
    }

    if (RenderWithTemporal)
    {
      _command.SetGlobalTexture(ShaderIDs._CameraDepthTexture, CameraDepthTexture.current);
      _command.SetGlobalTexture(ShaderIDs._CameraDepthTexture_LastFrame, CameraDepthTexture.old);
    }
    if (!RenderWithTemporal)
      _command.GetTemporaryRT(ShaderIDs._CameraDepthTexture, width, height, 24, FilterMode.Point, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);

    RenderTargetIdentifier depthTex = RenderWithTemporal ? (RenderTargetIdentifier)CameraDepthTexture.current : (RenderTargetIdentifier)ShaderIDs._CameraDepthTexture;

    _command.GetTemporaryRT(ShaderIDs._CameraGBufferTexture0, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
    _command.GetTemporaryRT(ShaderIDs._CameraGBufferTexture1, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
    _command.GetTemporaryRT(ShaderIDs._CameraGBufferTexture2, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);
    _command.GetTemporaryRT(ShaderIDs._CameraGBufferTexture3, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
    _command.GetTemporaryRT(ShaderIDs.FrameBuffer, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

    _gBufferBinding.depthRenderTarget = depthTex;

    _command.SetRenderTarget(_gBufferBinding);
    _command.ClearRenderTarget(true, true, Color.clear);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    RenderGBuffers(renderContext, camera);
    TriggerCameraEvent(renderContext, camera, CameraEvent.AfterGBuffer, vxgi);

    CopyCameraTargetToFrameBuffer(depthTex, renderContext, camera);

    bool depthNormalsNeeded = (camera.depthTextureMode & DepthTextureMode.DepthNormals) != DepthTextureMode.None;

    if (depthNormalsNeeded) {
      TriggerCameraEvent(renderContext, camera, CameraEvent.BeforeDepthNormalsTexture, vxgi);
      RenderCameraDepthNormalsTexture(depthTex, renderContext, camera);
      TriggerCameraEvent(renderContext, camera, CameraEvent.AfterDepthNormalsTexture, vxgi);
    }

    TriggerCameraEvent(renderContext, camera, CameraEvent.BeforeLighting, vxgi);
    RenderLighting(renderContext, camera, vxgi);
    TriggerCameraEvent(renderContext, camera, CameraEvent.AfterLighting, vxgi);

    renderContext.SetupCameraProperties(camera);

    if (camera.clearFlags == CameraClearFlags.Skybox) {
      TriggerCameraEvent(renderContext, camera, CameraEvent.BeforeSkybox, vxgi);
      RenderSkyBox(depthTex, renderContext, camera);
      TriggerCameraEvent(renderContext, camera, CameraEvent.AfterSkybox, vxgi);
    }

    UpdatePostProcessingLayer(renderContext, camera, vxgi);

    TriggerCameraEvent(renderContext, camera, CameraEvent.BeforeImageEffectsOpaque, vxgi);
    RenderPostProcessingOpaqueOnly(renderContext, camera);
    TriggerCameraEvent(renderContext, camera, CameraEvent.AfterImageEffectsOpaque, vxgi);

    TriggerCameraEvent(renderContext, camera, CameraEvent.BeforeForwardAlpha, vxgi);
    RenderTransparent(depthTex, renderContext, camera);
    TriggerCameraEvent(renderContext, camera, CameraEvent.AfterForwardAlpha, vxgi);

    RenderGizmos(renderContext, camera, GizmoSubset.PreImageEffects);

    renderContext.SetupCameraProperties(camera);

    _command.SetRenderTarget(ShaderIDs.FrameBuffer, _gBufferBinding.depthRenderTarget);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    renderContext.InvokeOnRenderObjectCallback();

    TriggerCameraEvent(renderContext, camera, CameraEvent.BeforeImageEffects, vxgi);
    RenderPostProcessing(renderContext, camera);
    _command.SetGlobalVector(ShaderIDs.BlitViewport, new Vector4(camera.rect.width, camera.rect.height, camera.rect.xMin, camera.rect.yMin));
    _command.Blit(ShaderIDs.FrameBuffer, BuiltinRenderTextureType.CameraTarget, UtilityShader.material, (int)UtilityShader.Pass.BlitViewport);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
    TriggerCameraEvent(renderContext, camera, CameraEvent.AfterImageEffects, vxgi);

    RenderGizmos(renderContext, camera, GizmoSubset.PostImageEffects);

    TriggerCameraEvent(renderContext, camera, CameraEvent.AfterEverything, vxgi);

    if (depthNormalsNeeded) {
      _command.ReleaseTemporaryRT(ShaderIDs._CameraDepthNormalsTexture);
    }

    if (!RenderWithTemporal)
      _command.ReleaseTemporaryRT(ShaderIDs._CameraDepthTexture);
    _command.ReleaseTemporaryRT(ShaderIDs._CameraGBufferTexture0);
    _command.ReleaseTemporaryRT(ShaderIDs._CameraGBufferTexture1);
    _command.ReleaseTemporaryRT(ShaderIDs._CameraGBufferTexture2);
    _command.ReleaseTemporaryRT(ShaderIDs._CameraGBufferTexture3);
    _command.ReleaseTemporaryRT(ShaderIDs.FrameBuffer);
    _command.EndSample(_command.name);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    if (RenderWithTemporal)
    {
      previousProj[camera] = camera.projectionMatrix * camera.worldToCameraMatrix;
      CameraDepthTexture.Swap();
    }
  }

  void CopyCameraTargetToFrameBuffer(RenderTargetIdentifier depthTex, ScriptableRenderContext renderContext, Camera camera) {
    _command.GetTemporaryRT(ShaderIDs.Dummy, Screen.width, Screen.height, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
    _command.SetGlobalVector(ShaderIDs.BlitViewport, new Vector4(camera.rect.width, camera.rect.height, camera.rect.xMin, camera.rect.yMin));
    _command.Blit(depthTex, BuiltinRenderTextureType.CameraTarget, UtilityShader.material, (int)UtilityShader.Pass.DepthCopyViewport);
    _command.Blit(BuiltinRenderTextureType.CameraTarget, ShaderIDs.Dummy);
    _command.Blit(ShaderIDs.Dummy, ShaderIDs.FrameBuffer, UtilityShader.material, (int)UtilityShader.Pass.GrabCopy);
    _command.ReleaseTemporaryRT(ShaderIDs.Dummy);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  void RenderCameraDepthNormalsTexture(RenderTargetIdentifier depthTex, ScriptableRenderContext renderContext, Camera camera) {
    _command.GetTemporaryRT(ShaderIDs._CameraDepthNormalsTexture, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
    _command.Blit(depthTex, ShaderIDs._CameraDepthNormalsTexture, UtilityShader.material, (int)UtilityShader.Pass.EncodeDepthNormal);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  void RenderGBuffers(ScriptableRenderContext renderContext, Camera camera) {
    var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
    var drawingSettings = new DrawingSettings(ShaderTagIDs.Deferred, sortingSettings) { perObjectData = RenderPipeline.PerObjectData };

    _filteringSettings.renderQueueRange = RenderQueueRange.opaque;
    renderContext.DrawRenderers(_cullingResults, ref drawingSettings, ref _filteringSettings);
  }

  [System.Diagnostics.Conditional("UNITY_EDITOR")]
  void RenderGizmos(ScriptableRenderContext renderContext, Camera camera, GizmoSubset gizmoSubset)
  {
#if UNITY_EDITOR
    if (UnityEditor.Handles.ShouldRenderGizmos())
    {
      renderContext.DrawGizmos(camera, gizmoSubset);
    }
#endif
  }
  void RenderLighting(ScriptableRenderContext renderContext, Camera camera, VXGI vxgi)
  {
    if (!clipToWorldLast.ContainsKey(camera))
      clipToWorldLast[camera] = new Matrix4x4();
    Matrix4x4 clipToWorld = camera.cameraToWorldMatrix * GL.GetGPUProjectionMatrix(camera.projectionMatrix, false).inverse;

    _renderScale[2] = vxgi.diffuseResolutionScale;

    _command.BeginSample(_sampleRenderLighting);
    _command.SetGlobalMatrix(ShaderIDs.ClipToVoxel, vxgi.ColorVoxelizer.worldToVoxel * clipToWorld);
    _command.SetGlobalMatrix(ShaderIDs.ClipToWorld, clipToWorld);
    _command.SetGlobalMatrix(ShaderIDs.ClipToWorldPrev, clipToWorldLast[camera]);
    _command.SetGlobalMatrix(ShaderIDs.VoxelToWorld, vxgi.ColorVoxelizer.voxelToWorld);
    _command.SetGlobalMatrix(ShaderIDs.WorldToVoxel, vxgi.ColorVoxelizer.worldToVoxel);

    for (int i = 0; i < _lightingPasses.Length; i++) {
      _lightingPasses[i].Execute(_command, camera, ShaderIDs.FrameBuffer, _renderScale[i]);
    }

    _command.EndSample(_sampleRenderLighting);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
    clipToWorldLast[camera] = clipToWorld;
  }

  void RenderPostProcessing(ScriptableRenderContext renderContext, Camera camera) {
    var layer = camera.GetComponent<PostProcessLayer>();

    if (layer == null || !layer.isActiveAndEnabled) return;

    _postProcessRenderContext.Reset();
    _postProcessRenderContext.camera = camera;
    _postProcessRenderContext.command = _command;
    _postProcessRenderContext.destination = ShaderIDs.FrameBuffer;
    _postProcessRenderContext.source = ShaderIDs.Dummy;
    _postProcessRenderContext.sourceFormat = RenderTextureFormat.ARGBHalf;

    _command.BeginSample(_samplePostProcessRender);
    _command.GetTemporaryRT(ShaderIDs.Dummy, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
    _command.Blit(ShaderIDs.FrameBuffer, ShaderIDs.Dummy);
    layer.Render(_postProcessRenderContext);
    _command.ReleaseTemporaryRT(ShaderIDs.Dummy);
    _command.EndSample(_samplePostProcessRender);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  void RenderPostProcessingOpaqueOnly(ScriptableRenderContext renderContext, Camera camera) {
    var layer = camera.GetComponent<PostProcessLayer>();

    if (layer == null || !layer.isActiveAndEnabled) return;

    _postProcessRenderContext.Reset();
    _postProcessRenderContext.camera = camera;
    _postProcessRenderContext.command = _command;
    _postProcessRenderContext.destination = ShaderIDs.FrameBuffer;
    _postProcessRenderContext.source = ShaderIDs.Dummy;
    _postProcessRenderContext.sourceFormat = RenderTextureFormat.ARGBHalf;

    if (!layer.HasOpaqueOnlyEffects(_postProcessRenderContext)) return;

    _command.BeginSample(_samplePostProcessRenderOpaqueOnly);
    _command.GetTemporaryRT(ShaderIDs.Dummy, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
    _command.Blit(ShaderIDs.FrameBuffer, ShaderIDs.Dummy);
    layer.RenderOpaqueOnly(_postProcessRenderContext);
    _command.ReleaseTemporaryRT(ShaderIDs.Dummy);
    _command.EndSample(_samplePostProcessRenderOpaqueOnly);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  void RenderSkyBox(RenderTargetIdentifier depthTex, ScriptableRenderContext renderContext, Camera camera) {
    _command.SetRenderTarget(ShaderIDs.FrameBuffer, depthTex);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
    renderContext.DrawSkybox(camera);
  }

  void RenderTransparent(RenderTargetIdentifier depthTex, ScriptableRenderContext renderContext, Camera camera) {
    _command.SetRenderTarget(ShaderIDs.FrameBuffer, depthTex);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonTransparent };
    var drawingSettings = new DrawingSettings(ShaderTagIDs.ForwardBase, sortingSettings) { perObjectData = RenderPipeline.PerObjectData };

    _filteringSettings.renderQueueRange = RenderQueueRange.transparent;
    renderContext.DrawRenderers(_cullingResults, ref drawingSettings, ref _filteringSettings);
  }

  void SetupLightSources(CommandBuffer command, VXGI vxgi) {
    var count = 0;
    var data = new NativeArray<LightSource>(128, Allocator.Temp);

    for (int i = 0; i < data.Length && i < _cullingResults.visibleLights.Length; i++) {
      var light = _cullingResults.visibleLights[i];

      if (VXGI.SupportedLightTypes.Contains(light.lightType) && light.finalColor.maxColorComponent > 0f) {
        data[count++] = new LightSource(light, vxgi, vxgi.ColorVoxelizer);
      }
    }

    _lightSources.SetData(data);

    command.SetGlobalInt(ShaderIDs.LightCount, count);
    command.SetGlobalBuffer(ShaderIDs.LightSources, _lightSources);

    data.Dispose();
  }

  void TriggerCameraEvent(ScriptableRenderContext renderContext, Camera camera, CameraEvent cameraEvent, VXGI vxgi) {
    camera = camera.cameraType == CameraType.SceneView ? vxgi.Camera : camera;

    var commands = camera.GetCommandBuffers(cameraEvent);

    if (commands.Length == 0) return;

    _eventCommand.name = _sampleCameraEvent + cameraEvent.ToString();
    _eventCommand.BeginSample(_eventCommand.name);
    _eventCommand.SetRenderTarget(ShaderIDs.FrameBuffer);
    renderContext.ExecuteCommandBuffer(_eventCommand);
    _eventCommand.Clear();

    foreach (var command in commands) renderContext.ExecuteCommandBuffer(command);

    _eventCommand.EndSample(_eventCommand.name);
    renderContext.ExecuteCommandBuffer(_eventCommand);
    _eventCommand.Clear();
  }

  void UpdatePostProcessingLayer(ScriptableRenderContext renderContext, Camera camera, VXGI vxgi) {
    var layer = vxgi.GetComponent<PostProcessLayer>();

    if (layer == null || !layer.isActiveAndEnabled) return;

    layer.UpdateVolumeSystem(camera, _command);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }
}
