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

        public static event Action<ItemDefinition, int, Vector2> ReturnedTheftRequested;

        public static void RequestReturnedTheft(ItemDefinition item, int amount, Vector2 position)
        {
            if (item == null || amount <= 0) return;
            if (ReturnedTheftRequested != null) ReturnedTheftRequested.Invoke(item, amount, position);
            else Request(item, amount, position);
        }

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
            public InventorySlot StorageState;
            public GameObject Root;
            public Rigidbody2D Body;
            public Collider2D Collider;
            public float PickupDelay;
            public bool TheftProtected;
            public bool Escaping;
            public int EscapeRevision = -1;
            public int EscapeIndex;
            public readonly List<Vector2> EscapePath = new List<Vector2>();
        }

        public const float MagnetRadius = 1.5f;
        // Terrain visuals now share the logical Grid boundary. This remains as a named value
        // because vegetation and drop rendering use the same surface contract.
        public const float VisualSurfaceOffset = 0f;
        public const float DropColliderRadius = .22f;
        private const float EscapeGridStepTiles = .25f;
        private const float EscapeClearance = .02f;
        private const float EscapeSpeed = 4f;
        private const int MaximumEscapeSearchNodes = 8192;
        private int escapeWorldRevision;
        private static readonly Vector2Int[] EscapeDirections =
        {
            Vector2Int.up, Vector2Int.left, Vector2Int.right, Vector2Int.down
        };
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
        private readonly List<Collider2D> escapeOverlapResults = new List<Collider2D>();
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
            out ItemDefinition stolenItem, out int stolenAmount, float radius = 1f,
            Func<ItemDefinition, int, bool> acceptTheft = null)
        {
            stolenItem = null;
            stolenAmount = 0;
            if (!IsFinite(origin) || !IsFiniteNonNegative(radius)) return false;
            Entry nearest = null;
            var nearestIndex = -1;
            var nearestDistance = radius * radius;
            for (var index = 0; index < drops.Count; index++)
            {
                var entry = drops[index];
                if (entry?.Item == null || entry.Root == null || entry.Amount <= 0 || entry.TheftProtected || entry.Escaping) continue;
                var distance = ((Vector2)entry.Root.transform.position - origin).sqrMagnitude;
                if (distance > nearestDistance) continue;
                nearest = entry;
                nearestIndex = index;
                nearestDistance = distance;
            }
            if (nearest == null || nearestIndex < 0) return false;
            if (acceptTheft != null && !acceptTheft(nearest.Item, nearest.Amount)) return false;
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
                if (entry?.Item == null || entry.Root == null || entry.Amount <= 0 || entry.Escaping) continue;
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
            if (entry == null || entry.Escaping || index < 0 || index >= drops.Count ||
                drops[index] != entry || destination == null ||
                !destination.TryAddWithStorageState(entry.Item.Id, entry.Amount,
                    entry.StorageState.hasStorageCondition, entry.StorageState.EffectiveStorageCondition,
                    entry.StorageState.storageMeltRemainder))
                return false;

            if (entry.Collider != null) ActiveDropColliders.Remove(entry.Collider);
            Destroy(entry.Root);
            drops.RemoveAt(index);
            if (notifyPlayerAcquisition)
            {
                GameEvents.RaiseItemAcquired();
                GameEvents.RaiseWorldItemPickedUp(entry.Item, entry.Amount,
                    player != null ? (Vector2)player.position : Vector2.zero);
            }
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
                    hasStorageCondition = entry.StorageState.hasStorageCondition,
                    storageCondition01 = entry.StorageState.storageCondition01,
                    storageMeltRemainder = entry.StorageState.storageMeltRemainder,
                    position = entry.Root.transform.position,
                    velocity = entry.Body != null ? entry.Body.linearVelocity : Vector2.zero,
                    pickupDelay = entry.PickupDelay,
                    theftProtected = entry.TheftProtected,
                    escapingTiles = entry.Escaping
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
                    !IsFinite(record.velocity) || !IsFiniteNonNegative(record.pickupDelay) ||
                    !IsFiniteNonNegative(record.storageCondition01) || record.storageCondition01 > 1f ||
                    !IsFiniteNonNegative(record.storageMeltRemainder) || record.storageMeltRemainder >= 1f)
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
                entry.StorageState = new InventorySlot { itemId = pair.record.itemId, amount = pair.record.amount,
                    hasStorageCondition = pair.record.hasStorageCondition,
                    storageCondition01 = pair.record.storageCondition01,
                    storageMeltRemainder = pair.record.storageMeltRemainder };
                entry.Root.transform.position = pair.record.position;
                entry.PickupDelay = pair.record.pickupDelay;
                entry.TheftProtected = pair.record.theftProtected;
                if (entry.Body != null) entry.Body.linearVelocity = pair.record.velocity;
                if (entry.Body != null) entry.Body.position = pair.record.position;
                if (tileService != null && HasPhysicalObstruction(entry, pair.record.position, -.01f))
                    BeginEscape(entry);
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

        private void OnEnable()
        {
            WorldItemDropRequest.Requested += Spawn;
            WorldItemDropRequest.ReturnedTheftRequested += SpawnReturnedTheft;
            GameEvents.OnTilePlaced += HandleTilePlaced;
            GameEvents.OnTileBroken += HandleEscapeTileBroken;
            GameEvents.OnSealChanged += HandleEscapeSealChanged;
        }
        private void OnDisable()
        {
            WorldItemDropRequest.Requested -= Spawn;
            WorldItemDropRequest.ReturnedTheftRequested -= SpawnReturnedTheft;
            GameEvents.OnTilePlaced -= HandleTilePlaced;
            GameEvents.OnTileBroken -= HandleEscapeTileBroken;
            GameEvents.OnSealChanged -= HandleEscapeSealChanged;
        }

        private void HandleTilePlaced(Vector3Int cell)
        {
            escapeWorldRevision++;
            if (tileService == null) return;
            Physics2D.SyncTransforms();
            var bounds = tileService.GetCellWorldBounds(cell);
            foreach (var entry in drops)
            {
                if (entry?.Root == null || entry.Escaping) continue;
                if (DropOverlapsCell(entry.Root.transform.position, bounds) &&
                    HasPhysicalObstruction(entry, entry.Root.transform.position, -.01f)) BeginEscape(entry);
            }
        }

        private void HandleEscapeTileBroken(Vector3Int cell) => escapeWorldRevision++;
        private void HandleEscapeSealChanged() => escapeWorldRevision++;

        private void BeginEscape(Entry entry)
        {
            entry.Escaping = true;
            entry.EscapeRevision = -1;
            entry.EscapePath.Clear();
            if (entry.Body == null) return;
            entry.Body.linearVelocity = Vector2.zero;
            entry.Body.interpolation = RigidbodyInterpolation2D.None;
            // 겹침을 물리 엔진이 강제로 해소하지 않도록 탈출 중에는 경로 이동만 적용한다.
            entry.Body.simulated = false;
        }

        private void RebuildEscapePath(Entry entry)
        {
            entry.EscapePath.Clear();
            entry.EscapeIndex = 0;
            entry.EscapeRevision = escapeWorldRevision;
            var origin = (Vector2)entry.Root.transform.position;
            var cellSize = tileService.GetCellWorldBounds(tileService.WorldToCell(origin)).size;
            var stepSize = new Vector2(cellSize.x, cellSize.y) * EscapeGridStepTiles;
            var queue = new Queue<Vector2Int>();
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            queue.Enqueue(Vector2Int.zero);
            previous.Add(Vector2Int.zero, Vector2Int.zero);
            // 이미 매몰된 상태에서 출발하므로 막힌 격자도 탐색한다. 첫 열린 격자가 최단 탈출점이다.
            while (queue.Count > 0 && previous.Count <= MaximumEscapeSearchNodes)
            {
                var node = queue.Dequeue();
                var point = origin + Vector2.Scale(node, stepSize);
                if (IsEscapeDestinationClear(entry, point))
                {
                    while (node != Vector2Int.zero)
                    {
                        entry.EscapePath.Add(origin + Vector2.Scale(node, stepSize));
                        node = previous[node];
                    }
                    entry.EscapePath.Reverse();
                    return;
                }
                foreach (var direction in EscapeDirections)
                {
                    var next = node + direction;
                    if (previous.ContainsKey(next)) continue;
                    var nextPoint = origin + Vector2.Scale(next, stepSize);
                    if (!tileService.InBounds(tileService.WorldToCell(nextPoint))) continue;
                    previous.Add(next, node);
                    queue.Enqueue(next);
                }
            }
            // 출구를 못 찾은 경우 제자리에서 보존한다. 지형 변경 때만 다시 탐색한다.
        }

        private void TickEscape(Entry entry, float deltaSeconds)
        {
            if (!HasPhysicalObstruction(entry, entry.Root.transform.position, -.01f))
            {
                entry.Escaping = false;
                entry.EscapePath.Clear();
                if (entry.Body != null)
                {
                    entry.Body.position = entry.Root.transform.position;
                    entry.Body.gravityScale = ResolveGravityScale();
                    entry.Body.simulated = true;
                    entry.Body.linearVelocity = Vector2.zero;
                    entry.Body.interpolation = RigidbodyInterpolation2D.Interpolate;
                }
                return;
            }
            if (entry.EscapeRevision != escapeWorldRevision) RebuildEscapePath(entry);
            var budget = EscapeSpeed * Mathf.Min(deltaSeconds, .05f);
            while (budget > 0f && entry.EscapeIndex < entry.EscapePath.Count)
            {
                var current = (Vector2)entry.Root.transform.position;
                var next = Vector2.MoveTowards(current, entry.EscapePath[entry.EscapeIndex], budget);
                budget -= Vector2.Distance(current, next);
                var z = entry.Root.transform.position.z;
                entry.Root.transform.position = new Vector3(next.x, next.y, z);
                if (entry.Body != null) entry.Body.position = next;
                if ((next - entry.EscapePath[entry.EscapeIndex]).sqrMagnitude > .000001f) break;
                entry.EscapeIndex++;
            }
        }

        private bool IsEscapeDestinationClear(Entry entry, Vector2 position)
        {
            var radius = DropColliderRadius + EscapeClearance;
            return tileService.InBounds(tileService.WorldToCell(position - Vector2.one * radius)) &&
                   tileService.InBounds(tileService.WorldToCell(position + Vector2.one * radius)) &&
                   !HasPhysicalObstruction(entry, position, EscapeClearance);
        }

        private bool HasPhysicalObstruction(Entry entry, Vector2 position, float clearance)
        {
            if (entry.Collider == null || !entry.Collider.enabled) return false;
            var filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = Physics2D.GetLayerCollisionMask(entry.Collider.gameObject.layer)
            };
            escapeOverlapResults.Clear();
            Physics2D.OverlapCircle(position, DropColliderRadius + clearance, filter, escapeOverlapResults);
            foreach (var obstacle in escapeOverlapResults)
            {
                if (obstacle == null || obstacle == entry.Collider || !obstacle.enabled || obstacle.isTrigger ||
                    ActiveDropColliders.Contains(obstacle) || Array.IndexOf(playerColliders, obstacle) >= 0 ||
                    obstacle.GetComponentInParent<WorldMobPhysicsBody>() != null ||
                    Physics2D.GetIgnoreCollision(entry.Collider, obstacle)) continue;
                return true;
            }
            return false;
        }

        private static bool DropOverlapsCell(Vector2 center, Bounds bounds, float radius = DropColliderRadius)
        {
            var closest = new Vector2(Mathf.Clamp(center.x, bounds.min.x, bounds.max.x),
                Mathf.Clamp(center.y, bounds.min.y, bounds.max.y));
            return (center - closest).sqrMagnitude < radius * radius;
        }

        private void SpawnReturnedTheft(ItemDefinition item, int amount, Vector2 position)
        {
            if (item == null || amount <= 0) return;
            for (var index = 0; index < amount; index++)
            {
                var entry = SpawnSingle(item, position, index, amount, null);
                if (entry != null) entry.TheftProtected = true;
            }
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;
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

                if (tileService != null)
                {
                    if (!entry.Escaping && HasPhysicalObstruction(entry, entry.Root.transform.position, -.01f))
                        BeginEscape(entry);
                    if (entry.Escaping)
                    {
                        TickEscape(entry, Time.deltaTime);
                        continue;
                    }
                }
                if (player == null || inventory == null) continue;
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
                if (!inventory.TryAddWithStorageState(entry.Item.Id, entry.Amount,
                        entry.StorageState.hasStorageCondition, entry.StorageState.EffectiveStorageCondition,
                        entry.StorageState.storageMeltRemainder)) continue;
                acquiredAny = true;
                if (entry.Collider != null) ActiveDropColliders.Remove(entry.Collider);
                Destroy(entry.Root);
                drops.RemoveAt(index);
                GameEvents.RaiseWorldItemPickedUp(entry.Item, entry.Amount, player.position);
            }
            if (acquiredAny) GameEvents.RaiseItemAcquired();
        }

        public void SpawnStoredStack(ItemDefinition item, InventorySlot slot, Vector2 position,
            int batchIndex, int batchCount)
        {
            if (item == null || slot.amount <= 0) return;
            // One physical drop per stored stack, rather than one per unit in a large stack.
            var entry = SpawnSingle(item, position, batchIndex, batchCount, null);
            entry.Amount = slot.amount;
            entry.StorageState = slot;
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
                    entry.Amount, entry.StorageState.storageMeltRemainder, meltPerDay,
                    out var remainingAmount, out var remainder);
                entry.Amount = remainingAmount;
                entry.StorageState.storageMeltRemainder = remainder;
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
