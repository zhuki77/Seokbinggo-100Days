using System.Text;
using Nyangbingo.World;
using UnityEngine;
using UnityEngine.UI;

namespace Nyangbingo.UI
{
    [DefaultExecutionOrder(-50)]
    public sealed class MainGameTurretBuildUiController : MonoBehaviour
    {
        [SerializeField] private MainGameTurretRuntime turretRuntime;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text detailsText;
        [SerializeField] private Button openButton;
        [SerializeField] private Button craftButton;
        [SerializeField] private Button previewButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        private bool initialized;

        public bool HasSceneBindings => turretRuntime != null && panel != null && detailsText != null &&
                                        openButton != null && craftButton != null && previewButton != null &&
                                        confirmButton != null && cancelButton != null;

        public void ConfigureForScene(MainGameTurretRuntime runtime, GameObject buildPanel, Text details,
            Button open, Button craft, Button preview, Button confirm, Button cancel)
        {
            turretRuntime = runtime;
            panel = buildPanel;
            detailsText = details;
            openButton = open;
            craftButton = craft;
            previewButton = preview;
            confirmButton = confirm;
            cancelButton = cancel;
        }

        private void Start()
        {
            if (!HasSceneBindings)
            {
                Debug.LogError("[Nyangbingo] MainGameTurretBuildUiController: scene bindings missing.");
                enabled = false;
                return;
            }
            openButton.onClick.AddListener(TogglePanel);
            craftButton.onClick.AddListener(() => turretRuntime.TryStartCraftingFromUi());
            previewButton.onClick.AddListener(() => turretRuntime.BeginPlacementPreview());
            confirmButton.onClick.AddListener(() => turretRuntime.ConfirmPlacementPreview());
            cancelButton.onClick.AddListener(HandleCancel);
            turretRuntime.BuildStateChanged += Refresh;
            panel.SetActive(false);
            initialized = true;
            Refresh();
        }

        private void Update()
        {
            if (!initialized || Time.timeScale <= 0f) return;
            if (Input.GetKeyDown(KeyCode.V)) TogglePanel();
            if (panel.activeSelf) Refresh();
        }

        private void TogglePanel()
        {
            if (!initialized) return;
            if (panel.activeSelf)
            {
                turretRuntime.CancelPlacementPreview();
                panel.SetActive(false);
            }
            else panel.SetActive(true);
            Refresh();
        }

        private void HandleCancel()
        {
            if (turretRuntime.IsPlacementPreviewActive) turretRuntime.CancelPlacementPreview();
            else panel.SetActive(false);
            Refresh();
        }

        private void Refresh()
        {
            if (!HasSceneBindings) return;
            var recipe = turretRuntime.TurretRecipe;
            var text = new StringBuilder();
            text.AppendLine("도깨비불 등탑  ·  MVP A");
            text.AppendLine("DPS 4 · 유도탄 · 사거리 8타일");
            text.AppendLine($"보유 {turretRuntime.TurretItemCount}개 · 석탄 {turretRuntime.CoalCount}개");
            if (recipe?.Ingredients != null)
            {
                text.Append("재료  ");
                for (var index = 0; index < recipe.Ingredients.Length; index++)
                {
                    var ingredient = recipe.Ingredients[index];
                    if (index > 0) text.Append(" · ");
                    text.Append(ingredient.item != null ? ingredient.item.DisplayName : "?");
                    text.Append(' ');
                    text.Append(ingredient.item != null ? turretRuntime.GetInventoryCount(ingredient.item.Id) : 0);
                    text.Append('/');
                    text.Append(ingredient.amount);
                }
                text.AppendLine();
            }
            text.AppendLine("작업대 제작 60초 · 연료 석탄 1개=270초");
            if (turretRuntime.IsPlacementPreviewActive)
                text.Append(turretRuntime.IsPlacementPreviewValid
                    ? "미리보기: 설치 가능 (초록색)"
                    : "미리보기: 설치 불가 (붉은색)");
            detailsText.text = text.ToString();

            craftButton.interactable = recipe != null && !turretRuntime.IsCrafting;
            previewButton.interactable = turretRuntime.TurretItemCount > 0 &&
                                         !turretRuntime.IsPlacementPreviewActive;
            confirmButton.gameObject.SetActive(turretRuntime.IsPlacementPreviewActive);
            confirmButton.interactable = turretRuntime.IsPlacementPreviewValid;
        }

        private void OnDestroy()
        {
            if (turretRuntime != null) turretRuntime.BuildStateChanged -= Refresh;
            if (openButton != null) openButton.onClick.RemoveAllListeners();
            if (craftButton != null) craftButton.onClick.RemoveAllListeners();
            if (previewButton != null) previewButton.onClick.RemoveAllListeners();
            if (confirmButton != null) confirmButton.onClick.RemoveAllListeners();
            if (cancelButton != null) cancelButton.onClick.RemoveAllListeners();
        }
    }
}
