using UnityEditor;

public class ColorOnlyRelativityGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        MaterialProperty _UVAndIRTextures = FindProperty("_UVAndIRTextures", properties);

        bool isUvIr = _UVAndIRTextures.floatValue == 1;

        foreach (MaterialProperty property in properties)
        {
            if (!isUvIr)
            {
                if (property.name == "_UVTex" ||
                    property.name == "_IRTex")
                {
                    continue;
                }
            }

            if ((property.flags & MaterialProperty.PropFlags.HideInInspector) == 0)
            {
                materialEditor.ShaderProperty(property, property.displayName);
            }
        }
    }
}