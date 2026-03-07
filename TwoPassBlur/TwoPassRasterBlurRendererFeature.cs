#if URP_COMPATIBILITY_MODE
#else
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace PKGE
{
    /// <see href="https://github.com/Unity-Technologies/RenderGraphWebinarSample01/blob/51b1db71ed3d4c1ef0852ddaabab5f09dec8402a/Assets/01_TwoPassBlur/TwoPassRasterBlurRendererFeature.cs#L8"/>
    internal sealed
    class RasterBlurRenderPass : ScriptableRenderPass
    {
        const string s_passName = "2 Pass Blur";
        static readonly ProfilingSampler s_profilingSampler = new ProfilingSampler(s_passName);

        private Material _material;
        private ShadingRateFragmentSize _shadingRateFragmentSize;
        private float _blurSize;
        private static readonly int s_blurSizeId = Shader.PropertyToID("_BlurSize");
        private static readonly int s_blitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int s_blitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly MaterialPropertyBlock s_propertyBlock = new MaterialPropertyBlock();

        sealed
        public class PassData
        {
            public Material material;
            public MaterialPropertyBlock propertyBlock;
            public TextureHandle srcHandle;
            public int shaderPass;
            public float blurSize;
        }

        public void Setup(Material material, float blurSize, ShadingRateFragmentSize shadingRateFragmentSize)
        {
            _material = material;
            _blurSize = blurSize;
            _shadingRateFragmentSize = shadingRateFragmentSize;
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
            desc.name = "Blur Temp Buffer";
            TextureHandle tmpHandle = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(s_passName, out PassData passData, s_profilingSampler))
            {
                builder.UseTexture(cameraTarget);
                builder.SetRenderAttachment(tmpHandle, 0);
                passData.material = _material;
                passData.blurSize = _blurSize;
                passData.shaderPass = 0;
                passData.srcHandle = cameraTarget;
                passData.propertyBlock = s_propertyBlock;
                builder.SetShadingRateFragmentSize(_shadingRateFragmentSize);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) => ExecutePass(data, ctx));
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(s_passName, out PassData passData, s_profilingSampler))
            {
                builder.UseTexture(tmpHandle);
                builder.SetRenderAttachment(cameraTarget, 0);
                passData.material = _material;
                passData.blurSize = _blurSize;
                passData.shaderPass = 1;
                passData.srcHandle = tmpHandle;
                passData.propertyBlock = s_propertyBlock;
                builder.SetShadingRateFragmentSize(_shadingRateFragmentSize);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) => ExecutePass(data, ctx));
            }
        }

        static void ExecutePass(PassData data, RasterGraphContext ctx)
        {
            data.propertyBlock.SetTexture(s_blitTextureId, data.srcHandle);
            data.propertyBlock.SetVector(s_blitScaleBiasId, new Vector4(1, 1, 0, 0));
            data.propertyBlock.SetFloat(s_blurSizeId, data.blurSize);
            ctx.cmd.DrawProcedural(Matrix4x4.identity, data.material, data.shaderPass, MeshTopology.Triangles, 3, 1, data.propertyBlock);
        }
    }

    /// <see href="https://github.com/Unity-Technologies/RenderGraphWebinarSample01/blob/51b1db71ed3d4c1ef0852ddaabab5f09dec8402a/Assets/01_TwoPassBlur/TwoPassRasterBlurRendererFeature.cs#L88"/>
    sealed
    public class TwoPassRasterBlurRendererFeature : ScriptableRendererFeature
    {
        private RasterBlurRenderPass _pass;
        private Material _material;

        [SerializeField]
        private Shader _shader;
        [SerializeField]
        private RenderPassEvent _passEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        [SerializeField, Range(0.0f, 0.1f)]
        private float _blurSize = 0.005f;

        public float blurSize { get => _blurSize; set => _blurSize = value; }
        public ShadingRateFragmentSize shadingRateFragmentSize;

        public override void Create()
        {
            _pass = new RasterBlurRenderPass();
            _pass.renderPassEvent = _passEvent;
            _material = new Material(_shader);
        }

        protected override void Dispose(bool disposing)
        {
            DestroyImmediate(_material);
            _material = null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            UnityEngine.Assertions.Assert.IsNotNull(_material);

            _pass.Setup(_material, _blurSize, shadingRateFragmentSize);
            renderer.EnqueuePass(_pass);
        }
    }
}
#endif // URP_COMPATIBILITY_MODE
