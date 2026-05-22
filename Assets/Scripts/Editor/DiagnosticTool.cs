using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class DiagnosticTool : MonoBehaviour
{
    [MenuItem("Tools/Run Diagnostics")]
    public static void Run()
    {
        Debug.Log($"--- DIAGNOSTICS ---");
        Debug.Log($"Active Scene: {SceneManager.GetActiveScene().name}");

        UIDocument doc = Object.FindFirstObjectByType<UIDocument>();
        if (doc == null)
        {
            Debug.LogError("No UIDocument found in the scene!");
        }
        else
        {
            Debug.Log($"UIDocument found on {doc.gameObject.name}");
            if (doc.visualTreeAsset != null)
                Debug.Log($"VisualTreeAsset: {doc.visualTreeAsset.name}");
            else
                Debug.LogError("VisualTreeAsset is NULL!");

            if (doc.panelSettings != null)
                Debug.Log($"PanelSettings: {doc.panelSettings.name}");
            else
                Debug.LogError("PanelSettings is NULL!");
                
            if (doc.rootVisualElement != null)
            {
                var mainMenu = doc.rootVisualElement.Q("main-menu-screen");
                if (mainMenu != null)
                {
                    Debug.Log($"main-menu-screen display style: {mainMenu.style.display.value}");
                }
                else
                {
                    Debug.LogError("main-menu-screen NOT FOUND in root!");
                }
            }
        }

        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            Debug.LogError("GameManager not found in scene!");
        }
        else
        {
            Debug.Log($"GameManager found. Cars generated? {gm.boardContainer?.childCount}");
        }
    }
}
