using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
public class GameHud : MonoBehaviour
{
    [SerializeField] private WaveController waves;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Text waveText;
    [SerializeField] private Text healthText;
    [SerializeField] private Text statusText;
    [SerializeField] private PlayerGold playerGold;
    [SerializeField] private Text goldText;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Text resultText;
    [SerializeField] private Text resultDetails;
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private Text towerNameText;
    [SerializeField] private Text towerLevelText;
    [SerializeField] private Text towerStatsText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Text upgradeButtonText;
    [SerializeField] private Button closeUpgradeButton;

    private TowerBase selectedTower;
    private BuildSite selectedBuildSite;
    private GameObject buildPanel;
    private Button archerBuildButton;
    private Button mageBuildButton;
    private Button bomberBuildButton;
    private Button closeBuildButton;
    private Button sellButton;
    private Text sellButtonText;
    private Canvas hudCanvas;

    public string WaveLabel => waveText.text;
    public string HealthLabel => healthText.text;
    public string GoldLabel => goldText.text;
    public string ResultLabel => resultText.text;
    public string ResultDetailsLabel => resultDetails.text;
    public bool ResultVisible => resultPanel.activeSelf;
    public bool UpgradePanelVisible => upgradePanel != null && upgradePanel.activeSelf;
    public bool BuildPanelVisible => buildPanel != null && buildPanel.activeSelf;
    public TowerBase SelectedTower => selectedTower;
    public BuildSite SelectedBuildSite => selectedBuildSite;
    public string UpgradeButtonLabel => upgradeButtonText != null ? upgradeButtonText.text : string.Empty;

    private void Awake()
    {
        hudCanvas = GetComponent<Canvas>();
        if (resultDetails != null)
        {
            resultDetails.rectTransform.anchoredPosition = new Vector2(24f, -124f);
            resultDetails.rectTransform.sizeDelta = new Vector2(392f, 80f);
            resultDetails.fontSize = 22;
        }
        CreateSellButton();
        CreateBuildPanel();
    }

    private void OnEnable()
    {
        if (upgradeButton != null) upgradeButton.onClick.AddListener(UpgradeSelectedTower);
        if (closeUpgradeButton != null) closeUpgradeButton.onClick.AddListener(CloseTowerPanel);
        if (sellButton != null) sellButton.onClick.AddListener(SellSelectedTower);
        if (archerBuildButton != null) archerBuildButton.onClick.AddListener(() => BuildSelected(BuildSite.TowerKind.Archer));
        if (mageBuildButton != null) mageBuildButton.onClick.AddListener(() => BuildSelected(BuildSite.TowerKind.Mage));
        if (bomberBuildButton != null) bomberBuildButton.onClick.AddListener(() => BuildSelected(BuildSite.TowerKind.Bomber));
        if (closeBuildButton != null) closeBuildButton.onClick.AddListener(CloseBuildPanel);
        waves.Changed += Refresh;
        playerHealth.Changed += Refresh;
        if (playerGold != null) playerGold.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (waves != null) waves.Changed -= Refresh;
        if (playerHealth != null) playerHealth.Changed -= Refresh;
        if (playerGold != null) playerGold.Changed -= Refresh;
        if (selectedTower != null) selectedTower.Upgraded -= OnTowerUpgraded;
        if (upgradeButton != null) upgradeButton.onClick.RemoveListener(UpgradeSelectedTower);
        if (closeUpgradeButton != null) closeUpgradeButton.onClick.RemoveListener(CloseTowerPanel);
        if (sellButton != null) sellButton.onClick.RemoveListener(SellSelectedTower);
        if (archerBuildButton != null) archerBuildButton.onClick.RemoveAllListeners();
        if (mageBuildButton != null) mageBuildButton.onClick.RemoveAllListeners();
        if (bomberBuildButton != null) bomberBuildButton.onClick.RemoveAllListeners();
        if (closeBuildButton != null) closeBuildButton.onClick.RemoveListener(CloseBuildPanel);
    }

    private void Refresh()
    {
        waveText.text = $"{waves.CurrentWave} / {waves.TotalWaves}";
        healthText.text = $"{playerHealth.CurrentHealth:0.#} / {playerHealth.MaxHealth:0.#}";
        if (goldText != null && playerGold != null) goldText.text = playerGold.CurrentGold.ToString();
        bool won = waves.State == WaveController.WaveState.Completed;
        bool lost = waves.State == WaveController.WaveState.Defeated;
        if (resultPanel != null)
        {
            resultPanel.SetActive(won || lost);
            resultText.text = won ? "KAZANDIN" : "KAYBETTİN";
            resultText.color = won ? new Color(0.38f, 0.94f, 0.73f) : new Color(1f, 0.42f, 0.40f);
            resultDetails.text = won ? GetStarText(CalculateWinStars(playerHealth.CurrentHealth)) : "Canın tükendi";
        }
        if (won || lost)
        {
            if (UpgradePanelVisible) CloseTowerPanel();
            if (BuildPanelVisible) CloseBuildPanel();
        }
        else RefreshUpgradePanel();
        RefreshBuildPanel();
        statusText.text = waves.State switch
        {
            WaveController.WaveState.Preparing => "Düşmanlar hazırlanıyor",
            WaveController.WaveState.Active => $"Kalan düşman: {waves.RemainingEnemies}",
            WaveController.WaveState.Completed => "Tüm dalgalar tamamlandı",
            WaveController.WaveState.Defeated => "Savunma düştü",
            _ => ""
        };
    }

    public static int CalculateWinStars(float remainingHealth) =>
        remainingHealth >= 75f ? 3 : remainingHealth >= 50f ? 2 : 1;

    private static string GetStarText(int starCount) => starCount switch
    {
        3 => "★  ★  ★",
        2 => "★  ★",
        _ => "★"
    };

    public void OpenTowerPanel(TowerBase tower)
    {
        if (tower == null || ResultVisible) return;
        CloseBuildPanel();
        if (selectedTower != null) selectedTower.Upgraded -= OnTowerUpgraded;
        selectedTower = tower;
        selectedTower.Upgraded += OnTowerUpgraded;
        upgradePanel.SetActive(true);
        RefreshUpgradePanel();
    }

    public void OpenBuildPanel(BuildSite site)
    {
        if (site == null || !site.IsEmpty || ResultVisible) return;
        CloseTowerPanel();
        selectedBuildSite = site;
        buildPanel.SetActive(true);
        RefreshBuildPanel();
    }

    public void CloseTowerPanel()
    {
        if (selectedTower != null) selectedTower.Upgraded -= OnTowerUpgraded;
        selectedTower = null;
        if (upgradePanel != null) upgradePanel.SetActive(false);
    }

    public void CloseBuildPanel()
    {
        selectedBuildSite = null;
        if (buildPanel != null) buildPanel.SetActive(false);
    }

    public void UpgradeSelectedTower()
    {
        if (selectedTower == null || playerGold == null) return;
        selectedTower.TryUpgrade(playerGold);
        RefreshUpgradePanel();
    }

    public void SellSelectedTower()
    {
        if (selectedTower == null || playerGold == null) return;
        TowerBase tower = selectedTower;
        CloseTowerPanel();
        tower.TrySell(playerGold);
    }

    public void BuildSelected(BuildSite.TowerKind kind)
    {
        if (selectedBuildSite == null || playerGold == null) return;
        if (selectedBuildSite.TryBuild(kind, playerGold)) CloseBuildPanel();
        else RefreshBuildPanel();
    }

    public bool TryHandleTowerPanelClick(Vector2 screenPosition)
    {
        if (!UpgradePanelVisible) return false;
        if (upgradeButton != null && IsPointerOver(upgradeButton.transform as RectTransform, screenPosition))
        {
            UpgradeSelectedTower();
            return true;
        }
        if (closeUpgradeButton != null && IsPointerOver(closeUpgradeButton.transform as RectTransform, screenPosition))
        {
            CloseTowerPanel();
            return true;
        }
        if (sellButton != null && IsPointerOver(sellButton.transform as RectTransform, screenPosition))
        {
            SellSelectedTower();
            return true;
        }
        return IsPointerOver(upgradePanel.transform as RectTransform, screenPosition);
    }

    public bool TryHandleBuildPanelClick(Vector2 screenPosition)
    {
        if (!BuildPanelVisible) return false;
        if (TryClickBuildButton(archerBuildButton, BuildSite.TowerKind.Archer, screenPosition)) return true;
        if (TryClickBuildButton(mageBuildButton, BuildSite.TowerKind.Mage, screenPosition)) return true;
        if (TryClickBuildButton(bomberBuildButton, BuildSite.TowerKind.Bomber, screenPosition)) return true;
        if (closeBuildButton != null && IsPointerOver(closeBuildButton.transform as RectTransform, screenPosition))
        {
            CloseBuildPanel();
            return true;
        }
        return IsPointerOver(buildPanel.transform as RectTransform, screenPosition);
    }

    public bool TryHandleInteractionPanelClick(Vector2 screenPosition) =>
        TryHandleTowerPanelClick(screenPosition) || TryHandleBuildPanelClick(screenPosition);

    public bool IsPointerOverTowerPanel(Vector2 screenPosition) =>
        (UpgradePanelVisible && IsPointerOver(upgradePanel.transform as RectTransform, screenPosition))
        || (BuildPanelVisible && IsPointerOver(buildPanel.transform as RectTransform, screenPosition));

    private bool IsPointerOver(RectTransform rect, Vector2 screenPosition)
    {
        if (rect == null) return false;
        Camera eventCamera = hudCanvas != null && hudCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? hudCanvas.worldCamera : null;
        if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, eventCamera)) return true;

        // In the Editor, Game View input coordinates can differ from the Canvas render size.
        // Map both spaces so UI clicks remain reliable at every Game View resolution.
        if (hudCanvas == null || Screen.width <= 0 || Screen.height <= 0) return false;
        Rect pixels = hudCanvas.pixelRect;
        Vector2 canvasPosition = new Vector2(
            pixels.x + screenPosition.x * pixels.width / Screen.width,
            pixels.y + screenPosition.y * pixels.height / Screen.height);
        return RectTransformUtility.RectangleContainsScreenPoint(rect, canvasPosition, eventCamera);
    }

    private void OnTowerUpgraded(TowerBase tower) => RefreshUpgradePanel();

    private void RefreshUpgradePanel()
    {
        if (!UpgradePanelVisible || selectedTower == null) return;
        towerNameText.text = selectedTower switch
        {
            ArcherTower => "Okçu Kulesi",
            MageTower => "Büyücü Kulesi",
            BomberTower => "Bombacı Kulesi",
            _ => "Kule"
        };
        towerLevelText.text = $"Seviye {selectedTower.TowerLevel}";
        towerStatsText.text = $"Hasar  {selectedTower.TowerDamage:0.#}\nHız  {selectedTower.TowerAttackSpeed:0.##}/sn\nMenzil  {selectedTower.TowerRange:0.##}";
        if (selectedTower.CanUpgrade)
        {
            upgradeButtonText.text = $"Yükselt  •  {selectedTower.UpgradeCost} Gold";
            upgradeButton.interactable = playerGold != null && playerGold.CanAfford(selectedTower.UpgradeCost);
        }
        else
        {
            upgradeButtonText.text = "Maksimum seviye";
            upgradeButton.interactable = false;
        }
        if (sellButtonText != null) sellButtonText.text = $"Sat / Yık  •  +{selectedTower.SellValue} Gold";
    }

    private void RefreshBuildPanel()
    {
        if (!BuildPanelVisible) return;
        if (selectedBuildSite == null || !selectedBuildSite.IsEmpty)
        {
            CloseBuildPanel();
            return;
        }
        SetBuildButtonState(archerBuildButton, 60);
        SetBuildButtonState(mageBuildButton, 90);
        SetBuildButtonState(bomberBuildButton, 120);
    }

    private void SetBuildButtonState(Button button, int cost)
    {
        if (button != null) button.interactable = playerGold != null && playerGold.CanAfford(cost);
    }

    private bool TryClickBuildButton(Button button, BuildSite.TowerKind kind, Vector2 screenPosition)
    {
        if (button == null || !IsPointerOver(button.transform as RectTransform, screenPosition)) return false;
        BuildSelected(kind);
        return true;
    }

    private void CreateSellButton()
    {
        if (upgradeButton == null || upgradePanel == null) return;
        RectTransform upgradeRect = upgradeButton.transform as RectTransform;
        upgradeRect.sizeDelta = new Vector2(142f, upgradeRect.sizeDelta.y);
        sellButton = Instantiate(upgradeButton, upgradePanel.transform);
        sellButton.name = "Sell";
        sellButton.onClick.RemoveAllListeners();
        RectTransform sellRect = sellButton.transform as RectTransform;
        sellRect.anchoredPosition = new Vector2(176f, upgradeRect.anchoredPosition.y);
        sellRect.sizeDelta = new Vector2(142f, upgradeRect.sizeDelta.y);
        Image image = sellButton.GetComponent<Image>();
        if (image != null) image.color = new Color(0.58f, 0.24f, 0.20f, 1f);
        sellButtonText = sellButton.GetComponentInChildren<Text>();
        if (sellButtonText != null) sellButtonText.fontSize = 14;
    }

    private void CreateBuildPanel()
    {
        buildPanel = CreateUiObject("Kule Kurma Paneli", transform, typeof(Image));
        RectTransform panelRect = buildPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-24f, 24f);
        panelRect.sizeDelta = new Vector2(340f, 264f);
        buildPanel.GetComponent<Image>().color = new Color(0.04f, 0.07f, 0.10f, 0.97f);

        CreateLabel("Başlık", buildPanel.transform, "Kule Kur", new Vector2(22f, -20f), new Vector2(250f, 42f), 28, TextAnchor.MiddleLeft);
        CreateLabel("Açıklama", buildPanel.transform, "Boş inşa alanına kurulacak kuleyi seç", new Vector2(22f, -62f), new Vector2(296f, 34f), 14, TextAnchor.MiddleLeft);
        archerBuildButton = CreateBuildButton("Okçu", "Okçu  •  60 Gold", -108f, new Color(0.15f, 0.48f, 0.38f), BuildSite.TowerKind.Archer);
        mageBuildButton = CreateBuildButton("Büyücü", "Büyücü  •  90 Gold", -158f, new Color(0.28f, 0.31f, 0.66f), BuildSite.TowerKind.Mage);
        bomberBuildButton = CreateBuildButton("Bombacı", "Bombacı  •  120 Gold", -208f, new Color(0.64f, 0.35f, 0.14f), BuildSite.TowerKind.Bomber);
        closeBuildButton = CreateButton("Kapat", buildPanel.transform, "×", new Vector2(286f, -18f), new Vector2(32f, 32f), new Color(0.30f, 0.34f, 0.39f), 22);
        buildPanel.SetActive(false);
    }

    private Button CreateBuildButton(string name, string label, float y, Color color, BuildSite.TowerKind kind)
    {
        Button button = CreateButton(name, buildPanel.transform, label, new Vector2(22f, y), new Vector2(296f, 42f), color, 17);
        EventTrigger trigger = button.gameObject.AddComponent<EventTrigger>();
        var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pointerDown.callback.AddListener(eventData =>
        {
            if (eventData is PointerEventData pointer && pointer.button == PointerEventData.InputButton.Left && button.interactable)
                BuildSelected(kind);
        });
        trigger.triggers.Add(pointerDown);
        return button;
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size, Color color, int fontSize)
    {
        GameObject buttonObject = CreateUiObject(name, parent, typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        Text text = CreateLabel("Label", buttonObject.transform, label, Vector2.zero, Vector2.zero, fontSize, TextAnchor.MiddleCenter);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        return button;
    }

    private Text CreateLabel(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
    {
        GameObject labelObject = CreateUiObject(name, parent, typeof(Text));
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Text label = labelObject.GetComponent<Text>();
        label.font = towerNameText != null ? towerNameText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = alignment;
        label.color = Color.white;
        label.text = value;
        label.raycastTarget = false;
        return label;
    }

    private static GameObject CreateUiObject(string name, Transform parent, params System.Type[] components)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = 5;
        gameObject.transform.SetParent(parent, false);
        foreach (System.Type component in components) gameObject.AddComponent(component);
        return gameObject;
    }
}
