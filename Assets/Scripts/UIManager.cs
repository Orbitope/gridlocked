using UnityEngine;
using UnityEngine.UIElements;

public class UIManager : MonoBehaviour
{
    private UIDocument _document;

    private void OnEnable()
    {
        _document = GetComponent<UIDocument>();
        if (_document == null || _document.rootVisualElement == null) return;

        var root = _document.rootVisualElement;

        // Undo Button
        Button undoBtn = root.Q<Button>("undo-btn");
        if (undoBtn != null)
        {
            undoBtn.clicked += () =>
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.UndoMove();
                }
            };
        }

        // Hint Button
        Button hintBtn = root.Q<Button>("hint-btn");
        if (hintBtn != null)
        {
            hintBtn.clicked += () =>
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ShowHint();
                }
            };
        }

        // New Puzzle Button
        Button newPuzzleBtn = root.Q<Button>("new-puzzle-btn");
        if (newPuzzleBtn != null)
        {
            newPuzzleBtn.clicked += () =>
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.LoadNewPuzzle();
                }
            };
        }
    }
}
