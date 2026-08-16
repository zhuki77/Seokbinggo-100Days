using System;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>v72 A-13. talismans.csv 5종의 이동·인식·저체온 런타임.</summary>
    public sealed class TalismanRuntime : IGameSecondsTickable
    {
        public const string ReturnId = "tal_return";
        public const string StrideId = "tal_stride";
        public const string WaypointId = "tal_waypoint";
        public const string HideId = "tal_hide";
        public const string FrostId = "tal_frost";
        public const float StrideDurationSeconds = 60f;
        public const float StrideMultiplier = 1.5f;
        public const float HideDurationSeconds = 30f;
        public const float FrostDurationSeconds = 120f;

        private readonly GameDataCatalog catalog;
        private readonly Inventory.Inventory inventory;
        private readonly MainGameEnvironmentState environment;
        private Transform player;

        public TalismanRuntime(GameDataCatalog data, Inventory.Inventory playerInventory,
            MainGameEnvironmentState environmentState)
        {
            catalog = data ?? throw new ArgumentNullException(nameof(data));
            inventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            environment = environmentState ?? throw new ArgumentNullException(nameof(environmentState));
            if (catalog.FindGlobal(GlobalKeys.TalismanTable)?.Value != "talismans.csv" ||
                catalog.FindGlobal(GlobalKeys.TalismanCount) is not { } count ||
                !count.TryGetInt(out var parsedCount) || parsedCount != 5 ||
                catalog.FindTalisman(ReturnId) == null || catalog.FindTalisman(StrideId) == null ||
                catalog.FindTalisman(WaypointId) == null || catalog.FindTalisman(HideId) == null ||
                catalog.FindTalisman(FrostId) == null)
                throw new InvalidOperationException("v72 부적 5종 데이터가 올바르지 않습니다.");
        }

        public float StrideRemaining { get; private set; }
        public float HideRemaining { get; private set; }
        public float FrostRemaining { get; private set; }
        public float MovementMultiplier => StrideRemaining > 0f ? StrideMultiplier : 1f;
        public bool IgnoresYokaiAggro => HideRemaining > 0f;
        public bool SuppressesHypothermia => FrostRemaining > 0f;
        public event Action Changed;

        public void BindPlayer(Transform playerTransform) => player = playerTransform;

        public static bool IsConsumableId(string itemId) =>
            itemId == ReturnId || itemId == StrideId || itemId == HideId || itemId == FrostId;

        public bool TryUse(string itemId, out string message)
        {
            message = string.Empty;
            if (!IsConsumableId(itemId) || catalog.FindTalisman(itemId) == null || player == null)
                return false;
            if (itemId == ReturnId)
            {
                if (!environment.TryGetNearestPlacedObjectPosition(
                        SeokbinggoRules.IceCoreDefinitionId, player.position, out var corePosition))
                {
                    message = "귀환할 얼음 저장고 코어가 없습니다.";
                    return false;
                }
                if (!inventory.TryRemove(itemId, 1))
                {
                    message = "귀환부가 없습니다.";
                    return false;
                }
                TeleportPlayer(corePosition);
                message = "귀환부 사용 · 석빙고 코어로 귀환";
                Changed?.Invoke();
                return true;
            }
            if (!inventory.TryRemove(itemId, 1))
            {
                message = "사용할 부적이 없습니다.";
                return false;
            }
            switch (itemId)
            {
                case StrideId:
                    StrideRemaining = StrideDurationSeconds;
                    message = "축지부 사용 · 이동 속도 +50% · 60초";
                    break;
                case HideId:
                    HideRemaining = HideDurationSeconds;
                    message = "은신부 사용 · 요괴 인식 무시 · 30초";
                    break;
                case FrostId:
                    FrostRemaining = FrostDurationSeconds;
                    message = "한기부 사용 · 저체온 하강 정지 · 120초";
                    break;
            }
            Changed?.Invoke();
            return true;
        }

        public bool TryUseWaypoint(bool fromCore, out string message)
        {
            message = string.Empty;
            if (player == null) return false;
            var targetDefinition = fromCore ? WaypointId : SeokbinggoRules.IceCoreDefinitionId;
            if (!environment.TryGetNearestPlacedObjectPosition(
                    targetDefinition, player.position, out var targetPosition))
            {
                message = fromCore ? "설치된 이정표 부적이 없습니다." : "얼음 저장고 코어가 없습니다.";
                return false;
            }
            TeleportPlayer(targetPosition);
            message = fromCore ? "이정표 부적으로 이동" : "이정표 부적에서 석빙고로 이동";
            return true;
        }

        public void Tick(float deltaGameSeconds)
        {
            if (deltaGameSeconds <= 0f || float.IsNaN(deltaGameSeconds) || float.IsInfinity(deltaGameSeconds))
                return;
            var previousStride = StrideRemaining;
            var previousHide = HideRemaining;
            var previousFrost = FrostRemaining;
            StrideRemaining = Mathf.Max(0f, StrideRemaining - deltaGameSeconds);
            HideRemaining = Mathf.Max(0f, HideRemaining - deltaGameSeconds);
            FrostRemaining = Mathf.Max(0f, FrostRemaining - deltaGameSeconds);
            if (!Mathf.Approximately(previousStride, StrideRemaining) ||
                !Mathf.Approximately(previousHide, HideRemaining) ||
                !Mathf.Approximately(previousFrost, FrostRemaining)) Changed?.Invoke();
        }

        public bool Restore(float strideRemaining, float hideRemaining, float frostRemaining)
        {
            if (!IsFiniteDuration(strideRemaining, StrideDurationSeconds) ||
                !IsFiniteDuration(hideRemaining, HideDurationSeconds) ||
                !IsFiniteDuration(frostRemaining, FrostDurationSeconds)) return false;
            StrideRemaining = strideRemaining;
            HideRemaining = hideRemaining;
            FrostRemaining = frostRemaining;
            Changed?.Invoke();
            return true;
        }

        private void TeleportPlayer(Vector2 position)
        {
            player.position = new Vector3(position.x, position.y, player.position.z);
            var body = player.GetComponent<Rigidbody2D>();
            if (body == null) return;
            body.position = position;
            body.linearVelocity = Vector2.zero;
        }

        private static bool IsFiniteDuration(float value, float maximum) =>
            value >= 0f && value <= maximum && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
