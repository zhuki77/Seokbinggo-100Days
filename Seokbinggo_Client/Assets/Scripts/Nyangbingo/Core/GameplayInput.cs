using UnityEngine;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using System.Collections.Generic;
#endif

namespace Nyangbingo.Core
{
    // Normal builds read Unity input directly. Editor replay changes only input,
    // never inventory, health, movement transforms, or game progression.
    public static class GameplayInput
    {
#if UNITY_EDITOR
        private static readonly HashSet<KeyCode> held = new();
        private static readonly HashSet<KeyCode> pressed = new();
        private static int sampleFrame = -1;
        private static float horizontal;
        private static bool primary, secondary, primaryDown, secondaryDown;
        private static Vector3 pointer;
        public static bool ReplayActive { get; private set; }
        private static readonly List<RaycastResult> uiHits = new();
        private static bool Fresh => sampleFrame == Time.frameCount;
        // Physics may run before the next Update sample. Keep held state for that
        // boundary only; edge-triggered presses remain confined to their sample frame.
        private static bool HeldFresh => sampleFrame >= 0 && Time.frameCount - sampleFrame <= 1;

        public static void BeginReplay()
        {
            EndReplay();
            ReplayActive = true;
        }

        // Call once before gameplay Update on every replay frame. A missed frame
        // expires held input after one physics-boundary frame instead of leaving it stuck.
        public static void Sample(float axis, Vector3 screenPointer, bool left, bool right,
            params KeyCode[] keys)
        {
            if (!ReplayActive) throw new System.InvalidOperationException("Replay is not active.");
            if (sampleFrame == Time.frameCount)
                throw new System.InvalidOperationException("Only one replay sample per frame is allowed.");
            if (!HeldFresh)
            {
                held.Clear();
                primary = secondary = false;
            }
            pressed.Clear();
            foreach (var key in keys) if (!held.Contains(key)) pressed.Add(key);
            held.Clear();
            foreach (var key in keys) held.Add(key);
            primaryDown = left && !primary;
            secondaryDown = right && !secondary;
            primary = left;
            secondary = right;
            horizontal = Mathf.Clamp(axis, -1f, 1f);
            pointer = screenPointer;
            sampleFrame = Time.frameCount;
        }

        public static void EndReplay()
        {
            ReplayActive = false;
            held.Clear();
            pressed.Clear();
            primary = secondary = primaryDown = secondaryDown = false;
            horizontal = 0f;
            sampleFrame = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => EndReplay();
#endif
        public static bool IsPointerOverUi()
        {
            var events = EventSystem.current;
            if (events == null) return false;
#if UNITY_EDITOR
            if (ReplayActive)
            {
                // Use the same pointer for UI hit testing and world aiming.
                // Preserve actual UI occlusion instead of bypassing it for tests.
                uiHits.Clear();
                events.RaycastAll(new PointerEventData(events) { position = pointer }, uiHits);
                foreach (var hit in uiHits)
                    if (hit.module is UnityEngine.UI.GraphicRaycaster) return true;
                return false;
            }
#endif
            return events.IsPointerOverGameObject();
        }
        public static bool GetKey(KeyCode key)
        {
#if UNITY_EDITOR
            if (ReplayActive) return HeldFresh && held.Contains(key);
#endif
            return Input.GetKey(key);
        }
        public static bool GetKeyDown(KeyCode key)
        {
#if UNITY_EDITOR
            if (ReplayActive) return Fresh && pressed.Contains(key);
#endif
            return Input.GetKeyDown(key);
        }
        public static float GetAxisRaw(string axis)
        {
#if UNITY_EDITOR
            if (ReplayActive) return Fresh && axis == "Horizontal" ? horizontal : 0f;
#endif
            return Input.GetAxisRaw(axis);
        }
        public static bool GetMouseButton(int button)
        {
#if UNITY_EDITOR
            if (ReplayActive) return HeldFresh && (button == 0 ? primary : button == 1 && secondary);
#endif
            return Input.GetMouseButton(button);
        }
        public static bool GetMouseButtonDown(int button)
        {
#if UNITY_EDITOR
            if (ReplayActive) return Fresh && (button == 0 ? primaryDown : button == 1 && secondaryDown);
#endif
            return Input.GetMouseButtonDown(button);
        }
        public static Vector3 mousePosition
        {
            get
            {
#if UNITY_EDITOR
                if (ReplayActive) return pointer;
#endif
                return Input.mousePosition;
            }
        }
        public static Vector2 mouseScrollDelta
        {
            get
            {
#if UNITY_EDITOR
                if (ReplayActive) return Vector2.zero;
#endif
                return Input.mouseScrollDelta;
            }
        }
    }
}
