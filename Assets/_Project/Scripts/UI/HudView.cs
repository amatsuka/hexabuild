using Game.Economy;
using Game.Storage;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Минимальный текстовый HUD: счётчики партии и короткое сообщение об отказе.</summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] Color textColor = new(0.92f, 0.92f, 0.94f);
        [SerializeField] Color messageColor = new(0.95f, 0.55f, 0.45f);
        [SerializeField] int fontSize = 18;
        [SerializeField] float messageSeconds = 2f;

        Text stats;
        Text message;
        float messageTimer;
        Wallet wallet;
        StorageGrid storage;

        public void Bind(Wallet boundWallet, StorageGrid boundStorage)
        {
            wallet = boundWallet;
            storage = boundStorage;

            stats = CreateLine("Stats", new Vector2(20f, -20f), textColor);
            message = CreateLine("Message", new Vector2(20f, -20f - fontSize * 1.6f), messageColor);
            message.text = string.Empty;

            wallet.Changed += Refresh;
            storage.Changed += Refresh;
            Refresh();
        }

        public void ShowMessage(string text)
        {
            message.text = text;
            messageTimer = messageSeconds;
        }

        void OnDestroy()
        {
            if (wallet == null)
                return;

            wallet.Changed -= Refresh;
            storage.Changed -= Refresh;
        }

        void Update()
        {
            if (messageTimer <= 0f)
                return;

            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f)
                message.text = string.Empty;
        }

        void Refresh()
        {
            stats.text = $"Очки: {wallet.Points}" +
                         $"   Щебень: {storage.CountOf(ResourceType.Gravel)}" +
                         $"   Доски: {storage.CountOf(ResourceType.Board)}" +
                         $"   Слитки: {storage.CountOf(ResourceType.Ingot)}" +
                         $"   Потеряно: {storage.LostCount}" +
                         $"   Склад: {storage.Count}/{storage.Capacity}";
        }

        Text CreateLine(string lineName, Vector2 position, Color color)
        {
            var line = new GameObject(lineName, typeof(RectTransform), typeof(Text));
            line.transform.SetParent(transform, false);

            var rect = (RectTransform)line.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(900f, fontSize * 1.6f);

            var text = line.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }
    }
}
