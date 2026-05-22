using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class SetupGraphScene : MonoBehaviour
{
    [MenuItem("Tools/Setup Graph Scene")]
    public static void SetupScene()
    {
        // 1. Open GraphScene
        Scene graphScene = EditorSceneManager.OpenScene("Assets/Scenes/GraphScene.unity");

        // 2. Setup Camera
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            mainCam = camObj.AddComponent<Camera>();
        }
        mainCam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
        mainCam.clearFlags = CameraClearFlags.SolidColor;
        
        if (mainCam.GetComponent<GraphCameraController>() == null)
        {
            mainCam.gameObject.AddComponent<GraphCameraController>();
        }

        // 3. Setup GraphManager
        GameObject gmObj = GameObject.Find("GraphManager");
        if (gmObj == null)
        {
            gmObj = new GameObject("GraphManager");
        }
        GraphManager gm = gmObj.GetComponent<GraphManager>();
        if (gm == null) gm = gmObj.AddComponent<GraphManager>();
        
        // Default Line Material (Sprites-Default usually works well for UI/Graph lines)
        Material lineMat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        gm.lineMaterial = lineMat;

        // 4. Setup "Back to Game" UI using basic OnGUI for simplicity since it's just one button
        if (gmObj.GetComponent<GraphUI>() == null)
        {
            gmObj.AddComponent<GraphUI>();
        }

        // 5. Save the Scene
        EditorSceneManager.SaveScene(graphScene);

        // 6. Return to SampleScene so we can play the game
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        Debug.Log("GraphScene is successfully set up!");
    }
}
