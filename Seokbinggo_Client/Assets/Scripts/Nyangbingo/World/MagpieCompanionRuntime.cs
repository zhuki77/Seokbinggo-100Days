using System;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// Non-combat v34 magpie companion. Progress is permanent, while collection is
    /// active only when its single nest still exists inside a sealed area.
    /// </summary>
    public sealed class MagpieCompanionRuntime : IGameSecondsTickable, IDisposable
    {
        public const int StorageSlotCount = 40;
        public const float FollowSpeedTilesPerGameSecond = 5f;
        public static readonly Vector2 DayFollowOffset = new Vector2(-1.1f, 1.15f);
        public static readonly Vector2 NestPerchOffset = new Vector2(0f, .85f);
        public static readonly Vector2 DropVisualOffset = new Vector2(0f, .5f);
        public const float CollectionContactRadius = .16f;

        private readonly Inventory.Inventory playerInventory;
        private readonly Inventory.Inventory nestStorage;
        private readonly MainGameEnvironmentState environmentState;
        private readonly MainGameWorldDropRuntime worldDrops;
        private readonly Transform player;
        private readonly DayNightService timeService;
        private readonly SealSystem sealSystem;
        private readonly int joinKillCount;
        private readonly float collectionRadius;
        private readonly float collectionIntervalSeconds;
        private Func<float> collectionRadiusMultiplier;
        private readonly GameObject visualRoot;
        private readonly RuntimeCharacterSpriteAnimator visualAnimator;

        private int killCount;
        private bool baekjungSurvived;
        private bool joined;
        private bool activeUntilNestRemoved;
        private float collectionElapsed;
        private Transform collectionTarget;
        private Inventory.Inventory collectionDestination;
        private bool notifyPlayerAcquisition;
        private Vector2 previousPlayerPosition;
        private float dayFollowSide = -1f;
        private bool playerPositionInitialized;
        private bool disposed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool editorTestOverride;
#endif

        public MagpieCompanionRuntime(GameDataCatalog catalog, Inventory.Inventory inventory,
            MainGameEnvironmentState environment, MainGameWorldDropRuntime dropRuntime,
            Transform playerTransform, DayNightService dayNightService, SealSystem worldSealSystem,
            CharacterArtCatalog characterArtCatalog = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            playerInventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            environmentState = environment ?? throw new ArgumentNullException(nameof(environment));
            worldDrops = dropRuntime ?? throw new ArgumentNullException(nameof(dropRuntime));
            player = playerTransform ?? throw new ArgumentNullException(nameof(playerTransform));
            previousPlayerPosition = player.position;
            playerPositionInitialized = true;
            timeService = dayNightService ?? throw new ArgumentNullException(nameof(dayNightService));
            sealSystem = worldSealSystem ?? throw new ArgumentNullException(nameof(worldSealSystem));

            joinKillCount = ReadPositiveInt(catalog, "magpie_join_kills");
            collectionRadius = ReadPositiveFloat(catalog, "magpie_magnet_radius");
            collectionIntervalSeconds = ReadPositiveFloat(catalog, "magpie_magnet_interval");
            nestStorage = new Inventory.Inventory(catalog.FindItem, StorageSlotCount);

            var art = characterArtCatalog?.Find("magpie");
            if (art?.Sprite != null)
            {
                visualRoot = new GameObject("MagpieCompanion");
                visualRoot.SetActive(false);
                var renderer = visualRoot.AddComponent<SpriteRenderer>();
                visualAnimator = visualRoot.AddComponent<RuntimeCharacterSpriteAnimator>();
                visualAnimator.Configure(art, 18);
                renderer.sortingOrder = 18;
            }

            GameEvents.OnYokaiKilled += HandleYokaiKilled;
            GameEvents.OnBaekjungEnd += HandleBaekjungEnd;
            GameEvents.OnDayStart += HandleDayStart;
        }

        public int KillCount => killCount;
        public bool BaekjungSurvived => baekjungSurvived;
        public bool Joined => joined;
        public bool IsActive => IsEditorTestOverrideActive ||
                                joined && activeUntilNestRemoved &&
                                TryResolveFunctionalNest(out _);
        public Inventory.Inventory NestStorage => nestStorage;

        public void ConfigureArtifactRadius(Func<float> multiplierProvider) =>
            collectionRadiusMultiplier = multiplierProvider;

        public void Tick(float deltaGameSeconds)
        {
            if (disposed || !IsFinitePositive(deltaGameSeconds))
                return;
            var testOverride = IsEditorTestOverrideActive;
            if (!testOverride && (!joined || !activeUntilNestRemoved))
            {
                CancelCollection();
                RefreshVisual(Vector2.zero, deltaGameSeconds, false);
                return;
            }
            var hasFunctionalNest = TryResolveFunctionalNest(out var nestPosition);
            if (!testOverride && !hasFunctionalNest)
            {
                activeUntilNestRemoved = false;
                CancelCollection();
                RefreshVisual(Vector2.zero, deltaGameSeconds, false);
                return;
            }

            var returnToNest = timeService.IsNight && hasFunctionalNest;
            RefreshDayFollowSide();
            var restingTarget = returnToNest
                ? nestPosition + NestPerchOffset
                : (Vector2)player.position + ResolveDayFollowOffset();

            if (collectionTarget == null)
            {
                collectionDestination = null;
                notifyPlayerAcquisition = false;
                collectionElapsed += deltaGameSeconds;
                if (collectionElapsed >= collectionIntervalSeconds)
                {
                    collectionElapsed %= collectionIntervalSeconds;
                    var destination = returnToNest ? nestStorage : playerInventory;
                    var origin = returnToNest ? nestPosition : (Vector2)player.position;
                    var radius = collectionRadius * ResolveCollectionRadiusMultiplier();
                    if (worldDrops.TryFindNearestStack(
                            origin, radius, out var target))
                    {
                        collectionTarget = target;
                        collectionDestination = destination;
                        notifyPlayerAcquisition = !returnToNest;
                    }
                }
            }

            var visualTarget = collectionTarget != null
                ? (Vector2)collectionTarget.position + DropVisualOffset
                : restingTarget;
            RefreshVisual(visualTarget, deltaGameSeconds, true);

            if (collectionTarget == null || visualRoot == null ||
                ((Vector2)visualRoot.transform.position - visualTarget).sqrMagnitude >
                CollectionContactRadius * CollectionContactRadius)
                return;

            if (worldDrops.TryCollectStack(
                    collectionTarget, collectionDestination, notifyPlayerAcquisition))
                visualAnimator?.PlayAttack();
            CancelCollection();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool ToggleEditorTestOverride()
        {
            if (disposed) return false;
            editorTestOverride = !editorTestOverride;
            CancelCollection();
            if (!editorTestOverride)
                RefreshVisual(Vector2.zero, 0f, false);
            Debug.Log($"[Nyangbingo] Alt+M magpie test override " +
                      $"{(editorTestOverride ? "enabled" : "disabled")}.");
            return editorTestOverride;
        }
#endif

        public bool Capture(SaveGame save)
        {
            if (save == null || disposed) return false;
            save.magpieKillCount = killCount;
            save.magpieBaekjungSurvived = baekjungSurvived;
            save.magpieJoined = joined;
            save.magpieNestPosition = environmentState.TryGetNearestPlacedObjectPosition(
                MainGameEnvironmentState.MagpieNestDefinitionId, Vector2.zero, out var nestPosition)
                ? nestPosition
                : Vector2.zero;
            save.magpieStorage = nestStorage.Export();
            return true;
        }

        public bool Restore(SaveGame save)
        {
            if (save == null || disposed || save.magpieKillCount < 0 ||
                save.magpieStorage == null || !nestStorage.TryImport(save.magpieStorage))
                return false;
            killCount = save.magpieKillCount;
            baekjungSurvived = save.magpieBaekjungSurvived;
            joined = save.magpieJoined;
            CancelCollection();
            activeUntilNestRemoved = joined && TryResolveFunctionalNest(out _);
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            GameEvents.OnYokaiKilled -= HandleYokaiKilled;
            GameEvents.OnBaekjungEnd -= HandleBaekjungEnd;
            GameEvents.OnDayStart -= HandleDayStart;
            if (visualRoot != null) UnityEngine.Object.Destroy(visualRoot);
        }

        private void HandleYokaiKilled(YokaiDefinition _)
        {
            if (disposed || killCount == int.MaxValue) return;
            killCount++;
        }

        private void HandleBaekjungEnd()
        {
            if (!disposed) baekjungSurvived = true;
        }

        private void HandleDayStart()
        {
            if (disposed) return;
            var eligible = killCount >= joinKillCount || baekjungSurvived;
            if (!joined && eligible && TryResolveFunctionalNest(out _))
                joined = true;
            activeUntilNestRemoved = joined && TryResolveFunctionalNest(out _);
            CancelCollection();
            if (!activeUntilNestRemoved) return;

            var transferredAny = false;
            for (var index = 0; index < nestStorage.Slots.Count; index++)
                transferredAny |= nestStorage.TryTransferSlotTo(index, playerInventory);
            if (transferredAny) GameEvents.RaiseItemAcquired();
        }

        private Vector2 ResolveDayFollowOffset()
        {
            return new Vector2(
                Mathf.Abs(DayFollowOffset.x) * dayFollowSide,
                DayFollowOffset.y);
        }

        private void RefreshDayFollowSide()
        {
            var currentPlayerPosition = (Vector2)player.position;
            if (!playerPositionInitialized)
            {
                previousPlayerPosition = currentPlayerPosition;
                playerPositionInitialized = true;
                return;
            }

            var horizontalMovement = currentPlayerPosition.x - previousPlayerPosition.x;
            previousPlayerPosition = currentPlayerPosition;
            if (Mathf.Abs(horizontalMovement) < .02f) return;
            dayFollowSide = horizontalMovement > 0f ? -1f : 1f;
        }

        private void RefreshVisual(Vector2 target, float deltaGameSeconds, bool visible)
        {
            if (visualRoot == null) return;
            var becameVisible = visible && !visualRoot.activeSelf;
            if (visualRoot.activeSelf != visible) visualRoot.SetActive(visible);
            if (!visible) return;

            var current = (Vector2)visualRoot.transform.position;
            if (becameVisible)
            {
                current = target;
                visualRoot.transform.position = current;
            }
            var next = Vector2.MoveTowards(
                current, target,
                FollowSpeedTilesPerGameSecond * Mathf.Max(0f, deltaGameSeconds));
            visualRoot.transform.position = next;
            var movement = next - current;
            // Beside the player and while collecting, the companion remains airborne even
            // when holding position. It only uses the seated frame after returning to its nest.
            var seatedAtNest = timeService.IsNight &&
                               collectionTarget == null &&
                               TryResolveFunctionalNest(out var nestPosition) &&
                               (next - (nestPosition + NestPerchOffset)).sqrMagnitude <=
                               CollectionContactRadius * CollectionContactRadius;
            visualAnimator?.SetMoving(!seatedAtNest);
            if (Mathf.Abs(movement.x) > .0001f)
                visualAnimator?.SetFacing(movement);
        }

        private bool TryResolveFunctionalNest(out Vector2 nestPosition)
        {
            return environmentState.TryGetNearestPlacedObjectPosition(
                       MainGameEnvironmentState.MagpieNestDefinitionId,
                       Vector2.zero, out nestPosition) &&
                   sealSystem.IsInsideSealedArea(nestPosition);
        }

        private void CancelCollection()
        {
            collectionElapsed = 0f;
            collectionTarget = null;
            collectionDestination = null;
            notifyPlayerAcquisition = false;
        }

        private float ResolveCollectionRadiusMultiplier()
        {
            var multiplier = collectionRadiusMultiplier != null ? collectionRadiusMultiplier() : 1f;
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0f)
                return 1f;
            return multiplier;
        }

        private static int ReadPositiveInt(GameDataCatalog catalog, string key)
        {
            var definition = catalog.FindGlobal(key);
            if (definition == null || !definition.TryGetInt(out var value) || value <= 0)
                throw new InvalidOperationException($"Invalid magpie global '{key}'.");
            return value;
        }

        private static float ReadPositiveFloat(GameDataCatalog catalog, string key)
        {
            var definition = catalog.FindGlobal(key);
            if (definition == null || !definition.TryGetFloat(out var value) ||
                !IsFinitePositive(value))
                throw new InvalidOperationException($"Invalid magpie global '{key}'.");
            return value;
        }

        private static bool IsFinitePositive(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private bool IsEditorTestOverrideActive
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return editorTestOverride;
#else
                return false;
#endif
            }
        }
    }
}
