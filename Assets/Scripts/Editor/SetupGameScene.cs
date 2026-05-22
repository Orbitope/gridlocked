using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine.UI;
using UnityEditor.SceneManagement;

public class SetupGameScene
{
    [MenuItem("Tools/Setup Game Scene")]
    public static void Setup()
    {
        // 1. Setup Car Prefab
        GameObject carObj = new GameObject("CarPrefab");
        var rt = carObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0, 0); // Bottom left pivot for easy grid math
        
        carObj.AddComponent<UnityEngine.UI.Image>(); // FIXED AMBIGUITY
        carObj.AddComponent<CarController>();
        
        PrefabUtility.SaveAsPrefabAsset(carObj, "Assets/Resources/CarPrefab.prefab");
        GameObject.DestroyImmediate(carObj);

        // 2. Setup Scene
        GameObject oldCanvas = GameObject.Find("Canvas");
        if (oldCanvas != null) GameObject.DestroyImmediate(oldCanvas);
        
        GameObject oldGm = GameObject.Find("GameManager");
        if (oldGm != null) GameObject.DestroyImmediate(oldGm);

        GameObject canvasObj = new GameObject("Canvas");
        canvasObj.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject boardContainer = new GameObject("BoardContainer");
        boardContainer.transform.SetParent(canvasObj.transform);
        var boardRT = boardContainer.AddComponent<RectTransform>();
        boardRT.anchoredPosition = new Vector2(0, 0);
        boardRT.anchorMin = new Vector2(0.5f, 0.5f);
        boardRT.anchorMax = new Vector2(0.5f, 0.5f);
        boardRT.pivot = new Vector2(0.5f, 0.5f);
        boardRT.sizeDelta = new Vector2(600, 600); // 6x6 grid, 100px each
        
        GameObject gameManagerObj = new GameObject("GameManager");
        var gm = gameManagerObj.AddComponent<GameManager>();
        gm.carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/CarPrefab.prefab");
        gm.boardContainer = boardContainer.transform;

        var uiDocument = gameManagerObj.AddComponent<UIDocument>();
        uiDocument.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/MainHUD.uxml");
        
        gameManagerObj.AddComponent<UIManager>();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        
        Debug.Log("Game Scene Setup Complete!");
    }
}
