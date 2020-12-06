using UnityEditor;

public class AcceleratedRelativityGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        MaterialProperty _dopplerShift = FindProperty("_dopplerShift", properties);
        MaterialProperty _UVAndIRTextures = FindProperty("_UVAndIRTextures", properties);

        bool isDoppler = _dopplerShift.floatValue == 1;
        bool isUvIr = isDoppler && _UVAndIRTextures.floatValue == 1;

        foreach (MaterialProperty property in properties)
        {
            if (!isDoppler)
            {
                if (property.name == "_dopplerIntensity" ||
                    property.name == "_dopplerMix" ||
                    property.name == "_UVAndIRTextures")
                {
                    continue;
                }
            }

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

