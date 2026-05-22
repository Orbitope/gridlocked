using UnityEngine;
using UnityEngine.UIElements;

public class UIManager : MonoBehaviour
{
    private VisualElement _mainMenuScreen;
    private VisualElement _customGenScreen;
    private VisualElement _gameScreen;
    
    private ScrollView _levelList;
    
    private SliderInt _carCountSlider;
    private Label _carCountLabel;
    private SliderInt _minMovesSlider;
    private Label _minMovesLabel;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        // Screens
        _mainMenuScreen = root.Q<VisualElement>("main-menu-screen");
        _customGenScreen = root.Q<VisualElement>("custom-generator-screen");
        _gameScreen = root.Q<VisualElement>("game-screen");

        // Main Menu UI
        _levelList = root.Q<ScrollView>("level-list");
        root.Q<Button>("goto-custom-btn").clicked += () => SwitchScreen(_customGenScreen);

        // Custom Gen UI
        _carCountSlider = root.Q<SliderInt>("car-count-slider");
        _carCountLabel = root.Q<Label>("car-count-label");
        _carCountSlider.RegisterValueChangedCallback(evt => _carCountLabel.text = $"{evt.newValue} Cars");

        _minMovesSlider = root.Q<SliderInt>("min-moves-slider");
        _minMovesLabel = root.Q<Label>("min-moves-label");
        _minMovesSlider.RegisterValueChangedCallback(evt => _minMovesLabel.text = $"{evt.newValue} Moves");

        root.Q<Button>("generate-btn").clicked += GenerateCustomPuzzle;
        root.Q<Button>("back-to-menu-btn").clicked += () => SwitchScreen(_mainMenuScreen);

        // Game UI
        root.Q<Button>("menu-btn").clicked += () => SwitchScreen(_mainMenuScreen);
        root.Q<Button>("undo-btn").clicked += () => GameManager.Instance.UndoMove();
        root.Q<Button>("hint-btn").clicked += () => GameManager.Instance.ShowHint();
        root.Q<Button>("graph-btn").clicked += LoadGraphScene;
        root.Q<Button>("new-puzzle-btn").clicked += RestartCurrentPuzzle;

        // Init
        SwitchScreen(_mainMenuScreen);
    }

    private void LoadGraphScene()
    {
        if (GameManager.Instance != null && GameManager.Instance.Def != null)
        {
            CrossSceneData.TargetDef = GameManager.Instance.Def;
            CrossSceneData.TargetState = GameManager.Instance.CurrentState;
            UnityEngine.SceneManagement.SceneManager.LoadScene("GraphScene");
        }
    }

    private void SwitchScreen(VisualElement screen)
    {
        _mainMenuScreen.style.display = DisplayStyle.None;
        _customGenScreen.style.display = DisplayStyle.None;
        _gameScreen.style.display = DisplayStyle.None;

        screen.style.display = DisplayStyle.Flex;

        if (screen == _mainMenuScreen)
        {
            GameManager.Instance.ClearBoard();
            PopulateLevelList();
        }
    }

    private void PopulateLevelList()
    {
        _levelList.Clear();
        
        LevelDatabase db = Resources.Load<LevelDatabase>("LevelDatabase");
        if (db == null || db.Levels.Count == 0)
        {
            _levelList.Add(new Label("No levels found. Use the Editor tool to generate some!") { style = { color = Color.white, fontSize = 24 }});
            return;
        }

        int levelNum = 1;
        foreach (var level in db.Levels)
        {
            var btn = new Button();
            btn.style.height = 80;
            btn.style.marginBottom = 10;
            btn.style.fontSize = 32;
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.justifyContent = Justify.SpaceBetween;
            btn.style.paddingLeft = 20;
            btn.style.paddingRight = 20;

            int bestMoves = SaveManager.GetBestMoves(level.ID);
            string scoreText = bestMoves > 0 ? $"Best: {bestMoves}" : "Unsolved";
            
            var titleLabel = new Label($"Level {levelNum}");
            var scoreLabel = new Label(scoreText);
            
            if (bestMoves > 0) scoreLabel.style.color = new Color(0.5f, 1f, 0.5f);

            btn.Add(titleLabel);
            btn.Add(scoreLabel);

            // Copy struct for closure
            LevelData data = level;
            btn.clicked += () => {
                GameManager.Instance.LoadLevel(data);
                SwitchScreen(_gameScreen);
            };

            _levelList.Add(btn);
            levelNum++;
        }
    }

    private void GenerateCustomPuzzle()
    {
        GameManager.Instance.LoadCustomPuzzle(_carCountSlider.value, _minMovesSlider.value);
        SwitchScreen(_gameScreen);
    }

    private void RestartCurrentPuzzle()
    {
        if (GameManager.Instance.CurrentLevelData.HasValue)
        {
            GameManager.Instance.LoadLevel(GameManager.Instance.CurrentLevelData.Value);
        }
        else
        {
            // Just regenerate with same params if random
            GameManager.Instance.LoadCustomPuzzle(_carCountSlider.value, _minMovesSlider.value);
        }
    }
}
