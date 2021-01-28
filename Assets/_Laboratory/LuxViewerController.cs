using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class LuxViewerController : MonoBehaviour
{
    [SerializeField] private Camera _TopViewCamera = null;
    [SerializeField] private GameObject _QuadViewerGO = null;
    [SerializeField] private GameObject _QuadBakerGO = null;
    [SerializeField] private SharedColorRTResource _LuxValueResource = null;
    [SerializeField] private SharedColorRTResource _LuxColorResource = null;
    [SerializeField] private SharedColorRTResource _LuxAverageResource = null;
    [SerializeField] private GameObject _CaptureToolsRoot = null;
    [SerializeField] private RenderingMode _RenderingMode = RenderingMode.AsColor;
    [SerializeField] private QuadMode _QuadMode = QuadMode.Viwerer;
    [SerializeField] private int _QuadCountX = 1;
    [SerializeField] private int _QuadCountY = 1;

    private void Start()
    {       
        m_QuadRenderingData = new QuadRenderingData(_QuadBakerGO);
        ValidateGraphicsResources();
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
        if (transform.parent != null)
        {
            ErrorMessage("The controller should be set as a root GameObject meaning \"let Transform not have a parent\"");
        }

        if (Vector3.Distance(transform.localScale, Vector3.one) > Mathf.Epsilon)
        {
            ErrorMessage("The controller should have identity scale meaning (1.0f, 1.0f, 1.0f)");
        }

        ValidateRenderingMode();
        ValidateQuadMode();
    }

    private void OnGUI()
    {
        if (!UnityEditor.EditorApplication.isPlaying)
        {
            return;
        }

        if (GUI.Button(new Rect(50f, 50f, 70f, 50f), "Viewer"))
        {
            _QuadMode = QuadMode.Viwerer;
            ValidateQuadMode();
        }                

        if (GUI.Button(new Rect(130f, 50f, 70f, 50f), "Baker"))
        {
            _QuadMode = QuadMode.Baker;
            ValidateQuadMode();            
        } 

        switch(_QuadMode)
        {
            case QuadMode.Viwerer:
                UnityEditor.EditorGUI.HelpBox(new Rect(50f, 120f, 400f, 50f), "In Viewer mode color rendering is only supported", UnityEditor.MessageType.Info);
                break;
            case QuadMode.Baker:
                if (GUI.Button(new Rect(50f, 120f, 200f, 50f), "Generate Lux Textures"))
                {
                    RequestGenerateTextures();
                }

                if (GUI.Button(new Rect(260f, 120f, 70f, 50f), "Color"))
                {
                    _RenderingMode = RenderingMode.AsColor;
                    ValidateRenderingMode();
                }

                if (GUI.Button(new Rect(340f, 120f, 70f, 50f), "Average"))
                {
                    _RenderingMode = RenderingMode.AsAverage;
                    ValidateRenderingMode();
                }                                  
                break;
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
                GenerateTexture(_LuxColorResource, ref m_QuadRenderingData._ColorTexture);
                GenerateTexture(_LuxAverageResource, ref m_QuadRenderingData._AverageTexture);
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

    private void ValidateGraphicsResources()
    {
        // [Warning!!!]
        // The plane can be horizontally longer or vertically longer...but for the simplicity, I only take care of sqaure cases...
        var width = Mathf.RoundToInt(QUAD_SIZE * SAMPLE_COUNT_PER_ROW);
        var height = Mathf.RoundToInt(QUAD_SIZE * SAMPLE_COUNT_PER_ROW);
        _LuxValueResource.RequestColorRT(width, height);
        _LuxColorResource.RequestColorRT(width, height);
        _LuxAverageResource.RequestColorRT(width, height, true);
    }

    private void ValidateRenderingMode()
    {
#if UNITY_EDITOR
        if (!UnityEditor.EditorApplication.isPlaying)
        {
            return;
        }
#endif

        if (m_QuadRenderingData == null)
        {
            return;
        }

        m_QuadRenderingData.SetRenderingMode(_RenderingMode);
    }

    private void ValidateQuadMode()
    {
        switch (_QuadMode)
        {
            case QuadMode.Viwerer:
                _QuadViewerGO.SetActive(true);
                _QuadBakerGO.SetActive(false);
                break;
            case QuadMode.Baker:
                _QuadViewerGO.SetActive(false);
                _QuadBakerGO.SetActive(true);
                break;
        }
    }    

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void ErrorMessage(string msg)
    {
        Debug.LogError($"[LuxViewerController] {msg}");
    }     

    private bool m_GenerateTexturesFlag = false;
    private QuadRenderingData m_QuadRenderingData = null;

    public const int SAMPLE_COUNT_PER_ROW = 32;
    private const float QUAD_SIZE = 10f; // 10 meter

    private static readonly WaitForEndOfFrame s_WaitForEndOfFrame = new WaitForEndOfFrame();

    private enum QuadMode
    {
        Viwerer,
        Baker,
    }

    private enum RenderingMode
    {
        AsColor,
        AsAverage,        
    }

    private static class MaterialProperties
    {
        public static readonly int _UnlitColorMap = Shader.PropertyToID("_UnlitColorMap");
    }

    private class QuadRenderingData
    {        
        public Renderer _Renderer = null;
        public Texture2D _ColorTexture = null;
        public Texture2D _AverageTexture = null;

        public QuadRenderingData(GameObject go)
        {
            _Renderer = go.GetComponent<Renderer>();            
        }

        public void SetRenderingMode(RenderingMode renderingMode)
        {           
            switch (renderingMode)
            {
                case RenderingMode.AsColor:
                    _Renderer.material.SetTexture(MaterialProperties._UnlitColorMap, _ColorTexture);        
                    break;
                case RenderingMode.AsAverage:
                    _Renderer.material.SetTexture(MaterialProperties._UnlitColorMap, _AverageTexture);        
                    break;
            }                     
        }
    }
}
