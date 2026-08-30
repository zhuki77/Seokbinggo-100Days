using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Save;
using Nyangbingo.World;
using UnityEngine;

namespace Nyangbingo.Inventory
{
    public readonly struct ArtifactActivationContext
    {
        public ArtifactActivationContext(bool isDeep, bool isSurface, bool isDay)
        {
            IsDeep = isDeep;
            IsSurface = isSurface;
            IsDay = isDay;
        }

        public bool IsDeep { get; }
        public bool IsSurface { get; }
        public bool IsDay { get; }
    }

    /// <summary>
    /// v46 아티팩트 동사 런타임. P5 스키마 위에 modifier·일일 제한·이벤트 훅을 제공한다.
    /// </summary>
    public sealed class ArtifactVerbRuntime
    {
        public const float MagpieRadiusMultiplier = 1.5f;
        public const float CoolerRadiusMultiplier = 1.25f;
        public const float DeepVisionBonusTiles = 3f;
        public const float ReduceFlameTagModifier = -.25f;
        public const float ReduceFlameAndHastenModifier = -.15f;
        public const float AltarTearDiscount = .25f;
        public const float CodexTearBonus = 1f;
        public const float ClayCraftDurationMultiplier = .5f;
        public const float SalvageRecoveryMultiplier = 1f;
        public const float OreEchoHighlightSeconds = 4f;

        private readonly Dictionary<string, int> dailyUses =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public void ResetDailyUses() => dailyUses.Clear();

        public bool IsVerbActive(
            EquipmentSystem equipment,
            ArtifactVerbId verb,
            ArtifactActivationContext context)
        {
            if (!ArtifactRules.TryFindEquipped(equipment, verb, out var definition) ||
                definition == null)
                return false;
            return ArtifactRules.IsActivationMet(definition.ActivationCondition,
                context.IsDeep, context.IsSurface, context.IsDay);
        }

        public bool TryConsumeDailyUse(EquipmentSystem equipment, ArtifactVerbId verb)
        {
            if (!ArtifactRules.TryFindEquipped(equipment, verb, out var definition) ||
                definition == null || definition.UsageLimitPerDay <= 0)
                return true;
            var key = definition.Id;
            dailyUses.TryGetValue(key, out var used);
            if (used >= definition.UsageLimitPerDay) return false;
            dailyUses[key] = used + 1;
            return true;
        }

        public int DailyUseCount(string equipmentId) =>
            !string.IsNullOrWhiteSpace(equipmentId) && dailyUses.TryGetValue(equipmentId, out var used)
                ? used
                : 0;

        public void RestoreDailyUse(string equipmentId, int count)
        {
            if (string.IsNullOrWhiteSpace(equipmentId) || count <= 0) return;
            dailyUses[equipmentId] = count;
        }

        public float ResolveMagpieRadiusMultiplier(EquipmentSystem equipment, ArtifactActivationContext context) =>
            IsVerbActive(equipment, ArtifactVerbId.ExtendMagpieRadius, context)
                ? MagpieRadiusMultiplier
                : 1f;

        public float ResolveCoolerRadiusMultiplier(EquipmentSystem equipment, ArtifactActivationContext context) =>
            IsVerbActive(equipment, ArtifactVerbId.ExtendCoolerRadius, context)
                ? CoolerRadiusMultiplier
                : 1f;

        public float ResolveDeepVisionBonusTiles(EquipmentSystem equipment, ArtifactActivationContext context) =>
            IsVerbActive(equipment, ArtifactVerbId.IncreaseVisionDeep, context)
                ? DeepVisionBonusTiles
                : 0f;

        public float ResolveFireDamageModifier(EquipmentSystem equipment, ArtifactActivationContext context)
        {
            var modifier = 0f;
            if (IsVerbActive(equipment, ArtifactVerbId.ReduceFlameTag, context))
                modifier += ReduceFlameTagModifier;
            if (IsVerbActive(equipment, ArtifactVerbId.ReduceFlameAndHasten, context))
                modifier += ReduceFlameAndHastenModifier;
            return modifier;
        }

        public float ResolveCraftDurationMultiplier(
            EquipmentSystem equipment, RecipeDefinition recipe, ArtifactActivationContext context)
        {
            if (recipe?.Output.item == null ||
                !string.Equals(recipe.Output.item.Id, "clay_plaster", StringComparison.Ordinal) ||
                !IsVerbActive(equipment, ArtifactVerbId.HalveClayCraftTime, context))
                return 1f;
            return ClayCraftDurationMultiplier;
        }

        public float ResolveAltarTearMultiplier(EquipmentSystem equipment, ArtifactActivationContext context) =>
            IsVerbActive(equipment, ArtifactVerbId.ReduceOfferTears, context)
                ? 1f - AltarTearDiscount
                : 1f;

        public float ResolveCodexTearBonus(EquipmentSystem equipment, ArtifactActivationContext context) =>
            IsVerbActive(equipment, ArtifactVerbId.BonusTearOnCodex, context) ? CodexTearBonus : 0f;

        public bool AllowsRemoteJangdok(EquipmentSystem equipment, ArtifactActivationContext context) =>
            IsVerbActive(equipment, ArtifactVerbId.OpenStorageAnywhere, context) &&
            TryConsumeDailyUse(equipment, ArtifactVerbId.OpenStorageAnywhere);

        public bool AllowsFullSalvage(EquipmentSystem equipment, ArtifactActivationContext context) =>
            IsVerbActive(equipment, ArtifactVerbId.FullDemolitionRecovery, context);

        public bool AllowsTurnWhileSliding(EquipmentSystem equipment, ArtifactActivationContext context) =>
            IsVerbActive(equipment, ArtifactVerbId.TurnWhileSliding, context);

        public bool AllowsWalkWhileCharging(EquipmentSystem equipment, ArtifactActivationContext context) =>
            IsVerbActive(equipment, ArtifactVerbId.WalkWhileCharging, context);

        public bool SuppressesFirstStrike(EquipmentSystem equipment, ArtifactActivationContext context) =>
            IsVerbActive(equipment, ArtifactVerbId.NoFirstStrike, context);

        public bool LocksShadeTemperature(EquipmentSystem equipment, ArtifactActivationContext context) =>
            IsVerbActive(equipment, ArtifactVerbId.NoHeatInShade, context);

        public bool MaintainsModuleAfterShutdown(EquipmentSystem equipment, ArtifactActivationContext context) =>
            IsVerbActive(equipment, ArtifactVerbId.MaintainAfterShutdown, context);

        public bool CanRelocateColdCore(EquipmentSystem equipment, ArtifactActivationContext context) =>
            IsVerbActive(equipment, ArtifactVerbId.RelocateColdCore, context);

        public bool CanGrabKnockedTarget(EquipmentSystem equipment, ArtifactActivationContext context) =>
            IsVerbActive(equipment, ArtifactVerbId.GrabKnockedTarget, context);

        public bool CanEscapeOnSwallow(EquipmentSystem equipment, ArtifactActivationContext context) =>
            IsVerbActive(equipment, ArtifactVerbId.EscapeOnSwallow, context) &&
            TryConsumeDailyUse(equipment, ArtifactVerbId.EscapeOnSwallow);

        public bool HighlightsOreVeins(EquipmentSystem equipment, ArtifactActivationContext context) =>
            IsVerbActive(equipment, ArtifactVerbId.HearIronVein, context);

        public bool ShowsDugPaths(EquipmentSystem equipment, ArtifactActivationContext context) =>
            IsVerbActive(equipment, ArtifactVerbId.ShowDugPaths, context);

        public List<ArtifactDailyUseRecord> ExportDailyUses()
        {
            var records = new List<ArtifactDailyUseRecord>();
            foreach (var pair in dailyUses)
            {
                if (pair.Value <= 0 || string.IsNullOrWhiteSpace(pair.Key)) continue;
                records.Add(new ArtifactDailyUseRecord
                {
                    equipmentId = pair.Key,
                    count = pair.Value
                });
            }
            return records;
        }

        public void RestoreDailyUses(IEnumerable<ArtifactDailyUseRecord> records)
        {
            dailyUses.Clear();
            if (records == null) return;
            foreach (var record in records)
                RestoreDailyUse(record.equipmentId, record.count);
        }
    }

    public static class ArtifactActivationContextFactory
    {
        public static ArtifactActivationContext Build(
            TileService tileService, Vector2 worldPosition, DayNightService timeService)
        {
            var isDay = timeService == null || !timeService.IsNight;
            var depth = 1;
            if (tileService != null &&
                tileService.TryGetSurfaceRelativeDepth(worldPosition, out var resolvedDepth) &&
                resolvedDepth > 0)
                depth = resolvedDepth;
            return new ArtifactActivationContext(depth >= 91, depth <= 1, isDay);
        }
    }
}
