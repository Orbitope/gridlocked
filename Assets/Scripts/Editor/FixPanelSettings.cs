using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class FixPanelSettings
{
    [MenuItem("Tools/Fix Panel Settings")]
    public static void Fix()
    {
        PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/DefaultPanelSettings.asset");
        if (panelSettings != null)
        {
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1080, 1920);
            EditorUtility.SetDirty(panelSettings);
            AssetDatabase.SaveAssets();
            Debug.Log("PanelSettings scaled to 1920x1080!");
        }
    }
}
