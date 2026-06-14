using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Front-door menu. Two buttons launch the square or hex game scene.
/// Attach to the UIDocument GameObject in MainMenu.unity.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        root.Q<Button>("play-square-btn").clicked += () => SceneManager.LoadScene("SampleScene");
        root.Q<Button>("play-hex-btn").clicked    += () => SceneManager.LoadScene("HexScene");
        var aggBtn = root.Q<Button>("play-aggregate-btn");
        if (aggBtn != null) aggBtn.clicked += () => SceneManager.LoadScene("AggregateScene");
    }
}
