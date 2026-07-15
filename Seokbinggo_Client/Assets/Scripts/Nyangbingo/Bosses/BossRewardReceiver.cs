using Nyangbingo.Data;
using Nyangbingo.Inventory;
using UnityEngine;

namespace Nyangbingo.Bosses
{
    public sealed class BossRewardReceiver : MonoBehaviour
    {
        [SerializeField] private BossManager bossManager;
        private bool subscribed;
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
            bossManager.BossEnded += GrantRewards;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (bossManager != null) bossManager.BossEnded -= GrantRewards;
            subscribed = false;
        }

        private void GrantRewards(BossDefinition boss, bool defeated)
        {
            if (!defeated || boss == null) return;
            foreach (var reward in boss.GuaranteedDrops)
                if (reward.item != null && reward.amount > 0)
                {
                    RewardGranted?.Invoke(reward.item, reward.amount);
                    ItemAcquisition.Request(reward.item, reward.amount);
                }
        }
    }
}
