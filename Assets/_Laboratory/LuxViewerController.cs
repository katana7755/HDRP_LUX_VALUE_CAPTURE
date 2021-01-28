using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class LuxViewerController : MonoBehaviour
{
    [SerializeField] private SharedColorRTResource _LuxValueResource = null;
    [SerializeField] private SharedColorRTResource _LuxColorResource = null;
    [SerializeField] private SharedColorRTResource _LuxAverageResource = null;
    [SerializeField] private GameObject _CaptureToolsRoot = null;
    [SerializeField] private bool _TestFlag = false;

    private void Start()
    {        
        m_Renderer = GetComponent<Renderer>();
        
        // [Warning!!!]
        // The plane can be horizontally longer or vertically longer...but for the simplicity, I only take care of sqaure cases...
        var width = Mathf.RoundToInt(m_Renderer.bounds.extents.x * 2.0f * SAMPLE_COUNT_PER_ROW);
        var height = Mathf.RoundToInt(m_Renderer.bounds.extents.z * 2.0f * SAMPLE_COUNT_PER_ROW);
        _LuxValueResource.RequestColorRT(width, height);
        _LuxColorResource.RequestColorRT(width, height);
        _LuxAverageResource.RequestColorRT(width, height, true);

        // [Warning!!!]
        // With scale setting, if you use RenderTexture.active to apply a texture HDRP RTHandleSystem expends maxWidth...Still couldn't find the exact reason...
        // But need to find out the reason for rendering correct result from the top view camera...
        // _LuxValueResources.RequestColorRT(Vector2.one);
        // _LuxColorResources.RequestColorRT(Vector2.one);
    }

    private void OnEnable()
    {
        StartCoroutine(ProcessingCaptureSequence());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private void Update()
    {
        if (m_UpdateTexturesFlag)
        {
            return;
        }

        if (_TestFlag)
        {
            m_UpdateTexturesFlag = true;
            _TestFlag = false;
        }
    }

    public bool GetUpdateTexturesFlag()
    {      
        return m_UpdateTexturesFlag;  
    }

    private IEnumerator ProcessingCaptureSequence()
    {        
        while (true)
        {
            if (m_UpdateTexturesFlag)
            {
                _CaptureToolsRoot.SetActive(true);

                yield return s_WaitForEndOfFrame;

                var frameBuffer = RenderTexture.active;
                GenerateNumberTexture();
                RenderTexture.active = frameBuffer;
                _CaptureToolsRoot.SetActive(false);
                m_UpdateTexturesFlag = false;
            }
            else
            {                
                yield return null;
            }            
        }
    }

    private void GenerateColorTexture()
    {   
        RenderTexture.active = _LuxColorResource.GetColorRT();

        // [Warning]    
        // Here we need to consider when width is shorter than height...
        var colorRT = _LuxColorResource.GetColorRT();
        var actualWidth = _LuxColorResource.GetActualWidth();
        var actualHeight = _LuxColorResource.GetActualHeight();
        var textureSize = new Vector2Int(actualHeight, actualHeight);
        var center = new Vector2Int(actualWidth / 2, actualHeight / 2);
        var halfHeight = actualHeight / 2;
        var texture = new Texture2D(actualHeight, actualHeight, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
        texture.ReadPixels(new Rect(center.x - halfHeight + ((actualWidth % 2 == 0) ? -1 : 0), 0f, actualHeight, actualHeight), 0, 0);
        texture.Apply();
        m_Renderer.material.SetTexture("_UnlitColorMap", texture);
    }

    private void GenerateNumberTexture()
    {   
        RenderTexture.active = _LuxAverageResource.GetColorRT();

        // [Warning]    
        // Here we need to consider when width is shorter than height...
        var colorRT = _LuxAverageResource.GetColorRT();
        var actualWidth = _LuxAverageResource.GetActualWidth();
        var actualHeight = _LuxAverageResource.GetActualHeight();
        var textureSize = new Vector2Int(actualHeight, actualHeight);
        var center = new Vector2Int(actualWidth / 2, actualHeight / 2);
        var halfHeight = actualHeight / 2;
        var texture = new Texture2D(actualHeight, actualHeight, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
        texture.ReadPixels(new Rect(center.x - halfHeight + ((actualWidth % 2 == 0) ? -1 : 0), 0f, actualHeight, actualHeight), 0, 0);
        texture.Apply();
        m_Renderer.material.SetTexture("_UnlitColorMap", texture);        
    }

    private bool m_UpdateTexturesFlag = false;
    private Renderer m_Renderer = null;

    public const int SAMPLE_COUNT_PER_ROW = 32;

    private static readonly WaitForEndOfFrame s_WaitForEndOfFrame = new WaitForEndOfFrame();
}
