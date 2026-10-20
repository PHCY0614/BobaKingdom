using TMPro;
using UnityEngine;

public class FloatingScore : MonoBehaviour
{
    private TextMeshProUGUI label;
    private Vector2 startPosition;
    private float elapsed;
    private const float Lifetime = 0.6f;

    public static void Show(int points, Vector3 worldPosition, TextMeshProUGUI style)
    {
        if (points <= 0 || style == null || style.canvas == null || Camera.main == null) return;
        Canvas canvas = style.canvas.rootCanvas;
        RectTransform parent = canvas.GetComponent<RectTransform>();
        Vector2 screenPosition = Camera.main.WorldToScreenPoint(worldPosition + Vector3.up * 1.15f);
        Vector2 position;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, uiCamera, out position)) return;

        GameObject popup = new GameObject("FloatingScore", typeof(RectTransform), typeof(TextMeshProUGUI));
        popup.layer = canvas.gameObject.layer;
        RectTransform rect = popup.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = parent.pivot;
        rect.sizeDelta = new Vector2(80f, 28f);
        rect.anchoredPosition = position;
        TextMeshProUGUI text = popup.GetComponent<TextMeshProUGUI>();
        text.font = style.font;
        text.color = style.color;
        text.fontSize = 16f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = "+" + points;
        FloatingScore floating = popup.AddComponent<FloatingScore>();
        floating.label = text;
        floating.startPosition = position;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(elapsed / Lifetime);
        label.rectTransform.anchoredPosition = startPosition + Vector2.up * (12f * progress);
        label.alpha = 1f - progress;
        if (elapsed >= Lifetime) Destroy(gameObject);
    }
}
