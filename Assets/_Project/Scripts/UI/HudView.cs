using Game.Core;
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
        [SerializeField] Color contractColor = new(0.96f, 0.92f, 0.76f);
        [SerializeField] int fontSize = 40;
        [SerializeField] float messageSeconds = 2f;

        Text points;
        Text resources;
        Text contract;
        Text message;
        float messageTimer;
        int shownSeconds = -1;
        GameState state;
        ContractSystem contracts;
        Wallet wallet;
        StorageGrid storage;

        public void Bind(GameState game, ContractSystem contractSystem)
        {
            state = game;
            contracts = contractSystem;
            wallet = game.Wallet;
            storage = game.Storage;

            points = CreateLine("Stats", new Vector2(24f, -24f), textColor);
            resources = CreateLine("Resources", new Vector2(24f, -24f - fontSize * 1.35f), textColor);
            contract = CreateLine("Contract", new Vector2(24f, -24f - fontSize * 2.7f), contractColor);
            message = CreateLine("Message", new Vector2(24f, -24f - fontSize * 4.05f), messageColor);
            message.text = string.Empty;

            wallet.Changed += Refresh;
            storage.Changed += Refresh;
            contracts.Issued += RefreshContract;
            contracts.Progressed += RefreshContract;
            contracts.Failed += RefreshContract;
            contracts.Completed += OnContractCompleted;
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
            contracts.Issued -= RefreshContract;
            contracts.Progressed -= RefreshContract;
            contracts.Failed -= RefreshContract;
            contracts.Completed -= OnContractCompleted;
        }

        void Update()
        {
            // Обратный отсчёт перерисовывается только на смене целой секунды: строка каждый кадр
            // ничего не добавляет глазу, зато мусорит строками в куче.
            if (contracts != null && contracts.IsActive && Mathf.CeilToInt(contracts.SecondsLeft) != shownSeconds)
                RefreshContract();

            if (messageTimer <= 0f)
                return;

            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f)
                message.text = string.Empty;
        }

        /// <summary>Несколько строк: на узком экране всё это в одну не помещается.</summary>
        void Refresh()
        {
            points.text = $"Очки: {wallet.Points}   Плитка: {state.NextTileCost}" +
                          $"   Склад: {storage.Count}/{storage.Capacity}   Потеряно: {storage.LostCount}";
            resources.text = $"Щебень: {storage.CountOf(ResourceType.Gravel)}" +
                             $"   Доски: {storage.CountOf(ResourceType.Board)}" +
                             $"   Слитки: {storage.CountOf(ResourceType.Ingot)}";
            RefreshContract();
        }

        /// <summary>Строка контракта: что просят, сколько сдано и сколько осталось времени.</summary>
        void RefreshContract()
        {
            if (!contracts.IsActive)
            {
                contract.text = string.Empty;
                shownSeconds = -1;
                return;
            }

            shownSeconds = Mathf.CeilToInt(contracts.SecondsLeft);
            contract.text = $"Контракт: {NameOf(contracts.Type)} {contracts.Delivered}/{contracts.Goal}" +
                            $"   {shownSeconds} с   +{contracts.Reward}";
        }

        void OnContractCompleted(int reward) => ShowMessage($"Контракт закрыт: +{reward} очков");

        static string NameOf(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Board:
                    return "доски";
                case ResourceType.Gravel:
                    return "щебень";
                case ResourceType.Ingot:
                    return "слитки";
                default:
                    return type.ToString();
            }
        }

        Text CreateLine(string lineName, Vector2 position, Color color)
        {
            var line = new GameObject(lineName, typeof(RectTransform), typeof(Text));
            line.transform.SetParent(transform, false);

            var rect = (RectTransform)line.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(1000f, fontSize * 1.3f);

            var text = line.GetComponent<Text>();
            text.font = UiFont.Shared;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }
    }
}
