using Nyangbingo.Data;
using System.Text;
using Nyangbingo.World;
using Nyangbingo.Inventory;
using System.Collections.Generic;
using UnityEngine;
using Input = Nyangbingo.Core.GameplayInput;

namespace Nyangbingo.UI
{
    /// <summary>v86 S1/S3/S4. 목표·새벽 결과와 CSV 기반 주 안내 우선순위를 표시한다.</summary>
    public sealed class MainGameGoalUiController : MonoBehaviour
    {
        private MainGameRuntimeServices services;
        private GameDataCatalog catalog;
        private ItemArtCatalog itemArt;
        private GameplayArtCatalog gameplayArt;
        private MainGamePlayerController player;
        private MainGameShellUiController shell;
        private Canvas canvas;
        private RectTransform root, list, dawnRoot;
        private UnityEngine.UI.Text title, hint, completion, dawnText, dawnPageText;
        private UnityEngine.UI.Image icon, arrow;
        private UnityEngine.UI.Text arrowLabel;
        private UnityEngine.UI.Button titleButton;
        private UnityEngine.UI.Button dawnPrevious, dawnNext, dawnConfirm;
        private UnityEngine.UI.Button[] options;
        private bool listOpen;
        private bool firstShelterCompleteShown;
        private float nextEvaluation, nextGuideEvaluation, completionUntil;
        private int dawnDayShown, dawnShownFrame, dawnPage;
        private int restoreVersion = -1;
        private Sprite directionSprite;
        private MainGameBootstrap bootstrap;
        private MainGameEncounterCoordinator encounters;
        private GuidePriorityQueue guides;
        public bool SuppressesBuildingGuides => guides?.Select(false)?.Definition.Tier <= 2;
        public bool IsPointerOverInteraction => canvas != null &&
            ((dawnRoot != null && dawnRoot.gameObject.activeInHierarchy &&
              RectTransformUtility.RectangleContainsScreenPoint(dawnRoot, Input.mousePosition,
                  canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera)) ||
             (root != null && root.gameObject.activeInHierarchy &&
            (RectTransformUtility.RectangleContainsScreenPoint(title.rectTransform, Input.mousePosition,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera) ||
             (list.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(list, Input.mousePosition,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera)))));

        public void Configure(Canvas parent, UnityEngine.UI.Text textTemplate, GameDataCatalog data,
            MainGameRuntimeServices runtime, MainGamePlayerController controller,
            ItemArtCatalog icons, GameplayArtCatalog art, Sprite goalDirectionSprite = null)
        {
            if (root != null || parent == null || textTemplate == null || runtime.Goals == null) return;
            canvas = parent; services = runtime; catalog = data; player = controller;
            bootstrap = FindAnyObjectByType<MainGameBootstrap>();
            encounters = FindAnyObjectByType<MainGameEncounterCoordinator>();
            guides = services.Goals.GuideQueue;
            guides.BeginDay(bootstrap?.TimeService?.Day ?? 1);
            itemArt = icons; gameplayArt = art; directionSprite = goalDirectionSprite;
            root = Panel("CurrentGoal", parent.transform, new Vector2(12, -62), new Vector2(220, 57));
            icon = Image("Icon", root, new Vector2(5, -5), new Vector2(18, 18));
            icon.preserveAspect = true;
            title = Label("GoalTitle", root, textTemplate, new Vector2(28, -3), new Vector2(187, 16), 8);
            title.raycastTarget = true;
            titleButton = title.gameObject.AddComponent<UnityEngine.UI.Button>();
            titleButton.targetGraphic = title;
            var titleColors = titleButton.colors;
            titleColors.disabledColor = Color.white;
            titleButton.colors = titleColors;
            titleButton.onClick.AddListener(() => listOpen = !listOpen);
            hint = Label("GoalHint", root, textTemplate, new Vector2(5, -23), new Vector2(210, 30), 7);
            completion = Label("GoalCompleted", root, textTemplate, new Vector2(5, -57), new Vector2(210, 14), 7);
            completion.color = new Color(.55f, 1f, .7f);
            list = Panel("GoalSelection", root, new Vector2(0, -74), new Vector2(220, 108));
            var goals = services.Goals.Progress.SelectableGoals;
            options = new UnityEngine.UI.Button[goals.Count];
            for (var i = 0; i < goals.Count; i++)
            {
                var goal = goals[i];
                var row = Panel(goal.Id, list, new Vector2(3, -3 - i * 17), new Vector2(214, 16));
                options[i] = row.gameObject.AddComponent<UnityEngine.UI.Button>();
                options[i].targetGraphic = row.GetComponent<UnityEngine.UI.Image>();
                options[i].targetGraphic.raycastTarget = true;
                Label("Name", row, textTemplate, new Vector2(4, 0), new Vector2(206, 16), 7).text = goal.DisplayName;
                options[i].onClick.AddListener(() => { services.Goals.Progress.Select(goal.Id); listOpen = false; });
            }
            dawnRoot = Panel("DawnPreservationConfirmation", parent.transform, Vector2.zero, new Vector2(260, 168));
            dawnRoot.anchorMin = dawnRoot.anchorMax = dawnRoot.pivot = Vector2.one * .5f;
            dawnText = Label("Result", dawnRoot, textTemplate, new Vector2(8, -7), new Vector2(244, 125), 8);
            dawnPrevious = Control("Previous", "◀", dawnRoot, textTemplate, new Vector2(8, -141), new Vector2(28, 20),
                () => { dawnPage--; RefreshDawn(); });
            dawnNext = Control("Next", "▶", dawnRoot, textTemplate, new Vector2(76, -141), new Vector2(28, 20),
                () => { dawnPage++; RefreshDawn(); });
            dawnPageText = Label("Page", dawnRoot, textTemplate, new Vector2(38, -145), new Vector2(36, 16), 8);
            dawnConfirm = Control("Confirm", "확인", dawnRoot, textTemplate, new Vector2(192, -141), new Vector2(60, 20), () =>
            {
                if (!dawnRoot.gameObject.activeInHierarchy || Time.frameCount <= dawnShownFrame || dawnShownFrame == 0) return;
                services.Goals.AcknowledgeDawn(dawnDayShown);
                RefreshDawn();
            });
            dawnRoot.gameObject.SetActive(false);
            arrow = Image("GoalDirection", parent.transform, Vector2.zero, new Vector2(16, 16));
            arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = arrow.rectTransform.pivot = Vector2.one * .5f;
            arrow.sprite = directionSprite;
            arrowLabel = Label("GoalDirectionLabel", arrow.transform, textTemplate,
                new Vector2(0, -15), new Vector2(30, 14), 7);
            arrowLabel.text = "목표";
            arrowLabel.alignment = TextAnchor.MiddleCenter;
            arrowLabel.rectTransform.anchorMin = arrowLabel.rectTransform.anchorMax =
                arrowLabel.rectTransform.pivot = Vector2.one * .5f;
            arrow.gameObject.SetActive(false);
            root.SetAsFirstSibling(); arrow.rectTransform.SetAsFirstSibling();
            root.gameObject.SetActive(false);
            services.Goals.Progress.Completed += HandleCompleted;
            services.StorageTemperature.ConditionMet += HandleStorageConditionMet;
            services.BuildingGuide.SealedNow += HandleGuideSealed;
            services.BuildingGuide.Fired += HandleMaintenanceEvent;
            services.Bed.SleepDeniedCold += HandleColdBed;
            services.Bed.Slept += HandleGuideSlept;
            services.StorageTemperature.DailyProcessed += HandleGuideDawn;
        }

        private void LateUpdate()
        {
            if (root == null || services?.Goals == null) return;
            if (restoreVersion != services.Goals.RestoreVersion)
            {
                restoreVersion = services.Goals.RestoreVersion;
                dawnDayShown = dawnShownFrame = 0;
                completionUntil = nextEvaluation = nextGuideEvaluation = 0f;
                listOpen = firstShelterCompleteShown = false;
                // Restore owns the queue's persisted once-per-day history.
                guides.BeginDay(bootstrap?.TimeService?.Day ?? 1);
            }
            shell ??= FindAnyObjectByType<MainGameShellUiController>();
            if (shell != null && shell.IsInitialized && !services.Goals.IsSuspended && Time.unscaledTime >= nextEvaluation)
            {
                services.StorageTemperature.RefreshConditions();
                services.Goals.Evaluate();
                nextEvaluation = Time.unscaledTime + .2f;
            }
            var ready = shell != null && shell.IsInitialized && !services.Goals.IsSuspended &&
                !SceneTransitionRequest.IsTransitionActive && !SceneTransitionRequest.IsLoadingSceneLoaded() &&
                player != null && !player.IsDead;
            var menuOpen = MainGameCraftingUiController.BlocksGameplayInput || Time.timeScale <= 0f;
            if (ready && Time.unscaledTime >= nextGuideEvaluation)
            {
                RefreshGuideStates();
                nextGuideEvaluation = Time.unscaledTime + .2f;
            }
            if (ready && !menuOpen) services.BuildingGuide.RefreshMiningWarning(player.transform.position);
            SetGuide("seal_boundary_dig", ready && !menuOpen && services.BuildingGuide.MiningSealBoundary,
                context: services.BuildingGuide.MiningCell.ToString());
            var visible = ready && !menuOpen;
            root.gameObject.SetActive(visible);
            if (!visible) { arrow.gameObject.SetActive(false); dawnRoot.gameObject.SetActive(false); dawnShownFrame = 0; return; }
            var progress = services.Goals.Progress;
            if (progress.FirstShelterComplete != firstShelterCompleteShown)
            {
                firstShelterCompleteShown = progress.FirstShelterComplete;
                listOpen = firstShelterCompleteShown;
            }
            var goal = progress.Current;
            title.text = goal?.DisplayName ?? string.Empty;
            if (!listOpen && services.Goals.PendingDawnDay == 0) services.Goals.RefreshRewardCraftingGuide();
            var selected = guides.Select(false);
            hint.text = selected?.Text ?? string.Empty;
            if (selected?.Definition.Id != "reward_crafting" || (!listOpen && services.Goals.PendingDawnDay == 0))
                guides.Advance(selected, Time.unscaledDeltaTime);
            if (selected?.Definition.Id == "reward_crafting" && selected.Remaining <= 0f)
                services.Goals.RefreshRewardCraftingGuide();
            icon.sprite = ResolveIcon(goal?.IconRef);
            icon.enabled = icon.sprite != null;
            if (services.Goals.PendingDawnDay > 0) listOpen = false;
            titleButton.interactable = progress.FirstShelterComplete && services.Goals.PendingDawnDay == 0;
            list.gameObject.SetActive(listOpen && progress.FirstShelterComplete);
            for (var i = 0; i < options.Length; i++)
            {
                var option = progress.SelectableGoals[i];
                options[i].interactable = !progress.IsComplete(option.Id);
                options[i].GetComponent<UnityEngine.UI.Image>().color = option.Id == goal?.Id
                    ? new Color(.13f, .30f, .40f, .98f) : new Color(.05f, .12f, .16f, .95f);
            }
            completion.gameObject.SetActive(Time.unscaledTime < completionUntil);
            if (selected?.Definition.Tier <= 2)
            {
                dawnRoot.gameObject.SetActive(false); dawnShownFrame = 0;
                list.gameObject.SetActive(false); completion.gameObject.SetActive(false);
            }
            else RefreshDawn();
            if (goal == null && string.IsNullOrEmpty(hint.text) &&
                !dawnRoot.gameObject.activeSelf && !completion.gameObject.activeSelf)
                root.gameObject.SetActive(false);
            RefreshArrow();
        }

        private void RefreshDawn()
        {
            var tracker = services.Goals;
            var pending = tracker.PendingDawnDay;
            if (pending > 0 && pending != dawnDayShown)
            {
                dawnDayShown = pending;
                dawnShownFrame = 0;
                dawnPage = 0;
            }
            var show = pending > 0;
            dawnRoot.gameObject.SetActive(show);
            if (!show) { dawnDayShown = dawnShownFrame = 0; return; }
            dawnRoot.SetAsLastSibling();
            var rows = tracker.PendingDawnContainers;
            // 원인과 다음 행동이 긴 문장이어도 확인 버튼을 덮지 않도록 용기 한 개씩 표시한다.
            var pages = Mathf.Max(1, rows.Count);
            dawnPage = Mathf.Clamp(dawnPage, 0, pages - 1);
            var text = new StringBuilder($"Day {pending}\n\n");
            if (rows.Count == 0)
                text.Append(Message("dawn_kept").Replace("{n}", tracker.PendingDawnKeptIce.ToString()));
            for (var i = dawnPage; i < Mathf.Min(rows.Count, dawnPage + 1); i++)
            {
                var row = rows[i];
                text.AppendLine($"{catalog.FindItem("jangdok")?.DisplayName ?? "장독"} {i + 1} · {row.iceBefore} → {row.iceAfter}");
                if (row.lostIce > 0)
                {
                    var causeId = row.cause == "unsealed" ? "dawn_cause_unsealed" :
                        row.cause == "outside_cold_area" ? "dawn_cause_outside" :
                        row.cause == "invasion_heat" ? "dawn_cause_heat" : null;
                    text.AppendLine(Message("dawn_lost").Replace("{lost}", row.lostIce.ToString())
                        .Replace("{cause}", causeId == null ? Below(row.temperature, row.requiredTemperature) : Message(causeId)));
                }
                else text.AppendLine(row.keptIce > 0
                    ? Message("dawn_kept").Replace("{n}", row.keptIce.ToString())
                    : Below(row.temperature, row.requiredTemperature));
                var comparison = row.temperature <= row.requiredTemperature ? "≤" : ">";
                text.AppendLine($"{row.temperature:0.#}°C {comparison} {row.requiredTemperature:0.#}°C\n");
            }
            dawnText.text = text.ToString().TrimEnd();
            dawnPageText.text = $"{dawnPage + 1}/{pages}";
            dawnPrevious.interactable = dawnPage > 0;
            dawnNext.interactable = dawnPage + 1 < pages;
            // A visible result is not an acknowledgement: only the confirm button records g09.
            if (dawnShownFrame == 0) dawnShownFrame = Time.frameCount;
            dawnConfirm.interactable = Time.frameCount > dawnShownFrame;
        }

        private string Message(string id) => catalog.FindGuideMessage(id)?.Text ?? string.Empty;
        private string Below(float temperature, float required) => Message("storage_below")
            .Replace("{temp}", temperature.ToString("0.#")).Replace("{req}", required.ToString("0.#"));
        private void HandleStorageConditionMet(StorageConditionState state)
        {
            if (state.HasIce) FireGuide("storage_ok", Message("storage_ok"));
        }

        private void HandleGuideSealed() => FireGuide("seal_success", Message("seal_success"));
        private void HandleMaintenanceEvent(string state)
        {
            if (state == "invasion_heat_cleared") FireGuide("heat_cleared", Message("heat_cleared"));
        }
        private void HandleColdBed(float required) => FireGuide("cold_bed",
            Message("cold_bed").Replace("{req}", required.ToString("0.#")));
        private void HandleGuideSlept(float _) => guides.Clear("cold_bed");
        private void HandleGuideDawn(int day, StorageDailyResult result)
        {
            if (services.Goals.IsSuspended) return;
            guides.BeginDay(day);
            var hasLoss = false;
            if (result.Containers != null)
                foreach (var row in result.Containers)
                {
                    if (row.lostIce <= 0) continue;
                    var cause = row.cause == "unsealed" ? Message("dawn_cause_unsealed") :
                        row.cause == "outside_cold_area" ? Message("dawn_cause_outside") :
                        row.cause == "invasion_heat" ? Message("dawn_cause_heat") : Below(row.temperature, row.requiredTemperature);
                    FireGuide("dawn_lost", Message("dawn_lost").Replace("{lost}", row.lostIce.ToString()).Replace("{cause}", cause));
                    hasLoss = true; break;
                }
            if (!hasLoss && result.KeptIceItems > 0)
                FireGuide("dawn_kept", Message("dawn_kept").Replace("{n}", result.KeptIceItems.ToString()));
        }

        private void FireGuide(string id, string text)
        {
            guides.BeginDay(bootstrap?.TimeService?.Day ?? 1);
            guides.Fire(catalog.FindGuideMessage(id), text);
        }

        private void SetGuide(string id, bool active, string text = null, string context = "") =>
            guides.SetState(catalog.FindGuideMessage(id), active, text ?? Message(id), context);

        private void RefreshGuideStates()
        {
            var time = bootstrap?.TimeService;
            if (time == null) return;
            guides.BeginDay(time.Day);
            SetGuide("hypo_danger", services.PlayerTemperature?.IsHypothermia == true);
            SetGuide("burn_danger", services.DayHeatDamage?.IsBurnActive == true);
            var boss = encounters?.BossManager?.ActiveDefinition;
            SetGuide("boss_active", boss != null,
                Message("boss_active").Replace("{boss}", boss?.DisplayName ?? string.Empty), boss?.Id ?? "");
            SetGuide("invasion_active", services.Invasion?.IsCurrentInvasionNight == true);
            SetGuide("invasion_tonight", InvasionScheduleRules.ShouldShowAnnouncement(time.Day, time.IsNight,
                InvasionScheduleRules.ReadAnnounceEnabled(catalog), InvasionScheduleRules.ReadPeriod(catalog),
                InvasionScheduleRules.ReadOffset(catalog)));
            var baekjungTonight = false;
            foreach (var dayEvent in catalog.DayEvents)
                if (dayEvent != null && dayEvent.Day == time.Day && !time.IsNight) baekjungTonight = true;
            SetGuide("baekjung_tonight", baekjungTonight);
            BossDefinition tonight = null;
            foreach (var definition in catalog.Bosses)
                if (definition != null && definition.ForcedDay == time.Day && !time.IsNight) { tonight = definition; break; }
            SetGuide("boss_tonight", tonight != null, Message("boss_tonight")
                .Replace("{boss}", tonight?.DisplayName ?? string.Empty)
                .Replace("{place}", tonight?.Id == "imugi_boss" ? catalog.FindItem("ice_lake")?.DisplayName ?? "얼음 호수" : "기지"));
            var building = services.BuildingGuide;
            SetGuide("base_damaged", building.IsActive("base_damaged"),
                Message("base_damaged").Replace("{n}", building.DamagedCells.Count.ToString()));
            SetGuide("core_unsealed", building.IsActive("core_unsealed"));
            SetGuide("door_open", building.IsActive("seal_blocked_by_open_door"));
            SetGuide("placed_block_not_seal", building.IsActive("placing_non_seal_block"),
                Message("placed_block_not_seal").Replace("{block}", catalog.ItemDisplayName(building.PlacementBlock, "블록")),
                building.PlacementBlock ?? "");
            var environment = services.GetComponent<MainGameEnvironmentState>();
            StorageConditionState? nearest = null;
            var distance = float.PositiveInfinity;
            if (environment != null)
                foreach (var record in environment.ExportPlacedObjects())
                {
                    if (record.definitionId != JangdokStorageRuntime.DefinitionId ||
                        !services.StorageTemperature.TryGetCondition(record.objectId, out var condition) ||
                        !condition.HasIce) continue;
                    var candidate = ((Vector2)record.position - (Vector2)player.transform.position).sqrMagnitude;
                    if (candidate < distance) { distance = candidate; nearest = condition; }
                }
            SetGuide("storage_below", nearest.HasValue && !nearest.Value.Met,
                nearest.HasValue ? Below(nearest.Value.Temperature, nearest.Value.RequiredTemperature) : "",
                nearest?.ObjectId ?? "");
            foreach (var definition in catalog.GuideMessages)
                if (definition != null && definition.TriggerState.StartsWith("goal:", System.StringComparison.Ordinal))
                    guides.SetState(definition, definition.Id == services.Goals.Progress.Current?.HintMessageId,
                        services.Goals.CurrentHint);
        }

        private void RefreshArrow()
        {
            var camera = Camera.main;
            // Missing direction art remains unbound; never borrow a different item's icon.
            if (directionSprite == null || camera == null || !root.gameObject.activeSelf || SuppressesBuildingGuides)
            { arrow.gameObject.SetActive(false); return; }
            var target = services.Goals.ResolveTarget(player.transform.position);
            // The independent S7 base marker already points at this core.
            if (target.HasValue && services.BuildingGuide.Core.HasValue &&
                bootstrap.TileService.WorldToCell(target.Value) == services.BuildingGuide.Core.Value)
            { arrow.gameObject.SetActive(false); return; }
            if (!target.HasValue) { arrow.gameObject.SetActive(false); return; }
            var viewport = camera.WorldToViewportPoint(target.Value);
            var outside = viewport.z <= 0 || viewport.x < .03f || viewport.x > .97f || viewport.y < .03f || viewport.y > .97f;
            arrow.gameObject.SetActive(outside);
            if (!outside) return;
            var canvasRect = (RectTransform)canvas.transform;
            // Match the base marker's coordinate space. Viewport deltas lose the screen
            // aspect ratio and cannot be intersected directly with Canvas-sized bounds.
            var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                camera.WorldToScreenPoint(target.Value), eventCamera, out var localTarget);
            var delta = localTarget - canvasRect.rect.center;
            if (viewport.z < 0) delta = -delta;
            var bounds = canvasRect.rect.size * .5f - Vector2.one * 28f;
            var factor = Mathf.Min(bounds.x / Mathf.Max(.0001f, Mathf.Abs(delta.x)),
                bounds.y / Mathf.Max(.0001f, Mathf.Abs(delta.y)));
            arrow.rectTransform.anchoredPosition = delta * factor;
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            var labelRotation = Quaternion.Euler(0, 0, -angle);
            arrowLabel.rectTransform.localRotation = labelRotation;
            // Keep the caption below the icon even when the arrow points up or down.
            arrowLabel.rectTransform.anchoredPosition = labelRotation * new Vector3(0, -15, 0);
        }

        private Sprite ResolveIcon(string id) => itemArt?.FindSprite(id) ?? (id switch
        {
            "king_dokkaebi" => gameplayArt?.BossHealthKingDokkaebi,
            "imugi_boss" => gameplayArt?.BossHealthImugi,
            _ => null
        });
        private void HandleCompleted(GoalDefinition goal)
        {
            completion.text = goal.DisplayName + " ✓";
            completionUntil = Time.unscaledTime + .8f;
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            go.GetComponent<UnityEngine.UI.Image>().color = new Color(.05f, .12f, .16f, .95f);
            go.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            return rect;
        }
        private static UnityEngine.UI.Button Control(string name, string text, Transform parent,
            UnityEngine.UI.Text template, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var rect = Panel(name, parent, position, size);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            button.targetGraphic.raycastTarget = true;
            var label = Label("Label", rect, template, new Vector2(2, -2), size - Vector2.one * 4, 8);
            label.alignment = TextAnchor.MiddleCenter; label.text = text;
            button.onClick.AddListener(action);
            return button;
        }
        private static UnityEngine.UI.Image Image(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = Panel(name, parent, position, size);
            var image = rect.GetComponent<UnityEngine.UI.Image>(); image.color = Color.white;
            return image;
        }
        private static UnityEngine.UI.Text Label(string name, Transform parent, UnityEngine.UI.Text template,
            Vector2 position, Vector2 size, int fontSize)
        {
            var label = Instantiate(template, parent); label.name = name;
            label.gameObject.SetActive(true); label.enabled = true; label.text = string.Empty;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
            label.raycastTarget = false; label.fontSize = fontSize; label.resizeTextForBestFit = false;
            label.alignment = TextAnchor.UpperLeft; label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private void OnDestroy()
        {
            if (services?.Goals != null) services.Goals.Progress.Completed -= HandleCompleted;
            if (services?.StorageTemperature != null) services.StorageTemperature.ConditionMet -= HandleStorageConditionMet;
            if (services?.BuildingGuide != null) services.BuildingGuide.SealedNow -= HandleGuideSealed;
            if (services?.BuildingGuide != null) services.BuildingGuide.Fired -= HandleMaintenanceEvent;
            if (services?.Bed != null) services.Bed.SleepDeniedCold -= HandleColdBed;
            if (services?.Bed != null) services.Bed.Slept -= HandleGuideSlept;
            if (services?.StorageTemperature != null) services.StorageTemperature.DailyProcessed -= HandleGuideDawn;
            if (root != null) Destroy(root.gameObject);
            if (dawnRoot != null) Destroy(dawnRoot.gameObject);
            if (arrow != null) Destroy(arrow.gameObject);
        }
    }
}
