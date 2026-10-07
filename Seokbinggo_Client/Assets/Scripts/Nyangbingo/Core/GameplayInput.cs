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
        private static int escapeConsumedFrame = -1;

        // Cancel handlers run in Update. The shell consumes only the remaining Escape in LateUpdate.
        public static bool TryConsumeEscape()
        {
            if (escapeConsumedFrame == Time.frameCount || !GetKeyDown(KeyCode.Escape)) return false;
            escapeConsumedFrame = Time.frameCount;
            return true;
        }

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
            escapeConsumedFrame = -1;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Nyangbingo.Core
{
    public enum DevelopmentShortcut
    {
        SelectBoss, CraftSummon, SummonMaterials, SummonItem, BossStation, DeepAltar,
        GoblinBoss, BulgasariBoss, ImugiBoss, ClearYokai, DefeatBoss, ForcedAnchor, SummonAnchor,
        Eoduksini, Gangcheori, Gaekgwi, EoduksiniKit, RoofKit,
        MiningBreak, MiningCritical, TowerItem, TowerMaterials, TowerFuel,
        ArtItems, Rope, Magpie, RecipeMaterials, RecipeStation, Samdugumi, Eop, MapSave, MapLoad
    }

    [System.Flags]
    public enum DevelopmentModifiers { None = 0, Shift = 1, Control = 2, Alt = 4 }

    // F5 설명과 각 컴포넌트의 입력은 이 단일 표를 사용한다.
    public static class DevelopmentShortcuts
    {
        public readonly struct Binding
        {
            public readonly DevelopmentShortcut Action;
            public readonly KeyCode Key;
            public readonly DevelopmentModifiers Modifiers;
            public readonly string Description;
            public Binding(DevelopmentShortcut action, KeyCode key, DevelopmentModifiers modifiers, string description)
            { Action = action; Key = key; Modifiers = modifiers; Description = description; }
            public string Label =>
                ((Modifiers & DevelopmentModifiers.Control) != 0 ? "Ctrl+" : "") +
                ((Modifiers & DevelopmentModifiers.Alt) != 0 ? "Alt+" : "") +
                ((Modifiers & DevelopmentModifiers.Shift) != 0 ? "Shift+" : "") + Key;
        }

        private static readonly Binding[] bindings =
        {
            new(DevelopmentShortcut.SelectBoss, KeyCode.F6, DevelopmentModifiers.None, "다음 보스 선택 패널"),
            new(DevelopmentShortcut.CraftSummon, KeyCode.F6, DevelopmentModifiers.Shift, "선택 소환템 제작(제작대·재료 필요)"),
            new(DevelopmentShortcut.SummonMaterials, KeyCode.F6, DevelopmentModifiers.Control, "선택 보스 소환 재료 지급"),
            new(DevelopmentShortcut.SummonItem, KeyCode.F6, DevelopmentModifiers.Alt, "선택 보스 소환템 지급"),
            new(DevelopmentShortcut.BossStation, KeyCode.F6, DevelopmentModifiers.Control | DevelopmentModifiers.Shift, "선택 보스 제작대 이동"),
            new(DevelopmentShortcut.DeepAltar, KeyCode.F6, DevelopmentModifiers.Alt | DevelopmentModifiers.Shift, "깊은 제단 이동(생성된 제단 필요)"),
            new(DevelopmentShortcut.GoblinBoss, KeyCode.F7, DevelopmentModifiers.None, "도깨비 대장(밤·활성 보스 없음)"),
            new(DevelopmentShortcut.BulgasariBoss, KeyCode.F7, DevelopmentModifiers.Shift, "어미 불가사리(밤·활성 보스 없음)"),
            new(DevelopmentShortcut.ImugiBoss, KeyCode.F7, DevelopmentModifiers.Control, "강철이(밤·활성 보스 없음)"),
            new(DevelopmentShortcut.ClearYokai, KeyCode.F8, DevelopmentModifiers.None, "일반 요괴 정리"),
            new(DevelopmentShortcut.DefeatBoss, KeyCode.F8, DevelopmentModifiers.Shift, "활성 보스 처치"),
            new(DevelopmentShortcut.ForcedAnchor, KeyCode.F8, DevelopmentModifiers.Control, "[확장] 내습 밤 50/60/90/100 이동"),
            new(DevelopmentShortcut.SummonAnchor, KeyCode.F8, DevelopmentModifiers.Alt, "[확장] 소환 밤 70/80 이동"),
            new(DevelopmentShortcut.Eoduksini, KeyCode.F9, DevelopmentModifiers.None, "어둑시니 소환(유효 지형 필요)"),
            new(DevelopmentShortcut.Gangcheori, KeyCode.F9, DevelopmentModifiers.Shift, "이무기 소환(유효 지형 필요)"),
            new(DevelopmentShortcut.Gaekgwi, KeyCode.F9, DevelopmentModifiers.Control, "객귀 소환(유효 지형 필요)"),
            new(DevelopmentShortcut.EoduksiniKit, KeyCode.F9, DevelopmentModifiers.Alt, "어둑시니 테스트 키트 지급"),
            new(DevelopmentShortcut.RoofKit, KeyCode.F9, DevelopmentModifiers.Control | DevelopmentModifiers.Shift, "차열 지붕 테스트 키트 지급"),
            new(DevelopmentShortcut.MiningBreak, KeyCode.F10, DevelopmentModifiers.None, "채굴 파괴 연출 미리보기"),
            new(DevelopmentShortcut.MiningCritical, KeyCode.F10, DevelopmentModifiers.Shift, "채굴 치명타 연출 미리보기"),
            new(DevelopmentShortcut.TowerItem, KeyCode.F10, DevelopmentModifiers.Control, "등탑 지급"),
            new(DevelopmentShortcut.TowerMaterials, KeyCode.F10, DevelopmentModifiers.Alt, "등탑 제작 재료 지급"),
            new(DevelopmentShortcut.TowerFuel, KeyCode.F10, DevelopmentModifiers.Control | DevelopmentModifiers.Shift, "등탑 연료 지급"),
            new(DevelopmentShortcut.ArtItems, KeyCode.F11, DevelopmentModifiers.None, "아트 확인용 아이템 지급"),
            new(DevelopmentShortcut.Rope, KeyCode.F11, DevelopmentModifiers.Shift, "로프 99개 지급"),
            new(DevelopmentShortcut.Magpie, KeyCode.F11, DevelopmentModifiers.Control, "까치 테스트 활성/비활성"),
            new(DevelopmentShortcut.RecipeMaterials, KeyCode.F12, DevelopmentModifiers.None, "제작·제련·장비창: 선택 재료/장비 지급"),
            new(DevelopmentShortcut.RecipeStation, KeyCode.F12, DevelopmentModifiers.Shift, "제작·제련창: 필요한 제작대 이동"),
            new(DevelopmentShortcut.Samdugumi, KeyCode.F12, DevelopmentModifiers.Control, "[확장] 삼두구미(밤·활성 보스 없음)"),
            new(DevelopmentShortcut.Eop, KeyCode.F12, DevelopmentModifiers.Alt, "[확장] 업구렁이(밤·활성 보스 없음)"),
            new(DevelopmentShortcut.MapSave, KeyCode.F1, DevelopmentModifiers.Control | DevelopmentModifiers.Alt, "[DevA 테스트씬 전용] 저장"),
            new(DevelopmentShortcut.MapLoad, KeyCode.F2, DevelopmentModifiers.Control | DevelopmentModifiers.Alt, "[DevA 테스트씬 전용] 불러오기")
        };
        public static System.Collections.Generic.IReadOnlyList<Binding> Bindings => System.Array.AsReadOnly(bindings);
        public const int HelpPageSize = 16;
        public static int HelpPageCount => (bindings.Length + HelpPageSize - 1) / HelpPageSize;

        public static bool Matches(Binding binding, KeyCode key, DevelopmentModifiers modifiers) =>
            binding.Key == key && binding.Modifiers == modifiers;

        public static bool IsPressed(DevelopmentShortcut action)
        {
            if (Nyangbingo.UI.MainGameBossSummonUiController.IsDebugShortcutHelpOpen || Time.timeScale <= 0f)
                return false;
            var modifiers = DevelopmentModifiers.None;
            if (GameplayInput.GetKey(KeyCode.LeftShift) || GameplayInput.GetKey(KeyCode.RightShift)) modifiers |= DevelopmentModifiers.Shift;
            if (GameplayInput.GetKey(KeyCode.LeftControl) || GameplayInput.GetKey(KeyCode.RightControl)) modifiers |= DevelopmentModifiers.Control;
            if (GameplayInput.GetKey(KeyCode.LeftAlt) || GameplayInput.GetKey(KeyCode.RightAlt)) modifiers |= DevelopmentModifiers.Alt;
            foreach (var binding in bindings)
                if (binding.Action == action)
                    return Matches(binding, binding.Key, modifiers) && GameplayInput.GetKeyDown(binding.Key);
            return false;
        }

        public static bool CanUseWorldShortcuts => Time.timeScale > 0f &&
            !Nyangbingo.UI.MainGameBossSummonUiController.IsDebugShortcutHelpOpen &&
            !Nyangbingo.UI.MainGameCraftingUiController.BlocksGameplayInput;

        public static string GetHelpText(int page)
        {
            var text = new System.Text.StringBuilder();
            // 안내도 실행 코드의 동일 키 표에서 생성해 오래된 키가 남지 않게 한다.
            text.AppendLine("F5를 닫고 Game 뷰를 클릭한 뒤 입력하세요.");
            text.AppendLine("F12 제작 테스트는 해당 창을 먼저 여세요.\n");
            var start = Mathf.Clamp(page, 0, HelpPageCount - 1) * HelpPageSize;
            for (var i = start; i < Mathf.Min(start + HelpPageSize, bindings.Length); i++)
                text.AppendLine(bindings[i].Label + "  " + bindings[i].Description);
            return text.ToString();
        }
    }
}
#endif
