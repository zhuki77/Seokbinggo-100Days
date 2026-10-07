using System.Collections.Generic;
using Nyangbingo.Data;
using Nyangbingo.World;
using UnityEngine;
using Input = Nyangbingo.Core.GameplayInput;

namespace Nyangbingo.UI
{
    /// <summary>v86 S2. 예시·진단 표식만 그리며 설치 및 밀폐 규칙을 변경하지 않는다.</summary>
    public sealed class MainGameBuildingGuideUiController : MonoBehaviour
    {
        private MainGameRuntimeServices services;
        private MainGameBootstrap bootstrap;
        private GameDataCatalog catalog;
        private MainGamePlayerController player;
        private MainGameShellUiController shell;
        private MainGameTilePaletteController palette;
        private MainGameTurretRuntime placement;
        private Canvas canvas;
        private Camera worldCamera;
        private RectTransform worldRoot, toggle;
        private RectTransform miningWarning, recoveryPanel, baseArrow;
        private UnityEngine.UI.Image baseArrowImage;
        private UnityEngine.UI.Text recoveryText;
        private UnityEngine.UI.Image toggleIcon;
        private Sprite checkOn, checkOff;
        private IReadOnlyList<Sprite> leakFrames;
        private IReadOnlyList<Sprite> sealSuccessFrames;
        private RectTransform sealSuccess;
        private Vector3Int? successCore;
        private float successAnimationStarted;
        private const float SealSuccessFrameSeconds = .1f; // 납품 원본: 프레임당 100ms.
        private const float SealSuccessVisualScale = .75f;
        // 납품 파동의 전체 프레임 공통 바닥 기준: 16px 캔버스 아래 투명 여백 5px.
        private const float SealSuccessBottomPaddingPixels = 5f;
        private Sprite repairSprite;
        private bool outlineEnabled = true, g05Complete;
        private int restoreVersion = -1;
        private float nextEvaluation, successUntil;
        private readonly List<RectTransform> outlines = new List<RectTransform>();
        private readonly List<Vector3Int> outlineOffsets = new List<Vector3Int>();
        private readonly List<RectTransform> holes = new List<RectTransform>();
        private readonly List<RectTransform> doors = new List<RectTransform>();
        private readonly List<RepairView> repairs = new List<RepairView>();
        private sealed class RepairView
        { public Vector3Int Cell; public RectTransform Rect; public float Until, VisualYOffset; }
        public string CurrentMessage { get; private set; } = string.Empty;
        public bool IsPointerOverInteraction => toggle != null && toggle.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(toggle, Input.mousePosition, EventCamera);
        private Camera EventCamera => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        public void Configure(Canvas parent, UnityEngine.UI.Text template, GameDataCatalog data,
            MainGameRuntimeServices runtime, MainGameBootstrap mainBootstrap,
            MainGamePlayerController controller, GameplayArtCatalog art)
        {
            if (worldRoot != null || parent == null || template == null || runtime.BuildingGuide == null) return;
            canvas = parent; catalog = data; services = runtime; bootstrap = mainBootstrap; player = controller;
            worldCamera = Camera.main; checkOn = art?.ShellCheckOn; checkOff = art?.ShellCheckOff;
            leakFrames = art?.SealLeakMarkerFrames; repairSprite = art?.SealRepairCheck;
            sealSuccessFrames = art?.SealSuccessFrames;
            worldRoot = Rect("BuildingGuideWorld", parent.transform);
            worldRoot.anchorMin = worldRoot.anchorMax = worldRoot.pivot = Vector2.one * .5f;
            worldRoot.sizeDelta = Vector2.zero;
            worldRoot.SetAsFirstSibling();
            miningWarning = Border("SealBoundaryMiningWarning", new Color(1f, .35f, .1f, 1f));
            recoveryPanel = Rect("BaseRecoveryChecklist", parent.transform);
            recoveryPanel.anchorMin = recoveryPanel.anchorMax = recoveryPanel.pivot = Vector2.one;
            recoveryPanel.anchoredPosition = new Vector2(-12, -90);
            recoveryPanel.sizeDelta = new Vector2(230, 58);
            var recoveryBackground = recoveryPanel.gameObject.AddComponent<UnityEngine.UI.Image>();
            recoveryBackground.color = new Color(.05f, .12f, .16f, .95f);
            recoveryBackground.raycastTarget = false;
            recoveryText = Instantiate(template, recoveryPanel);
            recoveryText.gameObject.SetActive(true); recoveryText.enabled = true; recoveryText.raycastTarget = false;
            recoveryText.fontSize = 8; recoveryText.resizeTextForBestFit = false;
            recoveryText.alignment = TextAnchor.UpperLeft; recoveryText.color = Color.white;
            recoveryText.rectTransform.anchorMin = recoveryText.rectTransform.anchorMax =
                recoveryText.rectTransform.pivot = new Vector2(0, 1);
            recoveryText.rectTransform.anchoredPosition = new Vector2(6, -5);
            recoveryText.rectTransform.sizeDelta = new Vector2(218, 48);
            baseArrow = Rect("BaseDirection", parent.transform);
            baseArrow.sizeDelta = new Vector2(16, 16);
            baseArrowImage = baseArrow.gameObject.AddComponent<UnityEngine.UI.Image>();
            baseArrowImage.sprite = art?.GoalDirectionArrow; baseArrowImage.color = Color.white;
            baseArrowImage.raycastTarget = false;
            var baseLabel = Instantiate(template, baseArrow);
            baseLabel.gameObject.SetActive(true); baseLabel.enabled = true; baseLabel.raycastTarget = false;
            baseLabel.fontSize = 7; baseLabel.resizeTextForBestFit = false; baseLabel.text = "거점";
            baseLabel.alignment = TextAnchor.MiddleCenter; baseLabel.color = Color.white;
            baseLabel.rectTransform.anchorMin = baseLabel.rectTransform.anchorMax =
                baseLabel.rectTransform.pivot = Vector2.one * .5f;
            baseLabel.rectTransform.localScale = Vector3.one;
            baseLabel.rectTransform.sizeDelta = new Vector2(30, 14);
            baseLabel.rectTransform.anchoredPosition = new Vector2(0, -15);
            recoveryPanel.gameObject.SetActive(false); baseArrow.gameObject.SetActive(false);
            sealSuccess = ArtMarker("SealSuccessWave", null);
            toggle = Rect("ShelterExampleToggle", parent.transform);
            toggle.anchorMin = toggle.anchorMax = toggle.pivot = Vector2.one;
            toggle.anchoredPosition = new Vector2(-12, -62); toggle.sizeDelta = new Vector2(150, 20);
            var bg = toggle.gameObject.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(.05f, .12f, .16f, .9f); bg.raycastTarget = true;
            var button = toggle.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = bg;
            button.onClick.AddListener(() => outlineEnabled = !outlineEnabled);
            toggleIcon = Rect("Enabled", toggle).gameObject.AddComponent<UnityEngine.UI.Image>();
            toggleIcon.rectTransform.anchoredPosition = new Vector2(-61, 0);
            toggleIcon.rectTransform.sizeDelta = new Vector2(12, 12); toggleIcon.raycastTarget = false;
            var label = Instantiate(template, toggle); label.gameObject.SetActive(true); label.enabled = true;
            label.name = "ExampleSize"; label.raycastTarget = false; label.color = Color.white;
            label.fontSize = 7; label.resizeTextForBestFit = false; label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = Vector2.one * .5f;
            label.rectTransform.anchoredPosition = new Vector2(8, 0); label.rectTransform.sizeDelta = new Vector2(120, 18);
            label.rectTransform.localScale = Vector3.one;
            var size = services.BuildingGuide.OutlineInnerSize;
            // 조작 버튼명은 S2 명세의 용어다. 행동 안내 문장은 Message()로 CSV에서만 읽는다.
            label.text = "예시 윤곽 · " + size.x + "×" + size.y;
            // 코어를 내부 왼쪽 두 번째 칸에 놓은 예시. 바닥은 코어 한 칸 아래이며 문은 오른쪽 1×2.
            for (var x = -2; x <= size.x - 1; x++)
                for (var y = -1; y <= size.y; y++)
                {
                    if (x != -2 && x != size.x - 1 && y != -1 && y != size.y) continue;
                    if (x == size.x - 1 && (y == 0 || y == 1)) continue;
                    outlineOffsets.Add(new Vector3Int(x, y, 0));
                    outlines.Add(Border("ExampleBoundary", new Color(.55f, .8f, 1f, .3f)));
                }
            // 문 두 칸은 같은 외곽선으로 그려 한 문임을 표시한다.
            outlineOffsets.Add(new Vector3Int(size.x - 1, 0, 0));
            outlines.Add(Border("ExampleDoor", new Color(.8f, .9f, 1f, .5f)));
            for (var i = 0; i < 3; i++) holes.Add(ArtMarker("MissingBoundary", null));
            services.BuildingGuide.Repaired += HandleRepaired;
            services.BuildingGuide.SealedNow += HandleSealed;
            worldRoot.gameObject.SetActive(false); toggle.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (worldRoot == null || services?.BuildingGuide == null || services.Goals == null) return;
            // 목표 UI가 나중에 생성되며 첫 번째 자식으로 이동하므로 생성 시 순서만으로는 부족하다.
            // 건축 월드 표식만 HUD 맨 아래에 유지해 안내·목표 목록이 항상 위에 그려지게 한다.
            if (worldRoot.GetSiblingIndex() != 0) worldRoot.SetAsFirstSibling();
            shell ??= FindAnyObjectByType<MainGameShellUiController>();
            palette ??= FindAnyObjectByType<MainGameTilePaletteController>();
            placement ??= FindAnyObjectByType<MainGameTurretRuntime>();
            if (restoreVersion != services.Goals.RestoreVersion)
            {
                restoreVersion = services.Goals.RestoreVersion;
                ClearRepairs(); successUntil = nextEvaluation = 0;
                ClearSuccessAnimation();
                g05Complete = services.Goals.Progress.IsComplete("g05"); outlineEnabled = !g05Complete;
            }
            var visible = shell != null && shell.IsInitialized && !services.Goals.IsSuspended &&
                !SceneTransitionRequest.IsTransitionActive && !SceneTransitionRequest.IsLoadingSceneLoaded() &&
                !MainGameCraftingUiController.BlocksGameplayInput && Time.timeScale > 0 && player != null && !player.IsDead;
            var gameplayVisible = visible;
            visible = visible && GetComponent<MainGameGoalUiController>()?.SuppressesBuildingGuides != true;
            worldRoot.gameObject.SetActive(visible);
            CurrentMessage = string.Empty;
            if (gameplayVisible && Time.unscaledTime >= nextEvaluation)
            {
                services.BuildingGuide.Evaluate(player.transform.position);
                nextEvaluation = Time.unscaledTime + .2f;
            }
            if (gameplayVisible) services.BuildingGuide.RefreshMiningWarning(player.transform.position);
            RefreshMaintenanceUi(gameplayVisible, visible);
            if (!visible)
            {
                services.BuildingGuide.SetPlacement(null, default); toggle.gameObject.SetActive(false);
                ClearSuccessAnimation();
                return;
            }
            var guide = services.BuildingGuide;
            RefreshSuccessAnimation();
            if (!g05Complete && services.Goals.Progress.IsComplete("g05")) { g05Complete = true; outlineEnabled = false; }
            toggle.gameObject.SetActive(guide.Core.HasValue);
            toggleIcon.sprite = outlineEnabled ? checkOn : checkOff;
            toggleIcon.enabled = toggleIcon.sprite != null;
            for (var i = 0; i < outlines.Count; i++)
                Position(outlines[i], guide.Core.GetValueOrDefault() + outlineOffsets[i],
                    i == outlines.Count - 1 ? 2 : 1, guide.Core.HasValue && outlineEnabled);
            for (var i = 0; i < holes.Count; i++)
            {
                var frame = leakFrames != null && leakFrames.Count > 0
                    ? leakFrames[Mathf.FloorToInt(Time.unscaledTime / .1f) % leakFrames.Count] : null;
                holes[i].GetComponent<UnityEngine.UI.Image>().sprite = frame;
                Position(holes[i], i < guide.MissingCells.Count ? guide.MissingCells[i] : default, 1,
                    frame != null && guide.Core.HasValue && i < guide.MissingCells.Count && !guide.IsSealed,
                    i < guide.MissingCells.Count
                        ? MainGameBuildingGuide.GetLeakVisualYOffset(bootstrap.SealSystem,
                            bootstrap.TileService, guide.MissingCells[i]) : 0f);
            }
            while (doors.Count < guide.OpenDoors.Count) doors.Add(Border("OpenDoor", Color.yellow));
            for (var i = 0; i < doors.Count; i++)
            {
                Position(doors[i], i < guide.OpenDoors.Count ? guide.OpenDoors[i] : default, 2, i < guide.OpenDoors.Count);
                // 초당 한 번 선명한 점멸. 어두운 구간도 약하게 남겨 문 위치를 놓치지 않게 한다.
                var bright = Time.unscaledTime % 1f < .6f;
                var color = new Color(1f, .9f, .15f, bright ? 1f : .12f);
                foreach (var image in doors[i].GetComponentsInChildren<UnityEngine.UI.Image>())
                {
                    image.color = color;
                    // 1×2 셀로 확대되는 선: 가로선은 높이 배율을 보정해 세로선과 두께를 맞춘다.
                    var rect = image.rectTransform;
                    var size = rect.sizeDelta;
                    size.y = Mathf.Abs(Mathf.Sin(rect.localEulerAngles.z * Mathf.Deg2Rad)) < .01f ? .04f : .08f;
                    rect.sizeDelta = size;
                }
            }
            for (var i = repairs.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime >= repairs[i].Until) { Destroy(repairs[i].Rect.gameObject); repairs.RemoveAt(i); }
                else Position(repairs[i].Rect, repairs[i].Cell, 1, true, repairs[i].VisualYOffset);
            }
            var block = palette?.ForegroundPlacementItemId ?? placement?.PlacementDefinitionId;
            var cell = palette?.IsForegroundPlacementActive == true ? palette.ForegroundPlacementCell : placement?.PlacementCell ?? default;
            guide.SetPlacement(block, cell);
            // 안내 문구는 정본의 돌/흙 사례에만 적용. 설치 가능 여부와 밀폐 인정 여부는 별개다.
            if (guide.MiningSealBoundary) CurrentMessage = Message("seal_boundary_dig");
            else if (guide.HasNonSealTerrainWarning(cell, block))
                CurrentMessage = Message("placed_block_not_seal").Replace("{block}", catalog.ItemDisplayName(block, "블록"));
            else if (Time.unscaledTime < successUntil) CurrentMessage = Message("seal_success");
            else if (guide.OpenDoors.Count > 0) CurrentMessage = Message("door_open");
            else if (guide.Core.HasValue && !guide.IsSealed) CurrentMessage = Message("core_unsealed");
        }

        private void RefreshMaintenanceUi(bool gameplayVisible, bool guideVisible)
        {
            var guide = services.BuildingGuide;
            Position(miningWarning, guide.MiningCell, 1, guideVisible && guide.MiningSealBoundary);
            recoveryPanel.gameObject.SetActive(guideVisible && guide.ShowRecoveryChecklist);
            if (recoveryPanel.gameObject.activeSelf)
            {
                recoveryText.text = (guide.HeatRemaining <= .0001f ? "✓ 열기 제거 완료" :
                    $"□ 열기 제거 · {guide.HeatRemaining:0.#}℃ 남음") + "\n" +
                    (guide.DamagedCells.Count == 0 ? "✓ 문·벽 파괴 칸 수리 완료" :
                    $"□ 문·벽 수리 · {guide.DamagedCells.Count}칸 남음") + "\n" +
                    (guide.StorageChecked == 0 ? "□ 보관 조건 · 보관할 물품을 넣으세요" :
                    $"{(guide.StorageReady ? "✓" : "□")} 보관 조건 · {guide.StorageMet}/{guide.StorageChecked}곳 충족");
            }
            baseArrow.gameObject.SetActive(false);
            if (!gameplayVisible || !guide.Core.HasValue || worldCamera == null || baseArrowImage.sprite == null) return;
            var world = bootstrap.TileService.GetCellWorldBounds(guide.Core.Value).center;
            var viewport = worldCamera.WorldToViewportPoint(world);
            if (viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1) return;
            var canvasRect = (RectTransform)canvas.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                worldCamera.WorldToScreenPoint(world), EventCamera, out var target);
            var delta = target - canvasRect.rect.center;
            if (viewport.z < 0) delta = -delta;
            var bounds = canvasRect.rect.size * .5f - Vector2.one * 28f;
            var factor = Mathf.Min(bounds.x / Mathf.Max(.0001f, Mathf.Abs(delta.x)),
                bounds.y / Mathf.Max(.0001f, Mathf.Abs(delta.y)));
            baseArrow.anchoredPosition = delta * factor;
            // Rotate only the icon, keeping the name readable.
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            baseArrow.localRotation = Quaternion.Euler(0, 0, angle);
            baseArrow.GetComponentInChildren<UnityEngine.UI.Text>().rectTransform.localRotation =
                Quaternion.Euler(0, 0, -angle);
            baseArrow.gameObject.SetActive(true);
        }

        private string Message(string id) => catalog.FindGuideMessage(id)?.Text ?? string.Empty;
        private void HandleSealed()
        {
            successUntil = Time.unscaledTime + 2f;
            ClearSuccessAnimation();
            if (sealSuccessFrames == null || sealSuccessFrames.Count == 0) return;
            successCore = services.BuildingGuide.Core;
            successAnimationStarted = Time.unscaledTime;
        }

        private void RefreshSuccessAnimation()
        {
            if (!successCore.HasValue) return;
            var index = Mathf.FloorToInt((Time.unscaledTime - successAnimationStarted) / SealSuccessFrameSeconds);
            if (services.BuildingGuide.Core != successCore || !services.BuildingGuide.IsSealed ||
                index < 0 || index >= sealSuccessFrames.Count)
            { ClearSuccessAnimation(); return; }
            var sprite = sealSuccessFrames[index];
            if (sprite == null) { sealSuccess.gameObject.SetActive(false); return; }
            sealSuccess.GetComponent<UnityEngine.UI.Image>().sprite = sprite;
            var tiles = bootstrap.TileService;
            if (tiles == null) { sealSuccess.gameObject.SetActive(false); return; }
            var coreBounds = tiles.GetCellWorldBounds(successCore.Value);
            var bottomToCenter = (sprite.rect.height * .5f - SealSuccessBottomPaddingPixels) /
                sprite.pixelsPerUnit * SealSuccessVisualScale * coreBounds.size.y;
            Position(sealSuccess, successCore.Value, 1, true, -coreBounds.size.y * .5f + bottomToCenter);
            // 투명 여백을 제외한 이펙트의 최하단을 코어 바닥에 맞춘다. 프레임별 위치는 고정한다.
            var size = sealSuccess.sizeDelta;
            sealSuccess.sizeDelta = new Vector2(size.x * sprite.rect.width / sprite.pixelsPerUnit,
                size.y * sprite.rect.height / sprite.pixelsPerUnit) * SealSuccessVisualScale;
        }

        private void ClearSuccessAnimation()
        {
            successCore = null;
            if (sealSuccess != null) sealSuccess.gameObject.SetActive(false);
        }
        private void HandleRepaired(Vector3Int cell, float visualYOffset)
        {
            if (repairSprite == null) return; // 전용 미제공 아트를 도형이나 다른 그림으로 대체하지 않는다.
            var rect = ArtMarker("RepairedBoundary", repairSprite);
            repairs.Add(new RepairView
            { Cell = cell, Rect = rect, Until = Time.unscaledTime + .9f, VisualYOffset = visualYOffset });
        }
        private RectTransform ArtMarker(string name, Sprite sprite)
        {
            var rect = Rect(name, worldRoot);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite; image.raycastTarget = false; image.preserveAspect = true;
            rect.gameObject.SetActive(false);
            return rect;
        }
        private RectTransform Border(string name, Color color)
        {
            var rect = Rect(name, worldRoot);
            Stroke(rect, new Vector2(-.5f, -.5f), new Vector2(.5f, -.5f), color);
            Stroke(rect, new Vector2(.5f, -.5f), new Vector2(.5f, .5f), color);
            Stroke(rect, new Vector2(.5f, .5f), new Vector2(-.5f, .5f), color);
            Stroke(rect, new Vector2(-.5f, .5f), new Vector2(-.5f, -.5f), color);
            return rect;
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            return rect;
        }
        private static void Stroke(RectTransform parent, Vector2 start, Vector2 end, Color color)
        {
            var rect = Rect("Stroke", parent);
            var delta = end - start;
            rect.anchoredPosition = (start + end) * .5f; rect.sizeDelta = new Vector2(delta.magnitude, .04f);
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false;
        }
        private void Position(RectTransform rect, Vector3Int cell, int height, bool show,
            float worldYOffset = 0f)
        {
            var tiles = bootstrap.TileService;
            if (!show || tiles == null || !tiles.InBounds(cell) || worldCamera == null) { rect.gameObject.SetActive(false); return; }
            var bounds = tiles.GetCellWorldBounds(cell);
            bounds.center += Vector3.up * worldYOffset;
            var lower = worldCamera.WorldToScreenPoint(bounds.min);
            var upper = worldCamera.WorldToScreenPoint(bounds.max + Vector3.up * bounds.size.y * (height - 1));
            if (upper.z <= 0 || upper.x < 0 || lower.x > Screen.width || upper.y < 0 || lower.y > Screen.height)
            { rect.gameObject.SetActive(false); return; }
            lower.x = Mathf.Round(lower.x); lower.y = Mathf.Round(lower.y);
            upper.x = Mathf.Round(upper.x); upper.y = Mathf.Round(upper.y);
            var canvasRect = (RectTransform)canvas.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, lower, EventCamera, out var a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, upper, EventCamera, out var b);
            rect.anchoredPosition = (a + b) * .5f - canvasRect.rect.center;
            if (rect.TryGetComponent<UnityEngine.UI.Image>(out _))
            { rect.sizeDelta = b - a; rect.localScale = Vector3.one; }
            else
            {
                // 예시 윤곽·문 테두리는 단위 선을 실제 셀 크기로 확대한다.
                rect.sizeDelta = Vector2.zero; rect.localScale = new Vector3(b.x - a.x, b.y - a.y, 1);
            }
            rect.gameObject.SetActive(true);
        }
        private void ClearRepairs() { foreach (var view in repairs) Destroy(view.Rect.gameObject); repairs.Clear(); }
        private void OnDestroy()
        {
            if (services?.BuildingGuide != null)
            { services.BuildingGuide.Repaired -= HandleRepaired; services.BuildingGuide.SealedNow -= HandleSealed; }
            if (worldRoot != null) Destroy(worldRoot.gameObject);
            if (toggle != null) Destroy(toggle.gameObject);
            if (recoveryPanel != null) Destroy(recoveryPanel.gameObject);
            if (baseArrow != null) Destroy(baseArrow.gameObject);
        }
    }
}
