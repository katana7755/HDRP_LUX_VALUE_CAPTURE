using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[CreateAssetMenu(fileName = "New SharedColorRTResource.asset", menuName = "ScriptableObjects/Lux Viewer/Create SharedColorRTResource Asset")]
public class SharedColorRTResource : ScriptableObject
{
    [SerializeField] private GraphicsFormat _ColorRTFormat = GraphicsFormat.R16G16_SFloat;

    private void OnDisable()
    {
        ReleaseColorRT();
    }

    public void RequestColorRT(Vector2 scale, bool enableRandomWrite = false)
    {
        m_RequestedScale = scale;
        m_RequestedEnableRandomWrite = enableRandomWrite;
        m_RequestedState = RequestedState.Scale;
    }

    public void RequestColorRT(int width, int height, bool enableRandomWrite = false)
    {        
        m_RequestedSize = new Vector2Int(width, height);
        m_RequestedEnableRandomWrite = enableRandomWrite;
        m_RequestedState = RequestedState.Size;
    }

    public void AllocateColorRT(int actualWidth, int actualHeight)
    {   
        switch (m_RequestedState)
        {
            case RequestedState.Scale:
                {
                    ReleaseColorRT();
                    m_ColorRT = RTHandles.Alloc(m_RequestedScale, 1, dimension: TextureDimension.Tex2D, enableRandomWrite: m_RequestedEnableRandomWrite, colorFormat: _ColorRTFormat, autoGenerateMips: false, useDynamicScale: false, name: name);
                    
                    var size = m_ColorRT.GetScaledSize(new Vector2Int(RTHandles.maxWidth, RTHandles.maxHeight));
                    m_ActualWidth = size.x;
                    m_ActualHeight = size.y;
                }
                break;

            case RequestedState.Size:
                {
                    ReleaseColorRT();

                    // [Warning]    
                    // Here we need to consider when width is shorter than height...                        
                    // float ratio = (float)m_RequestedSize.y / RTHandles.maxHeight;
                    // m_ColorRT = RTHandles.Alloc(Vector2.one * ratio, TextureXR.slices, dimension: TextureXR.dimension, colorFormat: _ColorRTFormat, autoGenerateMips: false, useDynamicScale: false, name: name);

                    // var size = m_ColorRT.GetScaledSize(new Vector2Int(RTHandles.maxWidth, RTHandles.maxHeight));
                    // m_ActualWidth = size.x;
                    // m_ActualHeight = size.y;

                    var ratio = (float)actualWidth / actualHeight;
                    m_ActualWidth = Mathf.CeilToInt(m_RequestedSize.y * ratio);
                    m_ActualHeight = m_RequestedSize.y;
                    m_ColorRT = RTHandles.Alloc(m_ActualWidth, m_ActualHeight, 1, dimension: TextureDimension.Tex2D, enableRandomWrite: m_RequestedEnableRandomWrite, colorFormat: _ColorRTFormat, autoGenerateMips: false, useDynamicScale: false, name: name);
                }
                break;
        }
    }

    public void ReleaseColorRT()
    {
        if (m_ColorRT == null)
        {
            return;
        }

        m_ColorRT.Release();        
        m_ColorRT = null;
    }

    public bool IsValid()
    {
        return m_ColorRT != null;
    }

    public RTHandle GetColorRT()
    {
        return m_ColorRT;
    }

    public int GetActualWidth()
    {
        return m_ActualWidth;
    }

    public int GetActualHeight()
    {
        return m_ActualHeight;
    }

    private RTHandle m_ColorRT;
    private Vector2 m_RequestedScale = Vector2.one;
    private Vector2Int m_RequestedSize = Vector2Int.zero;
    private bool m_RequestedEnableRandomWrite = false;
    private RequestedState m_RequestedState = RequestedState.None;
    private int m_ActualWidth = -1;
    private int m_ActualHeight = -1;

    private enum RequestedState
    {
        None,
        Scale,
        Size,
    }
}
