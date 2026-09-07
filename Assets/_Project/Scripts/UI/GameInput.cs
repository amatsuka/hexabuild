using System;
using Game.Grid;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// Единственный читатель ввода. Работает любым указателем: палец на телефоне, мышь в
    /// редакторе и в браузере на десктопе. Одиночное нажатие тянет камеру, щипок двумя
    /// пальцами и колесо зумят, короткое нажатие без протяжки — это клик.
    /// </summary>
    public sealed class GameInput : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;
        [SerializeField] float clickThresholdPixels = 12f;
        [SerializeField] float pinchSensitivity = 0.01f;

        // Указатель, который держит нажатие. Нажатие целиком принадлежит ему: браузер на
        // десктопе заводит устройство `Touchscreen` рядом с мышью, и разбор «сначала палец,
        // потом мышь» ронял мышиное нажатие нетронутым пальцем в тот же кадр.
        Pointer holding;

        Vector2 lastPosition;
        float draggedDistance;
        float previousPinchDistance;

        // Нажатие объявлено и ещё не снято. Снять его можно тремя разными путями — палец
        // отпустили, нажатие сорвалось в протяжку, указатель исчез, — и флаг держит их все
        // в одном русле: `PressEnded` уходит ровно один раз на одно `Pressed`.
        bool pressing;

        /// <summary>Короткое касание или клик без перетаскивания: экранная позиция.</summary>
        public event Action<Vector2> Clicked;

        /// <summary>
        /// Палец лёг на экран: экранная позиция. Отклик на нажатие идёт отсюда, а не с
        /// <see cref="Clicked"/>: тот приходит на отпускании, то есть после того, как игрок
        /// уже успел решить, что игра его не услышала.
        /// </summary>
        public event Action<Vector2> Pressed;

        /// <summary>
        /// Нажатие снято: палец отпущен, сорвался в протяжку или щипок, либо указатель исчез.
        /// Приходит ровно один раз на каждое <see cref="Pressed"/> и всегда до
        /// <see cref="Clicked"/> — прижатое отпускается раньше, чем срабатывает действие.
        /// </summary>
        public event Action PressEnded;

        /// <summary>Перетаскивание: смещение в пикселях за кадр.</summary>
        public event Action<Vector2> Dragged;

        /// <summary>Зум: положительное значение приближает.</summary>
        public event Action<float> Zoomed;

        /// <summary>
        /// Плитка под экранной точкой на заданной высоте. Камера смотрит на поле под углом,
        /// поэтому отсчёт по её глубине не даёт точку земли — берём пересечение луча с
        /// горизонтальной плоскостью. Высота здесь не украшение: по плоскости `y = 0` луч
        /// промахивается мимо приподнятой плитки, а какую именно плоскость брать, решает
        /// <see cref="Game.Grid.TilePicker"/> — он же и знает высоты поля.
        /// </summary>
        public HexCoord CoordAt(Vector2 screenPosition, float groundHeight)
        {
            var ray = worldCamera.ScreenPointToRay(screenPosition);
            if (!new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f)).Raycast(ray, out var distance))
                return HexCoord.Zero;

            return HexCoord.FromWorld(ray.GetPoint(distance));
        }

        void Update()
        {
            if (ReadPinch())
                return;

            previousPinchDistance = 0f;
            ReadWheel();
            ReadPointer();
        }

        /// <summary>Щипок двумя пальцами. Возвращает true, пока он идёт: тогда остальное молчит.</summary>
        bool ReadPinch()
        {
            var screen = Touchscreen.current;
            if (screen == null)
                return false;

            var first = screen.touches[0];
            var second = screen.touches[1];
            if (!first.press.isPressed || !second.press.isPressed)
                return false;

            var distance = Vector2.Distance(first.position.ReadValue(), second.position.ReadValue());
            if (previousPinchDistance > 0f)
                Zoomed?.Invoke((distance - previousPinchDistance) * pinchSensitivity);

            previousPinchDistance = distance;

            // Второй палец отменяет начатое нажатие: щипок не должен закончиться кликом.
            holding = null;
            EndPress();
            return true;
        }

        void ReadWheel()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            var scroll = mouse.scroll.ReadValue().y;
            if (!Mathf.Approximately(scroll, 0f))
                Zoomed?.Invoke(Mathf.Sign(scroll));
        }

        void ReadPointer()
        {
            if (holding != null && !holding.added)
            {
                holding = null;
                EndPress();
            }

            if (holding == null)
            {
                holding = FindPress();
                if (holding == null)
                    return;

                lastPosition = holding.position.ReadValue();
                draggedDistance = 0f;
                pressing = true;
                Pressed?.Invoke(lastPosition);
                return;
            }

            var position = holding.position.ReadValue();
            if (!holding.press.isPressed)
            {
                EndPress();
                if (draggedDistance <= clickThresholdPixels)
                    Clicked?.Invoke(position);

                holding = null;
                return;
            }

            // Смещение считаем по позициям, а не по `delta` устройства: в вебе она приходит
            // в собственном масштабе, и клик мышью уезжал за порог протяжки.
            var moved = position - lastPosition;
            if (moved == Vector2.zero)
                return;

            lastPosition = position;
            draggedDistance += moved.magnitude;

            // Протяжка перевесила клик: это уже пан камеры, и прижатое под пальцем отпускается,
            // не дожидаясь, пока палец оторвут.
            if (draggedDistance > clickThresholdPixels)
                EndPress();

            Dragged?.Invoke(moved);
        }

        void EndPress()
        {
            if (!pressing)
                return;

            pressing = false;
            PressEnded?.Invoke();
        }

        /// <summary>Указатель, нажатый в этом кадре: палец, мышь или перо — что окажется первым.</summary>
        static Pointer FindPress()
        {
            var screen = Touchscreen.current;
            if (screen != null && screen.press.wasPressedThisFrame)
                return screen;

            var mouse = Mouse.current;
            if (mouse != null && mouse.press.wasPressedThisFrame)
                return mouse;

            var pointer = Pointer.current;
            return pointer != null && pointer.press.wasPressedThisFrame ? pointer : null;
        }
    }
}
