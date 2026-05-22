using UnityEngine;
using UnityEngine.SceneManagement;

public class GraphUI : MonoBehaviour
{
    private void OnGUI()
    {
        // Simple GUI button to return to the game
        int width = 250;
        int height = 80;
        Rect btnRect = new Rect(Screen.width / 2 - width / 2, Screen.height - height - 40, width, height);

        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.fontSize = 32;

        if (GUI.Button(btnRect, "Back to Game", style))
        {
            SceneManager.LoadScene("SampleScene");
        }
    }
}
