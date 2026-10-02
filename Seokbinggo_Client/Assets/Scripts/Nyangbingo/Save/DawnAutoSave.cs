using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Save
{
    // CentralTickDriver (100) applies elapsed game time before this snapshot is captured.
    [DefaultExecutionOrder(200)]
    public sealed class DawnAutoSave : MonoBehaviour
    {
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private MonoBehaviour timeSourceComponent;
        [SerializeField] private MonoBehaviour snapshotProviderComponent;
        [Range(0, SaveManager.SlotCount - 1)][SerializeField] private int slot;
        private ITimeSource timeSource;
        private ISaveSnapshotProvider snapshotProvider;
        private bool savePending;

        private void Awake()
        {
            timeSource = timeSourceComponent as ITimeSource;
            snapshotProvider = snapshotProviderComponent as ISaveSnapshotProvider;
        }

        private void OnEnable() { if (timeSource != null) timeSource.Dawn += SaveAtDawn; }
        private void OnDisable()
        {
            if (timeSource != null) timeSource.Dawn -= SaveAtDawn;
            savePending = false;
        }

        private void SaveAtDawn()
        {
            if (saveManager == null || snapshotProvider == null) return;
            if (snapshotProvider is MainGameSaveCoordinator coordinator && coordinator.IsRestoring) return;
            // Coalesce boundaries crossed in one frame into the final fully simulated state.
            savePending = true;
        }

        private void LateUpdate()
        {
            if (!savePending) return;
            savePending = false;
            if (saveManager == null || snapshotProvider == null) return;
            if (snapshotProvider is MainGameSaveCoordinator coordinator && coordinator.IsRestoring) return;
            var snapshot = snapshotProvider.CaptureSnapshot();
            if (snapshot != null) saveManager.SaveAtDawn(slot, snapshot);
        }

        public void Configure(SaveManager manager, ITimeSource source, ISaveSnapshotProvider provider, int saveSlot)
        {
            if (timeSource != null) timeSource.Dawn -= SaveAtDawn;
            savePending = false;
            saveManager = manager;
            timeSource = source;
            snapshotProvider = provider;
            slot = Mathf.Clamp(saveSlot, 0, SaveManager.SlotCount - 1);
            if (isActiveAndEnabled && timeSource != null) timeSource.Dawn += SaveAtDawn;
        }
    }
}
