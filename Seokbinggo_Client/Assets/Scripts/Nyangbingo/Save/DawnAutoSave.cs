using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Save
{
    public sealed class DawnAutoSave : MonoBehaviour
    {
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private MonoBehaviour timeSourceComponent;
        [SerializeField] private MonoBehaviour snapshotProviderComponent;
        [Range(0, SaveManager.SlotCount - 1)][SerializeField] private int slot;
        private ITimeSource timeSource;
        private ISaveSnapshotProvider snapshotProvider;

        private void Awake()
        {
            timeSource = timeSourceComponent as ITimeSource;
            snapshotProvider = snapshotProviderComponent as ISaveSnapshotProvider;
        }

        private void OnEnable() { if (timeSource != null) timeSource.Dawn += SaveAtDawn; }
        private void OnDisable() { if (timeSource != null) timeSource.Dawn -= SaveAtDawn; }

        private void SaveAtDawn()
        {
            if (saveManager == null || snapshotProvider == null) return;
            var snapshot = snapshotProvider.CaptureSnapshot();
            if (snapshot != null) saveManager.SaveAtDawn(slot, snapshot);
        }

        public void Configure(SaveManager manager, ITimeSource source, ISaveSnapshotProvider provider, int saveSlot)
        {
            if (timeSource != null) timeSource.Dawn -= SaveAtDawn;
            saveManager = manager;
            timeSource = source;
            snapshotProvider = provider;
            slot = Mathf.Clamp(saveSlot, 0, SaveManager.SlotCount - 1);
            if (isActiveAndEnabled && timeSource != null) timeSource.Dawn += SaveAtDawn;
        }
    }
}
