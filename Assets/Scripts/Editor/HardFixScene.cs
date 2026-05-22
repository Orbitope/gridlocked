using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public class HardFixScene : MonoBehaviour
{
    [MenuItem("Tools/Hard Fix Scene")]
    public static void FixScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        
        // 1. Force refresh the UXML
        AssetDatabase.ImportAsset("Assets/UI/MainHUD.uxml", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/UI/DefaultPanelSettings.asset", ImportAssetOptions.ForceUpdate);
        
        // 2. Fix the Board Container
        GameObject boardObj = GameObject.Find("BoardContainer");
        if (boardObj == null)
        {
            boardObj = new GameObject("BoardContainer");
        }
        
        RectTransform boardRT = boardObj.GetComponent<RectTransform>();
        if (boardRT == null) boardRT = boardObj.AddComponent<RectTransform>();
        
        boardRT.anchorMin = new Vector2(0.5f, 0.5f);
        boardRT.anchorMax = new Vector2(0.5f, 0.5f);
        boardRT.sizeDelta = new Vector2(600, 600);
        boardRT.anchoredPosition = Vector2.zero; // Center it

        // 3. Fix the UIDocument (Re-assign the asset just in case it got unlinked)
        UIDocument uiDoc = Object.FindFirstObjectByType<UIDocument>();
        if (uiDoc != null)
        {
            uiDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/MainHUD.uxml");
            uiDoc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/DefaultPanelSettings.asset");
        }
        else
        {
            Debug.LogError("COULD NOT FIND UIDOCUMENT!");
        }

        // 4. Force refresh GameManager
        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            gm.boardContainer = boardRT;
            // Delete all children so it regenerates cleanly
            for (int i = boardRT.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(boardRT.GetChild(i).gameObject);
            }
        }
        else
        {
            Debug.LogError("COULD NOT FIND GAMEMANAGER!");
        }

        // 5. SAVE THE SCENE TO DISK SO PLAY MODE DOESN'T REVERT IT!
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("Hard Fix Applied and SAVED TO DISK! The Board is centered, the UXML is refreshed, and the grid is cleared.");
    }
}
