using System;
using Game.Grid;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// Единственный читатель ввода. На телефоне работает палец, в редакторе — мышь:
    /// одиночное касание тянет камеру, щипок двумя пальцами зумит, короткий тап — это клик.
    /// </summary>
    public sealed class GameInput : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;
        [SerializeField] float clickThresholdPixels = 12f;
        [SerializeField] float pinchSensitivity = 0.01f;

        float draggedDistance;
        float previousPinchDistance;
        bool pressed;
        bool pinching;

        /// <summary>Короткое касание или клик без перетаскивания: экранная позиция.</summary>
        public event Action<Vector2> Clicked;

        /// <summary>Перетаскивание: смещение в пикселях за кадр.</summary>
        public event Action<Vector2> Dragged;

        /// <summary>Зум: положительное значение приближает.</summary>
        public event Action<float> Zoomed;

        /// <summary>Плитка под экранной точкой.</summary>
        public HexCoord CoordAt(Vector2 screenPosition)
        {
            var distanceToPlane = -worldCamera.transform.position.z;
            var world = worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distanceToPlane));
            return HexCoord.FromWorld(world);
        }

        void Update()
        {
            if (ReadTouch())
                return;

            ReadMouse();
        }

        /// <summary>Возвращает true, если экран трогают пальцем: тогда мышь не опрашиваем.</summary>
        bool ReadTouch()
        {
            var screen = Touchscreen.current;
            if (screen == null)
                return false;

            var first = screen.touches[0];
            var second = screen.touches[1];

            if (first.press.isPressed && second.press.isPressed)
            {
                Pinch(first.position.ReadValue(), second.position.ReadValue());
                return true;
            }

            if (!first.press.isPressed)
            {
                if (pressed)
                    EndPress(first.position.ReadValue());

                pinching = false;
                previousPinchDistance = 0f;
                return pressed = false;
            }

            if (first.press.wasPressedThisFrame)
                BeginPress();

            Drag(first.delta.ReadValue());
            return true;
        }

        void ReadMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            var scroll = mouse.scroll.ReadValue().y;
            if (!Mathf.Approximately(scroll, 0f))
                Zoomed?.Invoke(Mathf.Sign(scroll));

            if (mouse.leftButton.wasPressedThisFrame)
                BeginPress();

            if (pressed && mouse.leftButton.isPressed)
                Drag(mouse.delta.ReadValue());

            if (pressed && mouse.leftButton.wasReleasedThisFrame)
            {
                EndPress(mouse.position.ReadValue());
                pressed = false;
            }
        }

        void BeginPress()
        {
            pressed = true;
            pinching = false;
            draggedDistance = 0f;
        }

        void Drag(Vector2 delta)
        {
            if (!pressed || delta == Vector2.zero)
                return;

            draggedDistance += delta.magnitude;
            Dragged?.Invoke(delta);
        }

        void EndPress(Vector2 screenPosition)
        {
            if (!pinching && draggedDistance <= clickThresholdPixels)
                Clicked?.Invoke(screenPosition);
        }

        void Pinch(Vector2 first, Vector2 second)
        {
            var distance = Vector2.Distance(first, second);
            if (previousPinchDistance > 0f)
                Zoomed?.Invoke((distance - previousPinchDistance) * pinchSensitivity);

            previousPinchDistance = distance;
            pinching = true;
            pressed = false;
        }
    }
}
