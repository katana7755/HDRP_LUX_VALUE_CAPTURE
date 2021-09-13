using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class RuntimeIESLight : MonoBehaviour
{
    [Header("[Inputs]")]
    public TextAsset _InputTextAsset;
    public float _InputSpotAngle = 120f;
    public IESResolution _InputResolution = IESResolution.IESResolution128;
    public bool _InputApplyLightAttenuation = true;
    public float _InputAimAxisRotation = -90f;
    public IESLightType _InputLightType = IESLightType.Point;
    public bool _InputUseIESMaximumIntensity = true;

    [Header("[Outputs]")]
    public Texture2D _OutputCookie2D;
    public Cubemap _OutputCookieCubemap;

    private void OnGUI()
    {
        if (GUI.Button(new Rect(10f, 10f, 100f, 50f), "Execute"))
        {            
            Execute();
        }
    }

    private void Execute()
    {        
        var engine = new RumtimeIESProfile.IESEngine();
        var errorMessage = engine.ReadFile(Application.dataPath + "/../" + UnityEditor.AssetDatabase.GetAssetPath(_InputTextAsset));

        if (!string.IsNullOrEmpty(errorMessage))
        {
            Debug.LogError(errorMessage);
            return;
        }

        _OutputCookie2D = engine.Generate2DCookie(_InputSpotAngle, (int)_InputResolution, _InputApplyLightAttenuation);
        _OutputCookieCubemap = engine.GenerateCubeCookie((int)_InputResolution);
        transform.localEulerAngles = new Vector3(90f, 0f, _InputAimAxisRotation);

        Light light = gameObject.GetComponent<Light>();

        if (light == null)
        {
            light = gameObject.AddComponent<Light>();
        }

        light.type = (_InputLightType == IESLightType.Point) ? LightType.Point : LightType.Spot;
        light.intensity = 1f;  // would need a better intensity value formula
        light.range = 10f; // would need a better range value formula
        light.spotAngle = _InputSpotAngle;
        light.range = 100f;

        (var IESMaximumIntensity, var IESMaximumIntensityUnit) = engine.GetMaximumIntensity();

        HDLightTypeAndShape hdLightTypeAndShape = (light.type == LightType.Point) ? HDLightTypeAndShape.Point : HDLightTypeAndShape.ConeSpot;
        HDAdditionalLightData hdLight = GameObjectExtension.AddHDLight(gameObject, hdLightTypeAndShape);

        if (_InputUseIESMaximumIntensity)
        {
            LightUnit lightUnit = (IESMaximumIntensityUnit == "Lumens") ? LightUnit.Lumen : LightUnit.Candela;
            hdLight.SetIntensity(IESMaximumIntensity, lightUnit);
            
            Texture ies = (light.type == LightType.Point) ? (Texture)_OutputCookieCubemap : (Texture)_OutputCookie2D;

            if (light.type == LightType.Point)
            {
                var property = hdLight.GetType().GetProperty("IESPoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                property.SetValue(hdLight, ies);
            }
            else
            {
                var property = hdLight.GetType().GetProperty("IESSpot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                property.SetValue(hdLight, ies);
            }
        }        
    }

    /// <summary>
    /// Possible values for the IES Size.
    /// </summary>
    public enum IESResolution
    {
        /// <summary>Size 16</summary>
        IESResolution16 = 16,
        /// <summary>Size 32</summary>
        IESResolution32 = 32,
        /// <summary>Size 64</summary>
        IESResolution64 = 64,
        /// <summary>Size 128</summary>
        IESResolution128 = 128,
        /// <summary>Size 256</summary>
        IESResolution256 = 256,
        /// <summary>Size 512</summary>
        IESResolution512 = 512,
        /// <summary>Size 1024</summary>
        IESResolution1024 = 1024,
        /// <summary>Size 2048</summary>
        IESResolution2048 = 2048,
        /// <summary>Size 4096</summary>
        IESResolution4096 = 4096
    }

    /// <summary>
    /// Various possible type for IES, in HDRP for Rectangular light we use spot version
    /// </summary>
    public enum IESLightType
    {
        /// <summary>
        /// Point for the IES
        /// </summary>
        Point,
        /// <summary>
        /// Spot for IES (compatible with Area Light)
        /// </summary>
        Spot,
    }    
}
