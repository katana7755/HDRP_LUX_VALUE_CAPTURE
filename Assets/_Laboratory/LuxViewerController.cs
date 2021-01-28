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
    [SerializeField] private RenderingMode _RenderingMode = RenderingMode.AsColor;

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
    }

    private void OnEnable()
    {
        StartCoroutine(ProcessingGenerateTexturesSequence());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ValidateRenderingMode();
    }

    private void OnGUI()
    {
        if (GUI.Button(new Rect(50f, 50f, 200f, 50f), "Generate Lux Textures"))
        {
            RequestGenerateTextures();
        }

        if (GUI.Button(new Rect(260f, 50f, 70f, 50f), "Color"))
        {
            _RenderingMode = RenderingMode.AsColor;
            ValidateRenderingMode();
        }

        if (GUI.Button(new Rect(340f, 50f, 70f, 50f), "Average"))
        {
            _RenderingMode = RenderingMode.AsAverage;
            ValidateRenderingMode();
        }        
    }
#endif

    public void RequestGenerateTextures()
    {      
        if (m_GenerateTexturesFlag == true)
        {
            ErrorMessage("Don't request to generate textures while it's running");
            return;
        }

        m_GenerateTexturesFlag = true;  
    }

    public bool IsGeneratingTextures()
    {      
        return m_GenerateTexturesFlag;  
    }

    private IEnumerator ProcessingGenerateTexturesSequence()
    {        
        while (true)
        {
            if (m_GenerateTexturesFlag)
            {
                _CaptureToolsRoot.SetActive(true);

                yield return s_WaitForEndOfFrame;

#if UNITY_EDITOR
                while (UnityEditor.ShaderUtil.anythingCompiling)
                {
                    yield return null; 
                    yield return s_WaitForEndOfFrame;
                }
#endif

                var frameBuffer = RenderTexture.active;
                RenderTexture.active = frameBuffer;
                GenerateTexture(_LuxColorResource, ref m_ColorTexture);
                GenerateTexture(_LuxAverageResource, ref m_AverageTexture);
                ValidateRenderingMode();
                _CaptureToolsRoot.SetActive(false);
                m_GenerateTexturesFlag = false;                
            }
            else
            {                
                yield return null;
            }            
        }
    }

    private void GenerateTexture(SharedColorRTResource colorRTResource, ref Texture2D texture)
    {       
        if (texture != null)
        {       
            GameObject.Destroy(texture);
            texture = null;
        }

        RenderTexture.active = colorRTResource.GetColorRT();

        // [Warning]    
        // Here we need to consider when width is shorter than height...
        var colorRT = colorRTResource.GetColorRT();
        var actualWidth = colorRTResource.GetActualWidth();
        var actualHeight = colorRTResource.GetActualHeight();
        var textureSize = new Vector2Int(actualHeight, actualHeight);
        var center = new Vector2Int(actualWidth / 2, actualHeight / 2);
        var halfHeight = actualHeight / 2;
        texture = new Texture2D(actualHeight, actualHeight, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
        texture.ReadPixels(new Rect(center.x - halfHeight + ((actualWidth % 2 == 0) ? -1 : 0), 0f, actualHeight, actualHeight), 0, 0);
        texture.Apply();         
    }

    private void ValidateRenderingMode()
    {
        if (m_Renderer == null)
        {
            return;
        }

        switch (_RenderingMode)
        {
            case RenderingMode.AsColor:
                m_Renderer.material.SetTexture(MaterialProperties._UnlitColorMap, m_ColorTexture);        
                break;
            case RenderingMode.AsAverage:
                m_Renderer.material.SetTexture(MaterialProperties._UnlitColorMap, m_AverageTexture);        
                break;
        }        
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void ErrorMessage(string msg)
    {
        Debug.LogError($"[LuxViewerController] {msg}");
    }     

    private bool m_GenerateTexturesFlag = false;
    private Renderer m_Renderer = null;
    private Texture2D m_ColorTexture = null;
    private Texture2D m_AverageTexture = null;

    public const int SAMPLE_COUNT_PER_ROW = 32;

    private static readonly WaitForEndOfFrame s_WaitForEndOfFrame = new WaitForEndOfFrame();

    private enum RenderingMode
    {
        AsColor,
        AsAverage,        
    }

    private static class MaterialProperties
    {
        public static readonly int _UnlitColorMap = Shader.PropertyToID("_UnlitColorMap");
    }
}
