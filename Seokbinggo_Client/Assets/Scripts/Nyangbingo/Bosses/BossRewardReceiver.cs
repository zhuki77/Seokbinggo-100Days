using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.World;
using Nyangbingo.Combat;
using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.Bosses
{
    public sealed class BossRewardReceiver : MonoBehaviour
    {
        [SerializeField] private BossManager bossManager;
        private bool subscribed;
        private Vector2 lastBossPosition;
        private Health trackedBossHealth;
        public event System.Action<ItemDefinition, int> RewardGranted;

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        public void ConfigureForRuntime(BossManager manager)
        {
            Unsubscribe();
            bossManager = manager;
            if (isActiveAndEnabled) Subscribe();
        }

        private void Subscribe()
        {
            if (subscribed || bossManager == null) return;
            bossManager.BossStarted += CaptureBossPosition;
            bossManager.BossEnded += GrantRewards;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (bossManager != null)
            {
                bossManager.BossStarted -= CaptureBossPosition;
                bossManager.BossEnded -= GrantRewards;
            }
            StopTrackingBossHealth();
            subscribed = false;
        }

        private void CaptureBossPosition(BossDefinition _)
        {
            StopTrackingBossHealth();
            trackedBossHealth = bossManager != null ? bossManager.ActiveHealth : null;
            lastBossPosition = trackedBossHealth != null ? trackedBossHealth.transform.position : transform.position;
            if (trackedBossHealth != null) trackedBossHealth.Damaged += CaptureLatestBossPosition;
        }

        private void CaptureLatestBossPosition(DamageTag _, int __)
        {
            if (trackedBossHealth != null) lastBossPosition = trackedBossHealth.transform.position;
        }

        private void StopTrackingBossHealth()
        {
            if (trackedBossHealth != null) trackedBossHealth.Damaged -= CaptureLatestBossPosition;
            trackedBossHealth = null;
        }

        private void GrantRewards(BossDefinition boss, bool defeated)
        {
            if (!defeated || boss == null)
            {
                StopTrackingBossHealth();
                return;
            }
            foreach (var reward in boss.GuaranteedDrops)
                if (reward.item != null && reward.amount > 0)
                {
                    RewardGranted?.Invoke(reward.item, reward.amount);
                    WorldItemDropRequest.Request(reward.item, reward.amount, lastBossPosition);
                }
            StopTrackingBossHealth();
        }
    }
}
