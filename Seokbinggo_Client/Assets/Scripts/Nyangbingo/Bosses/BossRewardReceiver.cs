using Nyangbingo.Data;
using Nyangbingo.Inventory;
using UnityEngine;

namespace Nyangbingo.Bosses
{
    public sealed class BossRewardReceiver : MonoBehaviour
    {
        [SerializeField] private BossManager bossManager;
        public event System.Action<ItemDefinition, int> RewardGranted;
        private void OnEnable() { if (bossManager != null) bossManager.BossEnded += GrantRewards; }
        private void OnDisable() { if (bossManager != null) bossManager.BossEnded -= GrantRewards; }
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
