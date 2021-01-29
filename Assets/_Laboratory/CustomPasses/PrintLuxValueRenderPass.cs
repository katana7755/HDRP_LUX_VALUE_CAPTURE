using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

class PrintLuxValueRenderPass : CustomPass
{
    [SerializeField] private LuxViewerController _Controller = null;
    [SerializeField] private LayerMask _LayerMask = ~0;
    [SerializeField] private Material _LuxPrintMaterial = null;
    [SerializeField] private Color _ClearLuxValue = Color.white;
    [SerializeField] private ComputeShader _LuxAverageComputeShader = null;
    [SerializeField] private SharedColorRTResource _LuxValueResource = null;
    [SerializeField] private SharedColorRTResource _LuxColorResource = null;
    [SerializeField] private SharedColorRTResource _LuxAverageResource = null;
    [SerializeField] private Texture2D _NumberTexture = null;
    [SerializeField] private Texture2D _NumberTileTexture = null;

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        if (_Controller == null)
        {
            ErrorMessage("Controller needs to be set");
            return;
        }

        if ((_LayerMask & (1 << _Controller.gameObject.layer)) == 0)
        {            
            ErrorMessage("You need to set LayerMask properly by adding new layer and setting the layer for all gameobjects under the controller");
            return;
        }

        if (_LuxAverageComputeShader == null)
        {
            ErrorMessage("LuxAverageComputeShader needs to be set");
            return;                       
        }        

        if (_LuxValueResource == null)
        {
            ErrorMessage("LuxValueResource needs to be set");
            return;
        }

        if (_LuxColorResource == null)
        {
            ErrorMessage("LuxColorResource needs to be set");
            return;
        }

        if (_LuxAverageResource == null)
        {
            ErrorMessage("LuxAverageResource needs to be set");
            return;
        }

        m_ShaderTagIds = new ShaderTagId[]
        {
            new ShaderTagId("Forward"),
            new ShaderTagId("ForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId(""),
        };
        m_LuxValuePassIndex = _LuxPrintMaterial.FindPass("Forward");
        m_LuxToColorPassIndex = _LuxPrintMaterial.FindPass("LuxToColor");
        m_LuxToColorProperties = new MaterialPropertyBlock();
        m_BufferSize = new int[2];
        m_StartOffset = new int[2];        
    }

    protected override void Execute(ScriptableRenderContext renderContext, CommandBuffer cmd, HDCamera hdCamera, CullingResults cullingResult)
    {
        if (_Controller == null /*|| !_Controller.GetUpdateTexturesFlag()*/)
        {
            return;
        }

        if (_LuxAverageComputeShader == null)
        {
            return;                       
        }        

        if (_LuxValueResource == null)
        {
            return;
        }

        if (_LuxColorResource == null)
        {
            return;
        }      

        if (_LuxAverageResource == null)
        {
            return;
        }          

        _LuxValueResource.AllocateColorRT(hdCamera.actualWidth, hdCamera.actualHeight);
        _LuxColorResource.AllocateColorRT(hdCamera.actualWidth, hdCamera.actualHeight);
        _LuxAverageResource.AllocateColorRT(hdCamera.actualWidth, hdCamera.actualHeight);

        if (!_LuxValueResource.IsValid() || !_LuxColorResource.IsValid() || !_LuxAverageResource.IsValid())
        {
            return;
        }
        
        // 1) Print lux values
        var valueRT = _LuxValueResource.GetColorRT();
        var scaleFactor = new Vector2((float)hdCamera.actualWidth / valueRT.referenceSize.x, (float)hdCamera.actualHeight / valueRT.referenceSize.y);
        Shader.SetGlobalVector(ShaderProperties._Lux_Value_RT_Scale, scaleFactor);

        var description = new RendererListDesc(m_ShaderTagIds, cullingResult, hdCamera.camera)
        {
            rendererConfiguration = PerObjectData.ShadowMask,
            renderQueueRange = RenderQueueRange.all,
            sortingCriteria = SortingCriteria.BackToFront,
            excludeObjectMotionVectors = false,
            overrideMaterial = _LuxPrintMaterial,
            overrideMaterialPassIndex = m_LuxValuePassIndex,
            layerMask = _LayerMask,            
        };
        var rendererList = RendererList.Create(description);

        if (!rendererList.isValid)
        {
            return;
        }

        CoreUtils.SetRenderTarget(cmd, valueRT, ClearFlag.Color, _ClearLuxValue);        
        HDUtils.DrawRendererList(renderContext, cmd, RendererList.Create(description));

        // 2) Render colors based on the previous lux value result
        var colorRT = _LuxColorResource.GetColorRT();
        m_LuxToColorProperties.SetTexture(ShaderProperties._Lux_Value_Buffer, valueRT);
        //m_LuxToColorProperties.SetVector(ShaderProperties._Lux_Value_RT_Scale, scaleFactor);
        CoreUtils.DrawFullScreen(cmd, _LuxPrintMaterial, colorRT, m_LuxToColorProperties, m_LuxToColorPassIndex);

        // 3) Calculate average lux values in a compute shader
        var sampleCount = LuxViewerController.SAMPLE_COUNT_PER_ROW;
        var tileCount = (valueRT.referenceSize.y + sampleCount - 1) / sampleCount;
        var gridSize = tileCount * sampleCount;
        var threadGroupX = tileCount;
        var threadGroupY = tileCount;
        m_BufferSize[0] = valueRT.referenceSize.x;
        m_BufferSize[1] = valueRT.referenceSize.y;
        m_StartOffset[0] = (valueRT.referenceSize.x - gridSize) / 2;
        m_StartOffset[1] = (valueRT.referenceSize.y - gridSize) / 2;
        cmd.SetComputeTextureParam(_LuxAverageComputeShader, 0, ShaderProperties._Lux_Value_Buffer, valueRT);
        cmd.SetComputeIntParams(_LuxAverageComputeShader, ShaderProperties._Buffer_Size, m_BufferSize);
        cmd.SetComputeIntParams(_LuxAverageComputeShader, ShaderProperties._Start_Offset, m_StartOffset);
        cmd.SetComputeTextureParam(_LuxAverageComputeShader, 0, ShaderProperties._Number_Texture, _NumberTexture);
        cmd.SetComputeTextureParam(_LuxAverageComputeShader, 0, ShaderProperties._Tile_Texture, _NumberTileTexture);
        cmd.SetComputeTextureParam(_LuxAverageComputeShader, 0, ShaderProperties._Test_Result, _LuxAverageResource.GetColorRT());
        cmd.DispatchCompute(_LuxAverageComputeShader, 0, threadGroupX, threadGroupY, 1);
    }

    protected override void Cleanup()
    {
        _LuxValueResource.ReleaseColorRT();
        _LuxColorResource.ReleaseColorRT();
        _LuxAverageResource.ReleaseColorRT();
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void ErrorMessage(string msg)
    {
        Debug.LogError($"[PrintLuxValueRenderPass] {msg}");
    }    

    private ShaderTagId[] m_ShaderTagIds = null;
    private int m_LuxValuePassIndex = -1;
    private int m_LuxToColorPassIndex = -1;
    private MaterialPropertyBlock m_LuxToColorProperties = null;
    private int[] m_BufferSize = null;
    private int[] m_StartOffset = null;

    private static class ShaderProperties
    {
        public static readonly int _Lux_Value_Buffer = Shader.PropertyToID("_Lux_Value_Buffer");
        public static readonly int _Lux_Value_RT_Scale = Shader.PropertyToID("_Lux_Value_RT_Scale");
        public static readonly int _Buffer_Size = Shader.PropertyToID("_Buffer_Size");
        public static readonly int _Start_Offset = Shader.PropertyToID("_Start_Offset");
        public static readonly int _Number_Texture = Shader.PropertyToID("_Number_Texture");
        public static readonly int _Tile_Texture = Shader.PropertyToID("_Tile_Texture");
        public static readonly int _Test_Result = Shader.PropertyToID("_Test_Result");
    }
}