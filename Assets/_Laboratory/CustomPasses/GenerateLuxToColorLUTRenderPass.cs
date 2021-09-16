using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

class GenerateLuxToColorLUTRenderPass : CustomPass
{
    [Header("[GenerateLuxToColorLUTRenderPass]")]
    [SerializeField] private LUTItem[] _LUTItems = null;

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        if (_LUTItems == null)
        {
            ErrorMessage("No LUT items are defined");
            return;
        }

        Cleanup();        
        m_ElementStride = Marshal.SizeOf<LUTItem>();
        m_ElementCount = _LUTItems.Length;
        m_ComputeBuffer = new ComputeBuffer(m_ElementCount, m_ElementStride);
        m_ComputeBuffer.SetData(_LUTItems);
    }

    protected override void Execute(ScriptableRenderContext renderContext, CommandBuffer cmd, HDCamera hdCamera, CullingResults cullingResult)
    {
        if (m_ComputeBuffer == null)
        {
            return;                       
        }        

#if UNITY_EDITOR
        m_ComputeBuffer.SetData(_LUTItems);
#endif

        cmd.SetGlobalInt(ShaderProperties._LuxToColor_Count, m_ElementCount);
        cmd.SetGlobalBuffer(ShaderProperties._LuxToColor_Buffer, m_ComputeBuffer);
    }

    protected override void Cleanup()
    {
        if (m_ComputeBuffer != null && m_ComputeBuffer.IsValid())
        {
            m_ComputeBuffer.Release();
            m_ComputeBuffer = null;
        }
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

    private int m_ElementStride = -1;
    private int m_ElementCount = -1;
    private ComputeBuffer m_ComputeBuffer = null;

    [System.Serializable]
    private struct LUTItem
    {        
        public Color _Color;
        public float _UpperLimit;
    }

    private static class ShaderProperties
    {
        public static readonly int _LuxToColor_Count = Shader.PropertyToID("_LuxToColor_Count");
        public static readonly int _LuxToColor_Buffer = Shader.PropertyToID("_LuxToColor_Buffer");
    }
}