using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public class NukeAndRebuildScene : MonoBehaviour
{
    [MenuItem("Tools/Nuke And Rebuild Scene")]
    public static void NukeAndRebuild()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "SampleScene")
        {
            scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        }

        // 1. Delete ALL GameManagers and UIDocuments to clear corruption
        var allGms = Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None);
        foreach (var gm in allGms)
        {
            if (gm != null && gm.gameObject != null) DestroyImmediate(gm.gameObject);
        }
        
        var allUIs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        foreach (var ui in allUIs)
        {
            if (ui != null && ui.gameObject != null) DestroyImmediate(ui.gameObject);
        }

        var allUIMgrs = Object.FindObjectsByType<UIManager>(FindObjectsSortMode.None);
        foreach (var um in allUIMgrs)
        {
            if (um != null && um.gameObject != null) DestroyImmediate(um.gameObject);
        }

        // 2. Delete BoardContainer if it exists
        var board = GameObject.Find("BoardContainer");
        if (board != null) DestroyImmediate(board);

        // 3. Delete Main Camera if duplicate
        var allCams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 1; i < allCams.Length; i++)
        {
            DestroyImmediate(allCams[i].gameObject);
        }

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            mainCam = camObj.AddComponent<Camera>();
        }
        mainCam.backgroundColor = new Color(0.12f, 0.15f, 0.2f);
        mainCam.clearFlags = CameraClearFlags.SolidColor;

        // 4. Create fresh BoardContainer
        GameObject newBoard = new GameObject("BoardContainer");
        RectTransform boardRT = newBoard.AddComponent<RectTransform>();
        boardRT.anchorMin = new Vector2(0.5f, 0.5f);
        boardRT.anchorMax = new Vector2(0.5f, 0.5f);
        boardRT.sizeDelta = new Vector2(600, 600);
        boardRT.anchoredPosition = Vector2.zero;

        // 5. Create fresh GameManager
        GameObject gmObj = new GameObject("GameManager");
        GameManager newGm = gmObj.AddComponent<GameManager>();
        newGm.boardContainer = boardRT;
        newGm.carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/CarPrefab.prefab");

        // 6. Create fresh UIDocument and UIManager
        GameObject uiObj = new GameObject("MainHUD");
        UIDocument uiDoc = uiObj.AddComponent<UIDocument>();
        uiDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/MainHUD.uxml");
        uiDoc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/DefaultPanelSettings.asset");
        uiObj.AddComponent<UIManager>();

        // 7. Save Scene explicitly
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("Scene completely nuked and rebuilt with fresh references. Saved to disk.");
    }
}
