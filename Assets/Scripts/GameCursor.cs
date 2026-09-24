using UnityEngine;

public sealed class GameCursor : MonoBehaviour
{
    private static GameCursor instance;

    [SerializeField] private Sprite cursorSprite;
    [SerializeField] private Vector2 hotspot = new Vector2(12f, 8f);

    private Texture2D runtimeCursor;

    public bool IsApplied => runtimeCursor != null;
    public Vector2Int CursorSize => runtimeCursor != null
        ? new Vector2Int(runtimeCursor.width, runtimeCursor.height)
        : Vector2Int.zero;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        ApplyCursor();
    }

    private void ApplyCursor()
    {
        if (cursorSprite == null)
        {
            Debug.LogWarning("Game cursor sprite is not assigned.", this);
            return;
        }

        Rect sourceRect = cursorSprite.textureRect;
        int width = Mathf.RoundToInt(sourceRect.width);
        int height = Mathf.RoundToInt(sourceRect.height);
        int sourceX = Mathf.RoundToInt(sourceRect.x);
        int sourceY = Mathf.RoundToInt(sourceRect.y);

        runtimeCursor = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = $"{cursorSprite.name}_RuntimeCursor",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        runtimeCursor.SetPixels(cursorSprite.texture.GetPixels(sourceX, sourceY, width, height));
        runtimeCursor.Apply(false, false);

        Vector2 clampedHotspot = new Vector2(
            Mathf.Clamp(hotspot.x, 0f, width - 1f),
            Mathf.Clamp(hotspot.y, 0f, height - 1f));
        Cursor.SetCursor(runtimeCursor, clampedHotspot, CursorMode.Auto);
    }

    private void OnDestroy()
    {
        if (instance != this) return;

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        if (runtimeCursor != null) Destroy(runtimeCursor);
        instance = null;
    }
}
