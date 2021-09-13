using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public class IESProfilePostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {        
        foreach (var path in importedAssets)
        {
            PostprocessIESProfile(path);
        }
    }

    private static void PostprocessIESProfile(string assetPath)
    {        
        var importer = AssetImporter.GetAtPath(assetPath) as UnityEditor.Rendering.IESImporter;

        if (importer == null)
        {
            return;
        }        

        if (!importer.iesMetaData.UseIESMaximumIntensity)
        {
            return;
        }

        var engine = new RumtimeIESProfile.IESEngine();
        var error = engine.ReadFile($"{Application.dataPath}/../{assetPath}");

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError($"[IESProfilePostprocesser] not a valid ies profile... {error}, {assetPath}");

            return;
        }        

        float maxIntensity = 0f;
        string intensityUnit = "";
        (maxIntensity, intensityUnit) = engine.GetMaximumIntensity();

        if (!intensityUnit.Equals("Candelas"))
        {
            Debug.LogError($"[IESProfilePostprocesser] not a valid ies profile... ({assetPath})");

            return;
        }

        var targetGO = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);     
        var lightData = targetGO.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();

        if (lightData == null)
        {
            Debug.LogError($"[IESProfilePostprocesser] not a valid ies profile... ({assetPath})");

            return;
        }
        
        lightData.SetIntensity(maxIntensity, UnityEngine.Rendering.HighDefinition.LightUnit.Candela);
        Debug.LogWarning($"[IESProfilePostprocesser] changed the result ies profile light to use max candelas as its intensity. ({maxIntensity}), ({assetPath})");        
    }
}
