using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Держит UI внутри безопасной зоны экрана: на телефоне сверху вырез камеры, снизу полоса
    /// жеста. Пересчитывается при смене размера экрана, а не каждый кадр.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect appliedArea;
        Vector2Int appliedResolution;

        void OnEnable() => Apply();

        void Update()
        {
            if (appliedArea != Screen.safeArea || appliedResolution.x != Screen.width || appliedResolution.y != Screen.height)
                Apply();
        }

        void Apply()
        {
            appliedArea = Screen.safeArea;
            appliedResolution = new Vector2Int(Screen.width, Screen.height);

            if (appliedResolution.x <= 0 || appliedResolution.y <= 0)
                return;

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(appliedArea.xMin / appliedResolution.x, appliedArea.yMin / appliedResolution.y);
            rect.anchorMax = new Vector2(appliedArea.xMax / appliedResolution.x, appliedArea.yMax / appliedResolution.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
