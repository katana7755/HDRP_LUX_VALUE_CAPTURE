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
        var newActualWidth = -1;
        var newActualHeigth = -1;

        switch (m_RequestedState)
        {
            case RequestedState.Scale:
                {
                    newActualWidth = Mathf.Max(Mathf.RoundToInt(RTHandles.maxWidth * m_RequestedScale.x), 1);
                    newActualHeigth = Mathf.Max(Mathf.RoundToInt(RTHandles.maxHeight * m_RequestedScale.y), 1);
                }
                break;

            case RequestedState.Size:
                {
                    var ratio = (float)actualWidth / actualHeight;
                    newActualWidth = Mathf.CeilToInt(m_RequestedSize.y * ratio);
                    newActualHeigth = m_RequestedSize.y;
                }
                break;
        }

        if (m_ActualWidth != newActualWidth || m_ActualHeight != newActualHeigth)
        {
            ReleaseColorRT();
            m_ActualWidth = newActualWidth;
            m_ActualHeight = newActualHeigth;
            m_ColorRT = RTHandles.Alloc(m_ActualWidth, m_ActualHeight, 1, dimension: TextureDimension.Tex2D, enableRandomWrite: m_RequestedEnableRandomWrite, colorFormat: _ColorRTFormat, autoGenerateMips: false, useDynamicScale: false, name: name);
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
