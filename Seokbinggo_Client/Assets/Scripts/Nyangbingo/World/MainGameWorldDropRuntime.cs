using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using Nyangbingo.Save;
using UnityEngine;

namespace Nyangbingo.World
{
    public static class WorldItemDropRequest
    {
        public static event Action<ItemDefinition, int, Vector2, Vector3Int?> Requested;

        public static void Request(ItemDefinition item, int amount, Vector2 position,
            Vector3Int? minedCell = null)
        {
            if (item == null || amount <= 0) return;
            if (Requested == null)
            {
                ItemAcquisition.Request(item, amount);
                return;
            }
            Requested.Invoke(item, amount, position, minedCell);
        }
    }

    public sealed class MainGameWorldDropRuntime : MonoBehaviour
    {
        private sealed class Entry
        {
            public ItemDefinition Item;
            public int Amount;
            public GameObject Root;
            public Rigidbody2D Body;
            public Collider2D Collider;
            public float PickupDelay;
        }

        public const float MagnetRadius = 1.5f;
        // Terrain visuals now share the logical Grid boundary. This remains as a named value
        // because vegetation and drop rendering use the same surface contract.
        public const float VisualSurfaceOffset = 0f;
        public const float DropColliderRadius = .22f;
        public const bool DropToDropCollisionResponseEnabled = false;
        private const float MinimumLaunchAngle = 25f;
        private const float MaximumLaunchAngle = 155f;
        private const float BaseLaunchSpeed = 2.2f;
        private const float LaunchSpeedPerExtraDrop = .12f;
        private const float MaximumLaunchSpeedBonus = 1.8f;
        private const float PickupRadius = .22f;
        private const float MagnetSpeed = 6f;
        private const float Gravity = 12f;
        private const float MaximumFallSpeed = 10f;
        private const float InitialPickupDelay = .45f;

        private readonly List<Entry> drops = new List<Entry>();
        private static readonly HashSet<Collider2D> ActiveDropColliders = new HashSet<Collider2D>();
        private Transform player;
        private Nyangbingo.Inventory.Inventory inventory;
        private ItemArtCatalog itemArtCatalog;
        private Collider2D[] playerColliders = Array.Empty<Collider2D>();
        private PhysicsMaterial2D dropMaterial;
        private TileService tileService;

        public int ActiveDropCount => drops.Count;

        /// <summary>
        /// Finds the actual nearest drop transform without removing it. Companions can
        /// follow this transform until they visibly reach the item.
        /// </summary>
        public bool TryFindNearestStack(Vector2 origin, float radius, out Transform target)
        {
            target = null;
            if (!TryFindNearestEntry(origin, radius, out var nearest, out _))
                return false;
            target = nearest.Root.transform;
            return true;
        }

        /// <summary>
        /// Collects one exact drop previously selected by its transform.
        /// </summary>
        public bool TryCollectStack(Transform target,
            Nyangbingo.Inventory.Inventory destination, bool notifyPlayerAcquisition = false)
        {
            if (target == null || destination == null) return false;
            for (var index = 0; index < drops.Count; index++)
            {
                var entry = drops[index];
                if (entry?.Root == null || entry.Root.transform != target ||
                    entry.Item == null || entry.Amount <= 0)
                    continue;
                return TryCollectEntry(entry, index, destination, notifyPlayerAcquisition);
            }
            return false;
        }

        /// <summary>
        /// Removes exactly one nearest world-drop stack after the destination accepts it.
        /// The magpie uses the same authoritative drop list as manual player pickup, so a
        /// collected stack cannot remain visible, be stolen, or be restored twice.
        /// </summary>
        public bool TryCollectNearestStack(Vector2 origin, float radius,
            Nyangbingo.Inventory.Inventory destination, bool notifyPlayerAcquisition = false)
        {
            if (destination == null ||
                !TryFindNearestEntry(origin, radius, out var nearest, out var nearestIndex))
                return false;
            return TryCollectEntry(
                nearest, nearestIndex, destination, notifyPlayerAcquisition);
        }

        public bool TryStealNearestStack(Vector2 origin,
            out ItemDefinition stolenItem, out int stolenAmount)
        {
            stolenItem = null;
            stolenAmount = 0;
            if (!IsFinite(origin)) return false;
            Entry nearest = null;
            var nearestIndex = -1;
            var nearestDistance = float.PositiveInfinity;
            for (var index = 0; index < drops.Count; index++)
            {
                var entry = drops[index];
                if (entry?.Item == null || entry.Root == null || entry.Amount <= 0) continue;
                var distance = ((Vector2)entry.Root.transform.position - origin).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = entry;
                nearestIndex = index;
                nearestDistance = distance;
            }
            if (nearest == null || nearestIndex < 0) return false;
            stolenItem = nearest.Item;
            stolenAmount = nearest.Amount;
            if (nearest.Collider != null) ActiveDropColliders.Remove(nearest.Collider);
            Destroy(nearest.Root);
            drops.RemoveAt(nearestIndex);
            return true;
        }

        private bool TryFindNearestEntry(Vector2 origin, float radius,
            out Entry nearest, out int nearestIndex)
        {
            nearest = null;
            nearestIndex = -1;
            if (!IsFinite(origin) || !IsFiniteNonNegative(radius)) return false;
            var nearestDistance = radius * radius;
            for (var index = 0; index < drops.Count; index++)
            {
                var entry = drops[index];
                if (entry?.Item == null || entry.Root == null || entry.Amount <= 0) continue;
                var distance = ((Vector2)entry.Root.transform.position - origin).sqrMagnitude;
                if (distance > nearestDistance) continue;
                if (nearest != null && (distance > nearestDistance ||
                    Mathf.Approximately(distance, nearestDistance) &&
                    string.CompareOrdinal(entry.Root.name, nearest.Root.name) >= 0))
                    continue;
                nearest = entry;
                nearestIndex = index;
                nearestDistance = distance;
            }
            return nearest != null && nearestIndex >= 0;
        }

        private bool TryCollectEntry(Entry entry, int index,
            Nyangbingo.Inventory.Inventory destination, bool notifyPlayerAcquisition)
        {
            if (entry == null || index < 0 || index >= drops.Count ||
                drops[index] != entry || destination == null ||
                !destination.TryAdd(entry.Item.Id, entry.Amount))
                return false;

            if (entry.Collider != null) ActiveDropColliders.Remove(entry.Collider);
            Destroy(entry.Root);
            drops.RemoveAt(index);
            if (notifyPlayerAcquisition) GameEvents.RaiseItemAcquired();
            return true;
        }

        public List<WorldDropStateRecord> Export()
        {
            var result = new List<WorldDropStateRecord>(drops.Count);
            for (var index = 0; index < drops.Count; index++)
            {
                var entry = drops[index];
                if (entry?.Item == null || entry.Root == null) continue;
                result.Add(new WorldDropStateRecord
                {
                    itemId = entry.Item.Id,
                    amount = entry.Amount,
                    position = entry.Root.transform.position,
                    velocity = entry.Body != null ? entry.Body.linearVelocity : Vector2.zero,
                    pickupDelay = entry.PickupDelay
                });
            }
            return result;
        }

        public bool Restore(IEnumerable<WorldDropStateRecord> records,
            Func<string, ItemDefinition> findItem)
        {
            if (records == null || findItem == null) return false;
            var validated = new List<(WorldDropStateRecord record, ItemDefinition item)>();
            foreach (var record in records)
            {
                var item = findItem(record.itemId);
                if (item == null || record.amount <= 0 || record.amount > item.MaxStack ||
                    !IsFinite(record.position) ||
                    !IsFinite(record.velocity) || !IsFiniteNonNegative(record.pickupDelay))
                    return false;
                validated.Add((record, item));
            }

            ClearDrops();
            for (var index = 0; index < validated.Count; index++)
            {
                var pair = validated[index];
                var entry = SpawnSingle(pair.item, pair.record.position, 0, 1, null);
                if (entry == null)
                {
                    ClearDrops();
                    return false;
                }
                entry.Amount = pair.record.amount;
                entry.Root.transform.position = pair.record.position;
                entry.PickupDelay = pair.record.pickupDelay;
                if (entry.Body != null) entry.Body.linearVelocity = pair.record.velocity;
            }
            Physics2D.SyncTransforms();
            return true;
        }

        public void ConfigureForRuntime(Transform playerTransform, Nyangbingo.Inventory.Inventory playerInventory,
            ItemArtCatalog artCatalog, TileService worldTileService)
        {
            player = playerTransform;
            inventory = playerInventory;
            itemArtCatalog = artCatalog;
            tileService = worldTileService;
            playerColliders = playerTransform != null
                ? playerTransform.GetComponentsInChildren<Collider2D>(true)
                : Array.Empty<Collider2D>();
            if (dropMaterial == null)
            {
                dropMaterial = new PhysicsMaterial2D("NyangbingoWorldDrop")
                {
                    friction = .55f,
                    bounciness = .08f
                };
            }
        }

        private void OnEnable() => WorldItemDropRequest.Requested += Spawn;
        private void OnDisable() => WorldItemDropRequest.Requested -= Spawn;

        private void Update()
        {
            if (player == null || inventory == null || Time.deltaTime <= 0f) return;
            var acquiredAny = false;
            for (var index = drops.Count - 1; index >= 0; index--)
            {
                var entry = drops[index];
                if (entry?.Root == null)
                {
                    if (entry?.Collider != null) ActiveDropColliders.Remove(entry.Collider);
                    drops.RemoveAt(index);
                    continue;
                }

                var delta = (Vector2)player.position - (Vector2)entry.Root.transform.position;
                entry.PickupDelay = Mathf.Max(0f, entry.PickupDelay - Time.deltaTime);
                var magnetActive = entry.PickupDelay <= 0f && delta.sqrMagnitude <= MagnetRadius * MagnetRadius;
                if (entry.Body != null)
                {
                    entry.Body.gravityScale = magnetActive ? 0f : ResolveGravityScale();
                    if (magnetActive && delta.sqrMagnitude > Mathf.Epsilon)
                        entry.Body.linearVelocity = delta.normalized * MagnetSpeed;
                    else if (entry.Body.linearVelocity.y < -MaximumFallSpeed)
                        entry.Body.linearVelocity = new Vector2(entry.Body.linearVelocity.x, -MaximumFallSpeed);
                }
                if (!magnetActive) continue;
                if (((Vector2)player.position - (Vector2)entry.Root.transform.position).sqrMagnitude >
                    PickupRadius * PickupRadius) continue;
                if (!inventory.TryAdd(entry.Item.Id, entry.Amount)) continue;
                acquiredAny = true;
                if (entry.Collider != null) ActiveDropColliders.Remove(entry.Collider);
                Destroy(entry.Root);
                drops.RemoveAt(index);
            }
            if (acquiredAny) GameEvents.RaiseItemAcquired();
        }

        private void Spawn(ItemDefinition item, int amount, Vector2 position, Vector3Int? minedCell)
        {
            if (item == null || amount <= 0) return;
            for (var index = 0; index < amount; index++)
                SpawnSingle(item, position, index, amount, minedCell);
        }

        private Entry SpawnSingle(ItemDefinition item, Vector2 position, int batchIndex, int batchCount,
            Vector3Int? minedCell)
        {
            if (item == null) return null;
            var root = new GameObject($"WorldDrop_{item.Id}");
            root.transform.SetParent(transform, false);
            var direction = CalculateLaunchDirection(batchIndex, batchCount);
            root.transform.position = position + direction * .08f;

            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = ResolveGravityScale();
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearDamping = .8f;
            body.linearVelocity = direction * CalculateLaunchSpeed(batchCount);
            var dropCollider = root.AddComponent<CircleCollider2D>();
            dropCollider.radius = DropColliderRadius;
            dropCollider.sharedMaterial = dropMaterial;
            for (var index = 0; index < playerColliders.Length; index++)
                if (playerColliders[index] != null)
                    Physics2D.IgnoreCollision(dropCollider, playerColliders[index], true);
            IgnoreCollisionWithExistingDrops(dropCollider);
            WorldMobPhysicsBody.IgnoreCollisionWithActiveMobs(dropCollider);
            ActiveDropColliders.Add(dropCollider);

            // Keep the simulated drop root at unit scale. Delivered Aseprite files use
            // different canvas sizes and pivots, so scaling the root would make the art
            // appear far away from the position used for collision and pickup checks.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            var sprite = itemArtCatalog?.FindSprite(item.Id);
            if (sprite != null)
                ConfigureDropVisual(renderer, sprite, .42f);
            else
            {
                RuntimePlaceholderVisual.Configure(renderer, new Color(.85f, .92f, 1f, 1f), .42f, 32);
                renderer.transform.localPosition = new Vector3(0f, -.21f + VisualSurfaceOffset, 0f);
            }

            if (minedCell.HasValue && tileService != null)
            {
                var snapped = tileService.ResolveForegroundMiningDropWorldPosition(minedCell.Value);
                root.transform.position = new Vector3(snapped.x, snapped.y, root.transform.position.z) +
                                          (Vector3)(direction * .08f);
            }

            var entry = new Entry
            {
                Item = item,
                Amount = 1,
                Root = root,
                Body = body,
                Collider = dropCollider,
                PickupDelay = InitialPickupDelay
            };
            drops.Add(entry);
            return entry;
        }

        private void IgnoreCollisionWithExistingDrops(Collider2D newDropCollider)
        {
            if (newDropCollider == null || DropToDropCollisionResponseEnabled) return;
            for (var index = 0; index < drops.Count; index++)
            {
                var existingCollider = drops[index]?.Collider;
                if (existingCollider != null)
                    Physics2D.IgnoreCollision(newDropCollider, existingCollider, true);
            }
        }

        public static Vector2 CalculateLaunchDirection(int batchIndex, int batchCount)
        {
            if (batchCount <= 1) return Vector2.up;
            var normalized = Mathf.Clamp01(batchIndex / (batchCount - 1f));
            var angle = Mathf.Lerp(MaximumLaunchAngle, MinimumLaunchAngle, normalized) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        public static float CalculateLaunchSpeed(int batchCount) =>
            BaseLaunchSpeed + Mathf.Min(MaximumLaunchSpeedBonus,
                Mathf.Max(0, batchCount - 1) * LaunchSpeedPerExtraDrop);

        public static void IgnoreCollisionWithActiveDrops(WorldMobPhysicsBody mobBody)
        {
            if (mobBody == null) return;
            ActiveDropColliders.RemoveWhere(collider => collider == null);
            foreach (var dropCollider in ActiveDropColliders)
                mobBody.IgnoreCollisionWith(dropCollider);
        }

        private static void ConfigureDropVisual(SpriteRenderer renderer, Sprite sprite, float targetSize)
        {
            RuntimePlaceholderVisual.ConfigureSprite(renderer, sprite, 32);
            var maximumSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            var scale = maximumSize > Mathf.Epsilon ? targetSize / maximumSize : 1f;
            renderer.transform.localScale = Vector3.one * scale;
            var bounds = sprite.bounds;
            // Root carries the circle collider. Put sprite feet on the root origin so art
            // matches the grounded drop position instead of floating around the collider center.
            renderer.transform.localPosition = new Vector3(
                -bounds.center.x * scale,
                -bounds.min.y * scale + VisualSurfaceOffset,
                0f);
        }

        private static float ResolveGravityScale()
        {
            var gravity = Mathf.Abs(Physics2D.gravity.y);
            return gravity > Mathf.Epsilon ? Gravity / gravity : 0f;
        }

        private static bool IsFinite(Vector2 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

        public int ApplyOutdoorIceMelt(float meltPerDay, IReadOnlyList<int> surfaceHeights)
        {
            if (meltPerDay <= 0f || surfaceHeights == null || surfaceHeights.Count == 0) return 0;
            var melted = 0;
            for (var index = drops.Count - 1; index >= 0; index--)
            {
                var entry = drops[index];
                if (entry?.Root == null || entry.Item == null || entry.Amount <= 0 ||
                    !OutdoorIceMeltRules.IsIceItem(entry.Item.Id))
                    continue;
                if (!WorldExposureRules.TryIsSurfaceExposed(
                        entry.Root.transform.position, surfaceHeights, out var exposed) || !exposed)
                    continue;
                var wholeLoss = StorageTemperatureService.CalculateIceMelt(
                    entry.Amount, 0f, meltPerDay, out var remainingAmount, out _);
                entry.Amount = remainingAmount;
                melted += wholeLoss;
                if (entry.Amount <= 0)
                {
                    if (entry.Collider != null) ActiveDropColliders.Remove(entry.Collider);
                    Destroy(entry.Root);
                    drops.RemoveAt(index);
                }
            }
            return melted;
        }

        private void ClearDrops()
        {
            foreach (var entry in drops)
            {
                if (entry?.Collider != null) ActiveDropColliders.Remove(entry.Collider);
                if (entry?.Root != null)
                {
                    entry.Root.SetActive(false);
                    Destroy(entry.Root);
                }
            }
            drops.Clear();
        }

        private void OnDestroy()
        {
            ClearDrops();
            if (dropMaterial != null) Destroy(dropMaterial);
        }
    }
}
