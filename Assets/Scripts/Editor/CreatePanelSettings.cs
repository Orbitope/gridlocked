using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class CreatePanelSettings
{
    [MenuItem("Tools/Create Panel Settings")]
    public static void Create()
    {
        PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/DefaultPanelSettings.asset");
        if (panelSettings == null)
        {
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(panelSettings, "Assets/UI/DefaultPanelSettings.asset");
            AssetDatabase.SaveAssets();
        }

        var gm = GameObject.Find("GameManager");
        if (gm != null)
        {
            var uiDoc = gm.GetComponent<UIDocument>();
            if (uiDoc != null)
            {
                uiDoc.panelSettings = panelSettings;
                EditorUtility.SetDirty(gm);
            }
        }
        
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
        
        Debug.Log("PanelSettings and EventSystem created!");
    }
}
