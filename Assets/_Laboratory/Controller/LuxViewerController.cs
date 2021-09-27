using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

// [UnityEditor.InitializeOnLoad]
// public static class ExternalGPUCapturer
// {    
//     static ExternalGPUCapturer()
//     {
//         UnityEditor.EditorApplication.playModeStateChanged += StateChanged;
//     }

//     private static void StateChanged(UnityEditor.PlayModeStateChange change)
//     {        
//         switch (change)
//         {
//             case UnityEditor.PlayModeStateChange.ExitingPlayMode:
//                 {
//                     UnityEngine.Experimental.Rendering.ExternalGPUProfiler.BeginGPUCapture();
//                 }
//                 break;

//             case UnityEditor.PlayModeStateChange.EnteredEditMode:
//                 {
//                     UnityEngine.Experimental.Rendering.ExternalGPUProfiler.EndGPUCapture();
//                 }
//                 break;
//         }
//     }
// }

[ExecuteAlways]
public class LuxViewerController : MonoBehaviour
{
    [SerializeField] private GameObject _QuadViewerGO = null;
    [SerializeField] private GameObject[] _QuadBakerGOs = null;
    [SerializeField] private GameObject _QuadBigBakerGO = null;
    [SerializeField] private SharedColorRTResource _LuxValueResource = null;
    [SerializeField] private SharedColorRTResource _LuxColorResource = null;
    [SerializeField] private SharedColorRTResource _LuxAverageResource = null;
    [SerializeField] private GameObject _CaptureToolsRoot = null;
    [SerializeField] private RenderingMode _RenderingMode = RenderingMode.AsColor;
    [SerializeField] private QuadMode _QuadMode = QuadMode.Viwerer;
    [SerializeField] private ComputeShader _CopyToTextureComputeShader = null;
    [SerializeField] private bool _IsBigBakerMode = true;
    [SerializeField] private bool _IsTextureIDUnique = true;
    [SerializeField] private string _FilePathForGeneratedTextures = "../Generated Textures";

    private void Start()
    {    
#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPlaying == true)
        {
#endif

        m_QuadBakerInstances = new QuadBakerInstance[_QuadBakerGOs.Length];

        for (var i = 0; i < _QuadBakerGOs.Length; ++i)
        {
            m_QuadBakerInstances[i] = new QuadBakerInstance(_QuadBakerGOs[i]);
        }

        if (_IsBigBakerMode)
        {
            m_QuadBigBakerInstance = new QuadBakerInstance(_QuadBigBakerGO);
        }        

        ValidateGraphicsResources();

#if UNITY_EDITOR
        }   
#endif                
    }

    private void OnEnable()
    {
        FindStaticSky();
        TurnOffStaticSKy();     

#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPlaying == true)
        {
#endif

        StartCoroutine(ProcessingEnsureToTurnOffStaticSky());
        StartCoroutine(ProcessingGenerateTexturesSequence());
        StartCoroutine(ProcessingValidateCamera());

#if UNITY_EDITOR
        }   
#endif        
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPlaying == true)
        {
#endif

        StopAllCoroutines();

#if UNITY_EDITOR
        }   
#endif        
        
        TurnOnStaticSky();
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

        if (_CaptureToolsRoot.GetComponent<Camera>() == null)
        {
            ErrorMessage("CaptureToolsRoot must conatin Camera component");
        }

        ValidateRenderingMode();
        ValidateQuadMode();
    }

    private void OnGUI()
    {
        if (UnityEditor.EditorApplication.isPlaying == false)
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
                CheckBakedTextureDirectoryExist();
                _CaptureToolsRoot.SetActive(true);

                for (var i = 0; i < m_QuadBakerInstances.Length; ++i)
                {           
                    var bakerInstance = m_QuadBakerInstances[i];
                    var x = bakerInstance._Transform.position.x;
                    var y = _CaptureToolsRoot.transform.position.y;
                    var z = bakerInstance._Transform.position.z;
                    _CaptureToolsRoot.transform.position = new Vector3(x, y, z);

                    yield return s_WaitForEndOfFrame;

    #if UNITY_EDITOR
                    while (UnityEditor.ShaderUtil.anythingCompiling)
                    {
                        yield return null; 
                        yield return s_WaitForEndOfFrame;
                    }
    #endif

                    if (_IsBigBakerMode)
                    {
                        Vector3 diff = (bakerInstance._Transform.position - m_QuadBigBakerInstance._Transform.position) * SAMPLE_COUNT_PER_ROW;
                        Vector2 sourcePos = new Vector2(diff.x, diff.z);

                        yield return CopyTextureByComputeShader(_LuxColorResource, m_QuadBigBakerInstance.GetColorRenderTexture(), sourcePos);
                        yield return CopyTextureByComputeShader(_LuxAverageResource, m_QuadBigBakerInstance.GetAverageRenderTexture(), sourcePos);
                    }
                    else
                    {
                        yield return CopyTextureByComputeShader(_LuxColorResource, bakerInstance.GetColorRenderTexture(), Vector2.zero);
                        yield return CopyTextureByComputeShader(_LuxAverageResource, bakerInstance.GetAverageRenderTexture(), Vector2.zero);

                        ExportToPNG(bakerInstance.GetColorRenderTexture(), GetBakedTextureFilePath($"{bakerInstance.GetName()}_Color"));
                        ExportToPNG(bakerInstance.GetAverageRenderTexture(), GetBakedTextureFilePath($"{bakerInstance.GetName()}_Average"));                        
                    }
                }

                if (_IsBigBakerMode)
                {
                    ExportToPNG(m_QuadBigBakerInstance.GetColorRenderTexture(), GetBakedTextureFilePath($"{m_QuadBigBakerInstance.GetName()}_Color"));
                    ExportToPNG(m_QuadBigBakerInstance.GetAverageRenderTexture(), GetBakedTextureFilePath($"{m_QuadBigBakerInstance.GetName()}_Average"));                                            
                }

                _CaptureToolsRoot.SetActive(false);
                ValidateRenderingMode();
                m_GenerateTexturesFlag = false;                
            }
            else
            {                
                yield return null;
            }            
        }
    }

    private IEnumerator ProcessingValidateCamera()
    {
        var captureCamera = _CaptureToolsRoot.GetComponent<Camera>();   
        var width = -1;
        var height = -1;

        while (true)
        {
            while (width == captureCamera.pixelWidth && height == captureCamera.pixelHeight)
            {
                yield return null;
            }

            width = captureCamera.pixelWidth;
            height = captureCamera.pixelHeight;
            captureCamera.orthographicSize = (width < height) ? QUAD_SIZE * 0.5f * height / width : QUAD_SIZE * 0.5f;
        }
    }

    private AsyncGPUReadbackRequest CopyTextureByComputeShader(SharedColorRTResource colorRTResource, Texture destTexture, Vector2 sourcePos)
    {
        Vector2Int sourceIntSize = new Vector2Int(colorRTResource.GetActualWidth(), colorRTResource.GetActualHeight());
        Vector2 sourceSize = new Vector2(sourceIntSize.x, sourceIntSize.y);
        Vector2 sourceOrigin = sourcePos - sourceSize * 0.5f;
        Vector2 sourceClampedSize = Vector2.zero;

        if (sourceIntSize.x >= sourceIntSize.y)
        {            
            sourceClampedSize = new Vector2(sourceIntSize.y, sourceIntSize.y);
        }
        else
        {
            sourceClampedSize = new Vector2(sourceIntSize.x, sourceIntSize.x);
        }
                
        Vector2 sourceStart = sourcePos - sourceClampedSize * 0.5f;
        Vector2 sourceEnd = sourceStart + sourceClampedSize;

        Vector2 destPos = Vector2.zero;
        Vector2Int destIntSize = new Vector2Int(destTexture.width, destTexture.height);
        Vector2 destSize = new Vector2(destIntSize.x, destIntSize.y);
        Vector2 destOrigin = destPos - destSize * 0.5f;
        Vector2 destStart = destOrigin;
        Vector2 destEnd = destOrigin + destSize;

        Vector2 aabbMin = Vector2.Max(sourceStart, destStart);
        Vector2 aabbMax = Vector2.Min(sourceEnd, destEnd);
        Vector2 aabbSize = aabbMax - aabbMin;

        if (aabbSize.x < 0f || aabbSize.y < 0f || Mathf.Approximately(aabbSize.x * aabbSize.y, 0f))
        {
            Debug.LogError("invalid AABB bound");
        }
        
        sourceStart = aabbMin - sourceOrigin;
        sourceEnd = aabbMax - sourceOrigin;
        destStart = aabbMin - destOrigin;
        destEnd = aabbMax - destOrigin;

        _CopyToTextureComputeShader.SetTexture(0, MaterialProperties._InputTexture, colorRTResource.GetColorRT());
        _CopyToTextureComputeShader.SetVector(MaterialProperties._InputInfo, new Vector4(sourceStart.x, sourceStart.y, sourceEnd.x, sourceEnd.y));
        _CopyToTextureComputeShader.SetVector(MaterialProperties._InputExtraInfo, new Vector4(sourceStart.x - Mathf.Floor(sourceStart.x), sourceStart.y - Mathf.Floor(sourceStart.y), sourceSize.x, sourceSize.y));
        _CopyToTextureComputeShader.SetTexture(0, MaterialProperties._OutputTexture, destTexture);
        _CopyToTextureComputeShader.SetVector(MaterialProperties._OutputInfo, new Vector4(destStart.x, destStart.y, destEnd.x, destEnd.y));
        _CopyToTextureComputeShader.Dispatch(0, ((int)aabbSize.x + 7) / 8, ((int)aabbSize.y + 7) / 8, 1);

        return AsyncGPUReadback.Request(destTexture, 0, null);
    }

    private void CheckBakedTextureDirectoryExist()
    {        
        if (!Directory.Exists(_FilePathForGeneratedTextures))
        {
            Directory.CreateDirectory(_FilePathForGeneratedTextures);
        }
    }

    private string GetBakedTextureFilePath(string textureName)
    {
        if (_IsTextureIDUnique)        
        {            
            return $"{_FilePathForGeneratedTextures}/{textureName}-{Guid.NewGuid()}.png";
        }

        return $"{_FilePathForGeneratedTextures}/{textureName}.png";
    }

    private void ExportToPNG(Texture inputTexture, string filePath)
    {       
        RenderTexture inputRT = inputTexture as RenderTexture;

        if (inputRT == null)
        {
            return;
        }

        var oldRT = RenderTexture.active;
        RenderTexture.active = inputRT;

        var tex2D = new Texture2D(inputRT.width, inputRT.height, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
        tex2D.ReadPixels(new Rect(0f, 0f, inputRT.width, inputRT.height), 0, 0);
        tex2D.Apply();     
        RenderTexture.active = oldRT;
        File.WriteAllBytes(filePath, tex2D.EncodeToPNG());
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

        if (_IsBigBakerMode)
        {
            if (m_QuadBigBakerInstance == null)
            {
                return;
            }

            m_QuadBigBakerInstance.SetRenderingMode(_RenderingMode);
        }
        else
        {
            if (m_QuadBakerInstances == null || m_QuadBakerInstances.Length <= 0)
            {
                return;
            }

            for (var i = 0; i < m_QuadBakerInstances.Length; ++i)
            {
                m_QuadBakerInstances[i].SetRenderingMode(_RenderingMode);
            }
        }
    }

    private void ValidateQuadMode()
    {
        switch (_QuadMode)
        {
            case QuadMode.Viwerer:
                _QuadViewerGO.SetActive(true);

#if UNITY_EDITOR
                for (var i = 0; i < _QuadBakerGOs.Length; ++i)
                {
                    _QuadBakerGOs[i].SetActive(false);
                }    

                _QuadBigBakerGO.SetActive(false);            
#else
                for (var i = 0; i < m_QuadBakerInstances.Length; ++i)
                {
                    m_QuadBakerInstances[i].SetActive(false);
                }                

                m_QuadBigBakerInstance.SetActive(false);
#endif
                break;
            case QuadMode.Baker:
                _QuadViewerGO.SetActive(false);

#if UNITY_EDITOR
                if (_IsBigBakerMode)
                {
                    _QuadBigBakerGO.SetActive(true);
                }
                else
                {
                    for (var i = 0; i < _QuadBakerGOs.Length; ++i)
                    {
                        _QuadBakerGOs[i].SetActive(true);
                    }                      
                }
#else
                if (_IsBigBakerMode)
                {
                    m_QuadBigBakerInstance.SetActive(true);
                }
                else
                {
                    for (var i = 0; i < m_QuadBakerInstances.Length; ++i)
                    {
                        m_QuadBakerInstances[i].SetActive(true);
                    }                       
                }
#endif                
                break;
        }
    }    

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void ErrorMessage(string msg)
    {
        Debug.LogError($"[LuxViewerController] {msg}");
    }     

    private void FindStaticSky()
    {        
        m_StaticSkyComponent = null;
        m_StaticSkyUniqueID = 0;

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        if (!scene.isLoaded)
        {
            return;
        }

        foreach (var go in scene.GetRootGameObjects())
        {
            m_StaticSkyComponent = go.GetComponent<UnityEngine.Rendering.HighDefinition.StaticLightingSky>();

            if (m_StaticSkyComponent != null)
            {
                break;
            }
        }

        if (m_StaticSkyComponent == null)
        {
            var candidates = GameObject.FindObjectsOfType<UnityEngine.Rendering.HighDefinition.StaticLightingSky>().Where(sls => sls.gameObject.scene == scene);

            if (candidates.Count() > 0)
            {
                m_StaticSkyComponent = candidates.First();
            }                
        }

        if (m_StaticSkyComponent != null)
        {
            m_StaticSkyUniqueID = m_StaticSkyComponent.staticLightingSkyUniqueID;
        }
    }

    private void TurnOffStaticSKy()
    {      
        if (m_StaticSkyComponent == null)  
        {
            return;
        }

        m_StaticSkyComponent.staticLightingSkyUniqueID = 0;
    }

    private void TurnOnStaticSky()
    {        
        if (m_StaticSkyComponent == null)  
        {
            return;
        }

        m_StaticSkyComponent.staticLightingSkyUniqueID = m_StaticSkyUniqueID;        
    }   

    private IEnumerator ProcessingEnsureToTurnOffStaticSky()
    {
        while (m_StaticSkyComponent == null)
        {
            yield return null;

            FindStaticSky();
            TurnOffStaticSKy();
        }
    }

    private bool m_GenerateTexturesFlag = false;
    private QuadBakerInstance[] m_QuadBakerInstances = null;
    private QuadBakerInstance m_QuadBigBakerInstance = null;
    private UnityEngine.Rendering.HighDefinition.StaticLightingSky m_StaticSkyComponent = null;
    private int m_StaticSkyUniqueID = 0;

    public const int SAMPLE_COUNT_PER_ROW = 32;
    private const float QUAD_SIZE = 10f; // 10 meter

    private static readonly WaitForSeconds s_WaitForSeconds = new WaitForSeconds(0.333f);
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
        public static readonly int _InputTexture = Shader.PropertyToID("_InputTexture");
        public static readonly int _InputInfo = Shader.PropertyToID("_InputInfo");
        public static readonly int _InputExtraInfo = Shader.PropertyToID("_InputExtraInfo");
        public static readonly int _OutputTexture = Shader.PropertyToID("_OutputTexture");
        public static readonly int _OutputInfo = Shader.PropertyToID("_OutputInfo");
    }

    private class QuadBakerInstance
    {   
        public Transform _Transform
        {
            get
            {
                return m_GO.transform;
            }
        }

        public QuadBakerInstance(GameObject go)
        {
            m_GO = go;
            m_Renderer = m_GO.GetComponent<Renderer>();            
        }

        public string GetName()
        {
            return m_GO.name;
        }

        public void SetRenderingMode(RenderingMode renderingMode)
        {           
            switch (renderingMode)
            {
                case RenderingMode.AsColor:
                    m_Renderer.material.SetTexture(MaterialProperties._UnlitColorMap, m_ColorTexture);        
                    break;
                case RenderingMode.AsAverage:
                    m_Renderer.material.SetTexture(MaterialProperties._UnlitColorMap, m_AverageTexture);        
                    break;
            }                     
        }

        public void SetActive(bool active)
        {
            m_GO.SetActive(active);
        }

        public Texture GetColorRenderTexture()
        {
            CheckTextureValid(ref m_ColorTexture);
            return m_ColorTexture;
        }

        public Texture GetAverageRenderTexture()
        {
            CheckTextureValid(ref m_AverageTexture);
            return m_AverageTexture;
        }

        private void CheckTextureValid(ref Texture texture)
        {            
            if (texture != null && !(texture is RenderTexture))
            {  
                GameObject.Destroy(texture);
                texture = null;
            }

            Vector3 lossyScale = _Transform.lossyScale;
            int width = Mathf.CeilToInt(lossyScale.x * LuxViewerController.SAMPLE_COUNT_PER_ROW);
            int height = Mathf.CeilToInt(lossyScale.y * LuxViewerController.SAMPLE_COUNT_PER_ROW);

            if (texture != null && (texture.width != width || texture.height != height))
            {       
                GameObject.Destroy(texture);
                texture = null;                         
            }            

            if (texture == null)
            {
                var rt = new RenderTexture(width, height, 1);
                rt.enableRandomWrite = true;
                texture = rt;
            }
        }

        private GameObject m_GO = null;     
        private Renderer m_Renderer = null;      
        private Texture m_ColorTexture = null;
        private Texture m_AverageTexture = null;          
    }
}
