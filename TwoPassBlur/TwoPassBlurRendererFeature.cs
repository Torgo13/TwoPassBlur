using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

class BlurRenderPass : ScriptableRenderPass
{
    static readonly string s_passName = "2 Pass Blur";
    static readonly ProfilingSampler s_profilingSampler = new (s_passName);
    
    private RTHandle _tmpHandle;
    private Material _material;
    
    private float _blurSize;
    static readonly int s_blurSizeId = Shader.PropertyToID("_BlurSize");
    
    public class PassData
    {
        public Material material;
        public TextureHandle cameraTarget;
        public TextureHandle tmpHandle;
        public float blurSize;
    }
    
    public void Setup(Material material, float blurSize)
    {
        _material = material;
        _blurSize = blurSize;
    }

    public void CleanUp()
    {
        _tmpHandle?.Release();
    }
    
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        RenderTextureDescriptor desc = cameraTextureDescriptor;
        desc.depthBufferBits = 0;
        desc.msaaSamples = 1;
        RenderingUtils.ReAllocateHandleIfNeeded (ref _tmpHandle, desc);
    }
    

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (_material == null)
            return;
        var cmd = CommandBufferPool.Get(s_passName);
        using (new ProfilingScope(cmd, s_profilingSampler))
        {
            cmd.Clear();
            
            RTHandle cameraTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
            if (cameraTarget == null || cameraTarget.rt == null)
                return;
    
            // step 1: vertical pass
            _material.SetFloat(s_blurSizeId, _blurSize);
            // Blit(cmd, cameraTarget, _tmpHandle, _material, 0);
            Blitter.BlitCameraTexture(cmd, cameraTarget, _tmpHandle, _material, 0);
            
            // step 2: horizontal pass
            // Blit(cmd, _tmpHandle, cameraTarget, _material, 1);
            Blitter.BlitCameraTexture(cmd, _tmpHandle, cameraTarget, _material, 1);
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }
        CommandBufferPool.Release(cmd);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        // ContextContainer (frameData) から UniversalResourceData を取得し、カメラターゲット(activeColorTexture)を取得   
        var resourceData = frameData.Get<UniversalResourceData>();
        TextureHandle cameraTarget = resourceData.activeColorTexture;
        if (!cameraTarget.IsValid())
            return;

        // TextureHandle.GetDescriptor で TextureHandle から TextureDescriptor を取得し部分的に変更を加える
        TextureDesc desc = cameraTarget.GetDescriptor(renderGraph);
        desc.depthBufferBits = 0;
        desc.msaaSamples = MSAASamples.None;
        TextureHandle tmpHandle = renderGraph.CreateTexture(desc);
        
#if true
        // Unsafe Pass
        using var builder = renderGraph.AddUnsafePass<PassData>(s_passName, out PassData passData, s_profilingSampler);
        builder.UseTexture(cameraTarget, AccessFlags.ReadWrite);
        builder.UseTexture(tmpHandle, AccessFlags.ReadWrite);
        
        // Set PassData
        passData.cameraTarget = cameraTarget;
        passData.tmpHandle = tmpHandle;
        passData.blurSize = _blurSize;
        passData.material = _material;
        
        builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) => ExecutePass(data, ctx));
        
        static void ExecutePass(PassData data, UnsafeGraphContext ctx)
        {
            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);

            // step 1: vertical pass
            data.material.SetFloat(s_blurSizeId, data.blurSize);
            Blitter.BlitCameraTexture(cmd, data.cameraTarget, data.tmpHandle, data.material, 0);
            
            // step 2: horizontal pass
            Blitter.BlitCameraTexture(cmd, data.tmpHandle, data.cameraTarget, data.material, 1);
        }
 
#else
        // Unsafe Pass ユーティリティ関数バージョン
        // step 1: vertical pass
        _material.SetFloat(s_blurSizeId, _blurSize);
        RenderGraphUtils.BlitMaterialParameters param1 = new(cameraTarget, tmpHandle, _material, 0);
        renderGraph.AddBlitPass(param1);
        
        // step 2: horizontal pass
        RenderGraphUtils.BlitMaterialParameters param2 = new(tmpHandle, cameraTarget, _material, 1);
        renderGraph.AddBlitPass(param2); 
#endif
        
     }
}
    
public class TwoPassBlurRendererFeature : ScriptableRendererFeature
{
    private BlurRenderPass _pass;
    private Material _material;
    
    [SerializeField]
    private RenderPassEvent _passEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    [SerializeField, Range(0.0f, 0.1f)]
    private float _blurSize = 0.005f;

    public override void Create()
    {
        _pass = new BlurRenderPass();
        _pass.renderPassEvent = _passEvent;
        _material = new Material(Shader.Find("Hidden/RGSample/2PassBlur"));
    }

    protected override void Dispose(bool disposing)
    {
        _pass.CleanUp();
        DestroyImmediate(_material);
        _material = null;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null)
            return;
        _pass.Setup(_material, _blurSize);
        renderer.EnqueuePass(_pass);
    }
}
