using UnityEngine;

public class SpawnArrow : UnityEngine.UI.MaskableGraphic
{
    private Transform player;
    private Canvas rootCanvas;
    private RectTransform canvasRect;

    public static SpawnArrow Create(Transform player, Canvas canvas)
    {
        if (player == null || canvas == null) return null;
        var arrowObject = new GameObject("Spawn Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(SpawnArrow));
        arrowObject.layer = canvas.gameObject.layer;
        SpawnArrow arrow = arrowObject.GetComponent<SpawnArrow>();
        arrow.player = player;
        arrow.rootCanvas = canvas;
        arrow.canvasRect = canvas.GetComponent<RectTransform>();
        arrow.raycastTarget = false;
        arrow.rectTransform.SetParent(canvas.transform, false);
        arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = arrow.canvasRect.pivot;
        arrow.rectTransform.pivot = new Vector2(0.5f, 0f);
        arrow.rectTransform.sizeDelta = new Vector2(36, 44);
        arrow.LateUpdate();
        return arrow;
    }

    private void LateUpdate()
    {
        if (player == null || rootCanvas == null || Camera.main == null) return;
        Vector2 screenPosition = Camera.main.WorldToScreenPoint(player.position + Vector3.up * 0.6f);
        Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Vector2 position;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out position)) return;
        Rect bounds = canvasRect.rect;
        Rect arrowBounds = rectTransform.rect;
        position.x = Mathf.Clamp(position.x, bounds.xMin + arrowBounds.width / 2 + 6, bounds.xMax - arrowBounds.width / 2 - 6);
        position.y = Mathf.Clamp(position.y, bounds.yMin + 6, bounds.yMax - arrowBounds.height - 6);
        rectTransform.anchoredPosition = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
    }

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vertices)
    {
        vertices.Clear();
        Color32 outline = new Color32(52, 26, 20, 255);
        Color32 fill = new Color32(255, 218, 102, 255);
        AddRect(vertices, new Rect(-7, 20, 14, 24), outline);
        AddRect(vertices, new Rect(-18, 20, 36, 4), outline);
        for (int row = 0; row < 5; row++)
        {
            float width = 4 + row * 8;
            AddRect(vertices, new Rect(-width / 2, row * 4, width, 4), outline);
        }
        AddRect(vertices, new Rect(-3, 20, 6, 20), fill);
        for (int row = 0; row < 4; row++)
        {
            float width = 4 + row * 8;
            AddRect(vertices, new Rect(-width / 2, 4 + row * 4, width, 4), fill);
        }
    }

    private static void AddRect(UnityEngine.UI.VertexHelper vertices, Rect rect, Color32 colour)
    {
        int start = vertices.currentVertCount;
        vertices.AddVert(new Vector3(rect.xMin, rect.yMin, 0), colour, Vector4.zero);
        vertices.AddVert(new Vector3(rect.xMin, rect.yMax, 0), colour, Vector4.zero);
        vertices.AddVert(new Vector3(rect.xMax, rect.yMax, 0), colour, Vector4.zero);
        vertices.AddVert(new Vector3(rect.xMax, rect.yMin, 0), colour, Vector4.zero);
        vertices.AddTriangle(start, start + 1, start + 2);
        vertices.AddTriangle(start + 2, start + 3, start);
    }
}
