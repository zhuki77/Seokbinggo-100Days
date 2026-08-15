using System;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.Core
{
    public enum MiningImpactSurface { Dirt, Mineral }

    public static class GameEvents
    {
        public static event Action OnDayStart;
        public static event Action OnNightStart;
        public static event Action OnDawnWarning;
        public static event Action<Vector3Int> OnTilePlaced;
        public static event Action<Vector3Int> OnTileBroken;
        public static event Action<YokaiDefinition> OnYokaiKilled;
        public static event Action<BossDefinition> OnBossSummoned;
        public static event Action<BossDefinition> OnBossDefeated;
        public static event Action OnSealChanged;
        public static event Action OnBaekjungStart;
        public static event Action OnBaekjungEnd;
        public static event Action<MiningImpactSurface> OnMiningImpact;
        public static event Action OnItemAcquired;
        public static event Action OnPlayerDamaged;
        public static event Action OnYokaiDamaged;
        public static event Action OnMiningCritical;
        public static event Action<Vector3Int, bool, bool> OnMiningTargetChanged;
        public static event Action<Vector3Int, float> OnMiningProgress;
        public static event Action<Vector3Int, string, int, bool> OnMiningResult;
        /// <summary>채굴 조준 하이라이트. visible=false면 숨김.</summary>
        public static event Action<bool, Vector3Int> OnMiningTarget;
        public static event Action OnCraftingCompleted;
        public static event Action<RecipeDefinition> OnRecipeCrafted;
        public static event Action<string> OnPlacedObjectBuilt;
        public static event Action OnChestOpened;
        public static event Action OnWallDamaged;
        public static event Action<Vector3Int, float, float, bool> OnWallDurabilityChanged;
        public static event Action OnBossFled;
        public static event Action OnEoduksiniBloomed;
        public static event Action OnPlayerHeatPanting;
        public static event Action OnGoalBadgeCompleted;
        public static event Action OnPlayerDied;
        public static event Action OnInvasionAnnounced;
        public static event Action OnHypothermiaEntered;
        public static event Action OnFrostMineralRevealed;

        public static void RaiseDayStart() => OnDayStart?.Invoke();
        public static void RaiseNightStart() => OnNightStart?.Invoke();
        public static void RaiseDawnWarning() => OnDawnWarning?.Invoke();
        public static void RaiseTilePlaced(Vector3Int position) => OnTilePlaced?.Invoke(position);
        public static void RaiseTileBroken(Vector3Int position) => OnTileBroken?.Invoke(position);
        public static void RaiseYokaiKilled(YokaiDefinition definition) => OnYokaiKilled?.Invoke(definition);
        public static void RaiseBossSummoned(BossDefinition definition) => OnBossSummoned?.Invoke(definition);
        public static void RaiseBossDefeated(BossDefinition definition) => OnBossDefeated?.Invoke(definition);
        public static void RaiseSealChanged() => OnSealChanged?.Invoke();
        public static void RaiseBaekjungStart() => OnBaekjungStart?.Invoke();
        public static void RaiseBaekjungEnd() => OnBaekjungEnd?.Invoke();
        public static void RaiseMiningImpact(MiningImpactSurface surface) => OnMiningImpact?.Invoke(surface);
        public static void RaiseItemAcquired() => OnItemAcquired?.Invoke();
        public static void RaisePlayerDamaged() => OnPlayerDamaged?.Invoke();
        public static void RaiseYokaiDamaged() => OnYokaiDamaged?.Invoke();
        public static void RaiseMiningCritical() => OnMiningCritical?.Invoke();
        public static void RaiseMiningTargetChanged(Vector3Int cell, bool visible, bool mineable) =>
            OnMiningTargetChanged?.Invoke(cell, visible, mineable);
        public static void RaiseMiningProgress(Vector3Int cell, float normalizedProgress) =>
            OnMiningProgress?.Invoke(cell, Mathf.Clamp01(normalizedProgress));
        public static void RaiseMiningResult(Vector3Int cell, string itemName, int amount, bool critical) =>
            OnMiningResult?.Invoke(cell, itemName, amount, critical);
        public static void RaiseMiningTarget(bool visible, Vector3Int cell = default) =>
            OnMiningTarget?.Invoke(visible, cell);
        public static void RaiseCraftingCompleted(RecipeDefinition recipe = null)
        {
            OnCraftingCompleted?.Invoke();
            if (recipe != null) OnRecipeCrafted?.Invoke(recipe);
        }
        public static void RaisePlacedObjectBuilt(string definitionId)
        {
            if (!string.IsNullOrWhiteSpace(definitionId)) OnPlacedObjectBuilt?.Invoke(definitionId);
        }
        public static void RaiseChestOpened() => OnChestOpened?.Invoke();
        public static void RaiseWallDamaged() => OnWallDamaged?.Invoke();
        public static void RaiseWallDurabilityChanged(
            Vector3Int cell, float current, float maximum, bool destroyed) =>
            OnWallDurabilityChanged?.Invoke(
                cell, Mathf.Max(0f, current), Mathf.Max(0f, maximum), destroyed);
        public static void RaiseBossFled() => OnBossFled?.Invoke();
        public static void RaiseEoduksiniBloomed() => OnEoduksiniBloomed?.Invoke();
        public static void RaisePlayerHeatPanting() => OnPlayerHeatPanting?.Invoke();
        public static void RaiseGoalBadgeCompleted() => OnGoalBadgeCompleted?.Invoke();
        public static void RaisePlayerDied() => OnPlayerDied?.Invoke();
        public static void RaiseInvasionAnnounced() => OnInvasionAnnounced?.Invoke();
        public static void RaiseHypothermiaEntered() => OnHypothermiaEntered?.Invoke();
        public static void RaiseFrostMineralRevealed() => OnFrostMineralRevealed?.Invoke();
    }
}
