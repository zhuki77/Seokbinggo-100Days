using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// day-curve-ext ice_melt_dps: 지표 직사 노출 중인 얼음류(인벤·월드 드랍)를 하루 1회 용해한다.
    /// 장독 보관(storage_melt_per_day)과 별도 규칙이다.
    /// </summary>
    public sealed class OutdoorIceMeltService : IDisposable
    {
        private readonly GameDataCatalog catalog;
        private readonly DayNightService time;
        private readonly MainGameBootstrap bootstrap;
        private readonly Func<Inventory.Inventory> inventoryProvider;
        private readonly Func<MainGameWorldDropRuntime> worldDropProvider;
        private bool disposed;

        public OutdoorIceMeltService(
            GameDataCatalog data,
            DayNightService timeService,
            MainGameBootstrap bootstrapValue,
            Func<Inventory.Inventory> playerInventoryProvider,
            Func<MainGameWorldDropRuntime> worldDropRuntimeProvider)
        {
            catalog = data ?? throw new ArgumentNullException(nameof(data));
            time = timeService ?? throw new ArgumentNullException(nameof(timeService));
            bootstrap = bootstrapValue ?? throw new ArgumentNullException(nameof(bootstrapValue));
            inventoryProvider = playerInventoryProvider ?? throw new ArgumentNullException(nameof(playerInventoryProvider));
            worldDropProvider = worldDropRuntimeProvider;
            time.DailyTick += HandleDailyTick;
        }

        public int LastMeltedItems { get; private set; }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            time.DailyTick -= HandleDailyTick;
        }

        public int ApplyDailyTick()
        {
            LastMeltedItems = 0;
            if (time.Day <= ExpansionProgressionRules.ExpansionStartDay) return 0;
            var curve = catalog.FindDayCurve(time.Day);
            var meltPerDay = DayCurveCombatRules.ResolveOutdoorIceMeltPerDay(curve);
            if (meltPerDay <= 0f) return 0;

            var surfaceHeights = bootstrap.Session?.LastResult.surfaceHeights;
            if (surfaceHeights == null || surfaceHeights.Length == 0) return 0;

            var inventory = inventoryProvider();
            if (inventory != null)
            {
                var player = bootstrap.GetComponentInChildren<MainGamePlayerController>();
                if (player != null &&
                    WorldExposureRules.TryIsSurfaceExposed(
                        player.transform.position, surfaceHeights, out var exposed) &&
                    exposed)
                    LastMeltedItems += inventory.ApplyOutdoorIceMelt(meltPerDay);
            }

            var worldDrops = worldDropProvider?.Invoke();
            if (worldDrops != null)
                LastMeltedItems += worldDrops.ApplyOutdoorIceMelt(meltPerDay, surfaceHeights);
            return LastMeltedItems;
        }

        private void HandleDailyTick() => ApplyDailyTick();
    }

    internal static class OutdoorIceMeltRules
    {
        public static bool IsIceItem(string itemId) =>
            itemId == StorageTemperatureService.IceShardId ||
            itemId == StorageTemperatureService.ThinIceId;
    }
}
