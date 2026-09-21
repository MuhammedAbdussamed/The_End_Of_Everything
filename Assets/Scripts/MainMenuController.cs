using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class MainMenuController : MonoBehaviour
{
    private Transform canvasRoot;
    private Font uiFont;
    private GameObject mainPanel;
    private GameObject settingsPanel;
    private GameObject levelPanel;
    private Text soundButtonLabel;
    private bool soundEnabled = true;

    private readonly Color background = new Color(0.035f, 0.055f, 0.10f, 0.96f);
    private readonly Color panelColor = new Color(0.08f, 0.13f, 0.23f, 0.96f);
    private readonly Color accent = new Color(0.12f, 0.74f, 0.72f, 1f);
    private readonly Color muted = new Color(0.22f, 0.28f, 0.38f, 1f);

    private void Awake()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CreateEventSystem();
        CreateCanvas();
        BuildMainMenu();
        BuildSettings();
        BuildLevelSelect();
        ShowMainMenu();
    }

    private void CreateEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

private void CreateCanvas()
    {
        GameObject canvasObject = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot = canvasObject.transform;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void BuildMainMenu()
    {
        mainPanel = CreatePanel("MainPanel", canvasRoot, new Vector2(0.5f, 0.5f), new Vector2(0.58f, 0.72f), panelColor);
        CreateText("Title", mainPanel.transform, "THE END OF EVERYTHING", 52, FontStyle.Bold, new Vector2(0.5f, 0.82f), new Vector2(0.85f, 0.12f), Color.white);
        CreateText("Subtitle", mainPanel.transform, "Ana Menü", 23, FontStyle.Normal, new Vector2(0.5f, 0.74f), new Vector2(0.7f, 0.06f), new Color(0.65f, 0.75f, 0.9f));

        CreateButton("StartButton", mainPanel.transform, "BAŞLA", new Vector2(0.5f, 0.58f), new Vector2(0.52f, 0.11f), accent, ShowLevels);
        CreateButton("SettingsButton", mainPanel.transform, "AYARLAR", new Vector2(0.5f, 0.44f), new Vector2(0.52f, 0.11f), new Color(0.15f, 0.23f, 0.36f), ShowSettings);
        CreateButton("QuitButton", mainPanel.transform, "ÇIKIŞ", new Vector2(0.5f, 0.30f), new Vector2(0.52f, 0.11f), new Color(0.48f, 0.16f, 0.20f), QuitGame);

        GameObject social = CreatePanel("SocialPanel", mainPanel.transform, new Vector2(0.88f, 0.5f), new Vector2(0.20f, 0.48f), new Color(0.055f, 0.09f, 0.16f, 1f));
        CreateText("SocialTitle", social.transform, "SOSYAL", 18, FontStyle.Bold, new Vector2(0.5f, 0.79f), new Vector2(0.8f, 0.14f), new Color(0.66f, 0.8f, 0.96f));
        CreateButton("GithubButton", social.transform, "GITHUB", new Vector2(0.5f, 0.55f), new Vector2(0.78f, 0.22f), new Color(0.16f, 0.19f, 0.25f), () => Application.OpenURL("https://github.com"));
        CreateButton("InstagramButton", social.transform, "INSTAGRAM", new Vector2(0.5f, 0.28f), new Vector2(0.78f, 0.22f), new Color(0.48f, 0.14f, 0.34f), () => Application.OpenURL("https://instagram.com"));
    }

    private void BuildSettings()
    {
        settingsPanel = CreatePanel("SettingsPanel", canvasRoot, new Vector2(0.5f, 0.5f), new Vector2(0.48f, 0.48f), panelColor);
        CreateText("SettingsTitle", settingsPanel.transform, "AYARLAR", 42, FontStyle.Bold, new Vector2(0.5f, 0.76f), new Vector2(0.8f, 0.16f), Color.white);
        CreateText("SoundLabel", settingsPanel.transform, "GENEL SES", 24, FontStyle.Bold, new Vector2(0.33f, 0.52f), new Vector2(0.42f, 0.12f), new Color(0.76f, 0.84f, 0.94f));

        Button soundButton = CreateButton("SoundToggle", settingsPanel.transform, "AÇIK", new Vector2(0.70f, 0.52f), new Vector2(0.28f, 0.14f), accent, ToggleSound);
        soundButtonLabel = soundButton.GetComponentInChildren<Text>();

        CreateButton("SettingsBack", settingsPanel.transform, "GERİ", new Vector2(0.5f, 0.20f), new Vector2(0.38f, 0.14f), muted, ShowMainMenu);
    }

    private void BuildLevelSelect()
    {
        levelPanel = CreatePanel("LevelPanel", canvasRoot, new Vector2(0.5f, 0.5f), new Vector2(0.80f, 0.60f), panelColor);
        CreateText("LevelTitle", levelPanel.transform, "BÖLÜM SEÇ", 42, FontStyle.Bold, new Vector2(0.5f, 0.84f), new Vector2(0.7f, 0.14f), Color.white);

        CreateLevelCard("Level1", levelPanel.transform, new Vector2(0.23f, 0.49f), "BÖLÜM 1", "AÇIK", accent, true);
        CreateLevelCard("Level2", levelPanel.transform, new Vector2(0.50f, 0.49f), "BÖLÜM 2", "🔒\nKİLİTLİ", muted, false);
        CreateLevelCard("Level3", levelPanel.transform, new Vector2(0.77f, 0.49f), "BÖLÜM 3", "🔒\nKİLİTLİ", muted, false);

        CreateButton("LevelBack", levelPanel.transform, "GERİ", new Vector2(0.5f, 0.16f), new Vector2(0.25f, 0.12f), muted, ShowMainMenu);
    }

    private void CreateLevelCard(string name, Transform parent, Vector2 position, string title, string state, Color color, bool unlocked)
    {
        GameObject card = CreatePanel(name, parent, position, new Vector2(0.22f, 0.42f), color);
        CreateText("Title", card.transform, title, 24, FontStyle.Bold, new Vector2(0.5f, 0.70f), new Vector2(0.9f, 0.18f), Color.white);
        CreateText("State", card.transform, state, unlocked ? 20 : 29, FontStyle.Bold, new Vector2(0.5f, 0.39f), new Vector2(0.9f, 0.28f), unlocked ? Color.white : new Color(0.75f, 0.80f, 0.86f));

        if (unlocked)
            CreateButton("Play", card.transform, "OYNA", new Vector2(0.5f, 0.15f), new Vector2(0.72f, 0.20f), new Color(0.04f, 0.24f, 0.25f), StartLevelOne);
    }

    private GameObject CreatePanel(string name, Transform parent, Vector2 anchor, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchor - size * 0.5f;
        rect.anchorMax = anchor + size * 0.5f;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private Text CreateText(string name, Transform parent, string value, int size, FontStyle style, Vector2 anchor, Vector2 area, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor - area * 0.5f;
        rect.anchorMax = anchor + area * 0.5f;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text text = textObject.GetComponent<Text>();
        text.font = uiFont;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        return text;
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 anchor, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreatePanel(name, parent, anchor, size, color);
        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.86f, 0.95f, 1f);
        colors.pressedColor = new Color(0.68f, 0.78f, 0.88f);
        button.colors = colors;
        button.onClick.AddListener(onClick);
        CreateText("Label", buttonObject.transform, label, 24, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0.92f, 0.82f), Color.white);
        return button;
    }

    private void ToggleSound()
    {
        soundEnabled = !soundEnabled;
        AudioListener.volume = soundEnabled ? 1f : 0f;
        soundButtonLabel.text = soundEnabled ? "AÇIK" : "KAPALI";
    }

    private void ShowMainMenu()
    {
        mainPanel.SetActive(true);
        settingsPanel.SetActive(false);
        levelPanel.SetActive(false);
    }

    private void ShowSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
        levelPanel.SetActive(false);
    }

    private void ShowLevels()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(false);
        levelPanel.SetActive(true);
    }

    private void StartLevelOne()
    {
        Debug.Log("Bölüm 1 başlatılıyor.");
        SceneManager.LoadScene("SampleScene");
    }

    private void QuitGame()
    {
        Debug.Log("Oyun kapatılıyor.");
        Application.Quit();
    }
}