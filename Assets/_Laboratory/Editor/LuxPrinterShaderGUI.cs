using UnityEngine;
using UnityEditor;

public class LuxPrinterShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {        
        EditorGUILayout.HelpBox("Go to the Generate Lux To Color LUT render pass", MessageType.Warning);

        var material = materialEditor.target as Material;
        if (material.renderQueue != 2499)
        {
            material.renderQueue = 2499;            
        }
    }
}
