using UnityEditor;

public class StandardRelativityGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        MaterialProperty _Lorentz = FindProperty("_Lorentz", properties);
        MaterialProperty _dopplerShift = FindProperty("_dopplerShift", properties);
        MaterialProperty _UVAndIRTextures = FindProperty("_UVAndIRTextures", properties);
        MaterialProperty _EmissionOn = FindProperty("_EmissionOn", properties);

        bool isLorentz = _Lorentz.floatValue == 1;
        bool isDoppler = _dopplerShift.floatValue == 1;
        bool isUvIr = isDoppler && _UVAndIRTextures.floatValue == 1;
        bool isEmission = _EmissionOn.floatValue == 1;

        foreach (MaterialProperty property in properties)
        {
            if (!isLorentz)
            {
                if (property.name == "_IsStatic")
                {
                    continue;
                }
            }

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

            if (!isEmission)
            {
                if (property.name == "_EmissionMap" ||
                    property.name == "_EmissionColor" ||
                    property.name == "_EmissionMultiplier")
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
