using UnityEngine;
using UnityEditor;

public class LuxPrinterShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {        
        m_Item0_Lux = FindProperty(MaterialPropertyNames._Item0_Lux, properties);
        m_Item0_Color = FindProperty(MaterialPropertyNames._Item0_Color, properties);
        m_Item1_Lux = FindProperty(MaterialPropertyNames._Item1_Lux, properties);
        m_Item1_Color = FindProperty(MaterialPropertyNames._Item1_Color, properties);
        m_Item2_Lux = FindProperty(MaterialPropertyNames._Item2_Lux, properties);
        m_Item2_Color = FindProperty(MaterialPropertyNames._Item2_Color, properties);        
        m_Item3_Lux = FindProperty(MaterialPropertyNames._Item3_Lux, properties);
        m_Item3_Color = FindProperty(MaterialPropertyNames._Item3_Color, properties);        
        m_Item4_Lux = FindProperty(MaterialPropertyNames._Item4_Lux, properties);
        m_Item4_Color = FindProperty(MaterialPropertyNames._Item4_Color, properties);        
        m_ItemEtc_Color = FindProperty(MaterialPropertyNames._ItemEtc_Color, properties);        
        materialEditor.ShaderProperty(m_Item0_Lux, Styles._Item0_Lux);
        materialEditor.ShaderProperty(m_Item0_Color, Styles._Item0_Color);
        materialEditor.ShaderProperty(m_Item1_Lux, Styles._Item1_Lux);
        materialEditor.ShaderProperty(m_Item1_Color, Styles._Item1_Color);        
        materialEditor.ShaderProperty(m_Item2_Lux, Styles._Item2_Lux);
        materialEditor.ShaderProperty(m_Item2_Color, Styles._Item2_Color);        
        materialEditor.ShaderProperty(m_Item3_Lux, Styles._Item3_Lux);
        materialEditor.ShaderProperty(m_Item3_Color, Styles._Item3_Color);        
        materialEditor.ShaderProperty(m_Item4_Lux, Styles._Item4_Lux);
        materialEditor.ShaderProperty(m_Item4_Color, Styles._Item4_Color);        
        materialEditor.ShaderProperty(m_ItemEtc_Color, Styles._ItemEtc_Color);        

        var material = materialEditor.target as Material;
        if (material.renderQueue != 3050)
        {
            material.renderQueue = 3050;            
        }
    }

    private MaterialProperty m_Item0_Lux = null;    
    private MaterialProperty m_Item0_Color = null;    
    private MaterialProperty m_Item1_Lux = null;    
    private MaterialProperty m_Item1_Color = null;            
    private MaterialProperty m_Item2_Lux = null;    
    private MaterialProperty m_Item2_Color = null;            
    private MaterialProperty m_Item3_Lux = null;    
    private MaterialProperty m_Item3_Color = null;            
    private MaterialProperty m_Item4_Lux = null;    
    private MaterialProperty m_Item4_Color = null;            
    private MaterialProperty m_ItemEtc_Color = null;            

    private static class Styles
    {      
        public static GUIContent _Item0_Lux = new GUIContent("_Item0_Lux");
        public static GUIContent _Item0_Color = new GUIContent("_Item0_Color");
        public static GUIContent _Item1_Lux = new GUIContent("_Item1_Lux");
        public static GUIContent _Item1_Color = new GUIContent("_Item1_Color");
        public static GUIContent _Item2_Lux = new GUIContent("_Item2_Lux");
        public static GUIContent _Item2_Color = new GUIContent("_Item2_Color");        
        public static GUIContent _Item3_Lux = new GUIContent("_Item3_Lux");
        public static GUIContent _Item3_Color = new GUIContent("_Item3_Color");        
        public static GUIContent _Item4_Lux = new GUIContent("_Item4_Lux");
        public static GUIContent _Item4_Color = new GUIContent("_Item4_Color");        
        public static GUIContent _ItemEtc_Color = new GUIContent("_ItemEtc_Color");        
    }

    private static class MaterialPropertyNames
    {
        public const string _Item0_Lux = "_Item0_Lux";
        public const string _Item0_Color = "_Item0_Color";
        public const string _Item1_Lux = "_Item1_Lux";
        public const string _Item1_Color = "_Item1_Color";        
        public const string _Item2_Lux = "_Item2_Lux";
        public const string _Item2_Color = "_Item2_Color";        
        public const string _Item3_Lux = "_Item3_Lux";
        public const string _Item3_Color = "_Item3_Color";        
        public const string _Item4_Lux = "_Item4_Lux";
        public const string _Item4_Color = "_Item4_Color";        
        public const string _ItemEtc_Color = "_ItemEtc_Color";        
    }    
}
