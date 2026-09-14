using UnityEngine;
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

    public string WaveLabel => waveText.text;
    public string HealthLabel => healthText.text;
    public string GoldLabel => goldText.text;
    public string ResultLabel => resultText.text;
    public bool ResultVisible => resultPanel.activeSelf;
    public bool UpgradePanelVisible => upgradePanel != null && upgradePanel.activeSelf;
    public TowerBase SelectedTower => selectedTower;
    public string UpgradeButtonLabel => upgradeButtonText != null ? upgradeButtonText.text : string.Empty;

    private void OnEnable()
    {
        if (upgradeButton != null) upgradeButton.onClick.AddListener(UpgradeSelectedTower);
        if (closeUpgradeButton != null) closeUpgradeButton.onClick.AddListener(CloseTowerPanel);
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
            resultText.text = won ? "Win" : "Lose";
            resultText.color = won ? new Color(0.38f, 0.94f, 0.73f) : new Color(1f, 0.42f, 0.40f);
            resultDetails.text = won ? "Tüm dalgalar tamamlandı" : "Canın tükendi";
        }
        if ((won || lost) && UpgradePanelVisible) CloseTowerPanel();
        else RefreshUpgradePanel();
        statusText.text = waves.State switch
        {
            WaveController.WaveState.Preparing => "Düşmanlar hazırlanıyor",
            WaveController.WaveState.Active => $"Kalan düşman: {waves.RemainingEnemies}",
            WaveController.WaveState.Completed => "Tüm dalgalar tamamlandı",
            WaveController.WaveState.Defeated => "Savunma düştü",
            _ => ""
        };
    }

    public void OpenTowerPanel(TowerBase tower)
    {
        if (tower == null || ResultVisible) return;
        if (selectedTower != null) selectedTower.Upgraded -= OnTowerUpgraded;
        selectedTower = tower;
        selectedTower.Upgraded += OnTowerUpgraded;
        upgradePanel.SetActive(true);
        RefreshUpgradePanel();
    }

    public void CloseTowerPanel()
    {
        if (selectedTower != null) selectedTower.Upgraded -= OnTowerUpgraded;
        selectedTower = null;
        if (upgradePanel != null) upgradePanel.SetActive(false);
    }

    public void UpgradeSelectedTower()
    {
        if (selectedTower == null || playerGold == null) return;
        selectedTower.TryUpgrade(playerGold);
        RefreshUpgradePanel();
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
        return IsPointerOver(upgradePanel.transform as RectTransform, screenPosition);
    }

    public bool IsPointerOverTowerPanel(Vector2 screenPosition) =>
        UpgradePanelVisible && IsPointerOver(upgradePanel.transform as RectTransform, screenPosition);

    private bool IsPointerOver(RectTransform rect, Vector2 screenPosition)
    {
        if (rect == null) return false;
        Canvas canvas = GetComponent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, eventCamera)) return true;

        // In the Editor, Game View input coordinates can differ from the Canvas render size.
        // Map both spaces so UI clicks remain reliable at every Game View resolution.
        if (canvas == null || Screen.width <= 0 || Screen.height <= 0) return false;
        Rect pixels = canvas.pixelRect;
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
    }
}
