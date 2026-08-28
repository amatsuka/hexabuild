using System;
using Game.Grid;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>Единственный читатель мыши: отделяет клик по плитке от перетаскивания камеры.</summary>
    public sealed class GameInput : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;
        [SerializeField] float clickThresholdPixels = 6f;

        float draggedDistance;
        bool pressed;

        /// <summary>Клик левой кнопкой по плитке без перетаскивания.</summary>
        public event Action<HexCoord> TileClicked;

        /// <summary>Перетаскивание левой кнопкой: смещение курсора в пикселях за кадр.</summary>
        public event Action<Vector2> Dragged;

        /// <summary>Прокрутка колеса: положительное значение — приближение.</summary>
        public event Action<float> Zoomed;

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            var scroll = mouse.scroll.ReadValue().y;
            if (!Mathf.Approximately(scroll, 0f))
                Zoomed?.Invoke(scroll);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressed = true;
                draggedDistance = 0f;
            }

            if (pressed && mouse.leftButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                draggedDistance += delta.magnitude;
                if (delta != Vector2.zero)
                    Dragged?.Invoke(delta);
            }

            if (pressed && mouse.leftButton.wasReleasedThisFrame)
            {
                pressed = false;
                if (draggedDistance <= clickThresholdPixels)
                    TileClicked?.Invoke(CoordUnderCursor(mouse.position.ReadValue()));
            }
        }

        HexCoord CoordUnderCursor(Vector2 screenPosition)
        {
            var distanceToPlane = -worldCamera.transform.position.z;
            var world = worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distanceToPlane));
            return HexCoord.FromWorld(world);
        }
    }
}
