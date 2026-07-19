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
            // GDD 5 UI/UX: 제작·건설은 숫자 2 통합 패널에서 처리한다.
            // 구형 V 전용 패널은 씬 호환을 위해 참조만 유지하고 런타임에서는 노출하지 않는다.
            if (panel != null) panel.SetActive(false);
            if (openButton != null) openButton.gameObject.SetActive(false);
            enabled = false;
            Debug.Log("[Nyangbingo] Legacy V turret panel disabled; building is integrated into tab 2.");
        }

    }
}
