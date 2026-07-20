using System;
using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using UnityEngine;

namespace Nyangbingo.World
{
    public static class WorldItemDropRequest
    {
        public static event Action<ItemDefinition, int, Vector2> Requested;

        public static void Request(ItemDefinition item, int amount, Vector2 position)
        {
            if (item == null || amount <= 0) return;
            if (Requested == null)
            {
                ItemAcquisition.Request(item, amount);
                return;
            }
            Requested.Invoke(item, amount, position);
        }
    }

    public sealed class MainGameWorldDropRuntime : MonoBehaviour
    {
        private sealed class Entry
        {
            public ItemDefinition Item;
            public int Amount;
            public GameObject Root;
            public Vector2 Velocity;
            public float PickupDelay;
        }

        public const float MagnetRadius = 1.5f;
        private const float PickupRadius = .22f;
        private const float MagnetSpeed = 6f;
        private const float Gravity = 12f;
        private const float MaximumFallSpeed = 10f;
        private const float DropHalfExtent = .22f;
        private const float CollisionSkin = .001f;
        private const float InitialPickupDelay = .45f;
        private const float GroundFriction = 9f;

        private readonly List<Entry> drops = new List<Entry>();
        private Transform player;
        private Nyangbingo.Inventory.Inventory inventory;
        private ItemArtCatalog itemArtCatalog;
        private TileService tileService;
        private int spawnSequence;

        public int ActiveDropCount => drops.Count;

        public void ConfigureForRuntime(Transform playerTransform, Nyangbingo.Inventory.Inventory playerInventory,
            ItemArtCatalog artCatalog, TileService worldTileService)
        {
            player = playerTransform;
            inventory = playerInventory;
            itemArtCatalog = artCatalog;
            tileService = worldTileService;
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
                    drops.RemoveAt(index);
                    continue;
                }

                var delta = (Vector2)player.position - (Vector2)entry.Root.transform.position;
                entry.PickupDelay = Mathf.Max(0f, entry.PickupDelay - Time.deltaTime);
                var magnetActive = entry.PickupDelay <= 0f && delta.sqrMagnitude <= MagnetRadius * MagnetRadius;
                if (magnetActive && delta.sqrMagnitude > Mathf.Epsilon)
                    entry.Velocity = delta.normalized * MagnetSpeed;
                else
                    entry.Velocity.y = Mathf.Max(-MaximumFallSpeed,
                        entry.Velocity.y - Gravity * Time.deltaTime);
                MoveWithTileCollision(entry, Time.deltaTime, magnetActive);
                if (!magnetActive) continue;
                if (((Vector2)player.position - (Vector2)entry.Root.transform.position).sqrMagnitude >
                    PickupRadius * PickupRadius) continue;
                if (!inventory.TryAdd(entry.Item.Id, entry.Amount)) continue;
                acquiredAny = true;
                Destroy(entry.Root);
                drops.RemoveAt(index);
            }
            if (acquiredAny) GameEvents.RaiseItemAcquired();
        }

        private void Spawn(ItemDefinition item, int amount, Vector2 position)
        {
            if (item == null || amount <= 0) return;
            for (var index = 0; index < amount; index++) SpawnSingle(item, position);
        }

        private void SpawnSingle(ItemDefinition item, Vector2 position)
        {
            var root = new GameObject($"WorldDrop_{item.Id}");
            root.transform.SetParent(transform, false);
            var fanIndex = spawnSequence++ % 17;
            var angle = Mathf.Lerp(22.5f, 157.5f, fanIndex / 16f) * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            root.transform.position = position + direction * .08f;

            // Keep the simulated drop root at unit scale. Delivered Aseprite files use
            // different canvas sizes and pivots, so scaling the root would make the art
            // appear far away from the position used for collision and pickup checks.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            var sprite = itemArtCatalog?.FindSprite(item.Id);
            if (sprite != null)
            {
                RuntimePlaceholderVisual.ConfigureSprite(renderer, sprite, 32);
                var maximumSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
                var scale = maximumSize > Mathf.Epsilon ? .42f / maximumSize : 1f;
                visual.transform.localScale = Vector3.one * scale;
                visual.transform.localPosition = -(Vector3)sprite.bounds.center * scale;
            }
            else
                RuntimePlaceholderVisual.Configure(renderer, new Color(.85f, .92f, 1f, 1f), .42f, 32);

            drops.Add(new Entry
            {
                Item = item,
                Amount = 1,
                Root = root,
                Velocity = direction * (2.2f + fanIndex % 3 * .25f),
                PickupDelay = InitialPickupDelay
            });
        }

        private void MoveWithTileCollision(Entry entry, float deltaTime, bool magnetActive)
        {
            if (entry?.Root == null) return;
            var position = (Vector2)entry.Root.transform.position;
            if (tileService == null)
            {
                entry.Root.transform.position = position + entry.Velocity * deltaTime;
                return;
            }

            var targetX = ResolveHorizontal(position, entry.Velocity.x * deltaTime, out var hitWall);
            position.x = targetX;
            if (hitWall) entry.Velocity.x *= -.25f;
            var wasFalling = entry.Velocity.y <= 0f;
            var targetY = ResolveVertical(position, entry.Velocity.y * deltaTime, out var hitFloorOrCeiling);
            position.y = targetY;
            if (hitFloorOrCeiling)
                entry.Velocity.y = entry.Velocity.y < 0f ? 0f : -entry.Velocity.y * .2f;
            position.x = Mathf.Clamp(position.x, DropHalfExtent, tileService.Width - DropHalfExtent);
            position.y = Mathf.Clamp(position.y, DropHalfExtent, tileService.Height - DropHalfExtent);
            entry.Root.transform.position = position;
            var grounded = wasFalling && (hitFloorOrCeiling || HasGroundBelow(position));
            if (grounded && !magnetActive)
                entry.Velocity.x = Mathf.MoveTowards(entry.Velocity.x, 0f, GroundFriction * deltaTime);
        }

        private float ResolveHorizontal(Vector2 position, float displacement, out bool collided)
        {
            collided = false;
            if (Mathf.Abs(displacement) <= Mathf.Epsilon) return position.x;
            var targetX = position.x + displacement;
            var minY = Mathf.FloorToInt(position.y - DropHalfExtent + CollisionSkin);
            var maxY = Mathf.FloorToInt(position.y + DropHalfExtent - CollisionSkin);
            var direction = displacement > 0f ? 1 : -1;
            var startCell = Mathf.FloorToInt(position.x + direction * DropHalfExtent);
            var endCell = Mathf.FloorToInt(targetX + direction * DropHalfExtent);
            for (var x = startCell; direction > 0 ? x <= endCell : x >= endCell; x += direction)
            {
                var blocked = false;
                for (var y = minY; y <= maxY && !blocked; y++) blocked = IsSolidCell(x, y);
                if (!blocked) continue;
                collided = true;
                return direction > 0
                    ? x - DropHalfExtent - CollisionSkin
                    : x + 1f + DropHalfExtent + CollisionSkin;
            }
            return targetX;
        }

        private float ResolveVertical(Vector2 position, float displacement, out bool collided)
        {
            collided = false;
            if (Mathf.Abs(displacement) <= Mathf.Epsilon) return position.y;
            var targetY = position.y + displacement;
            var minX = Mathf.FloorToInt(position.x - DropHalfExtent + CollisionSkin);
            var maxX = Mathf.FloorToInt(position.x + DropHalfExtent - CollisionSkin);
            var direction = displacement > 0f ? 1 : -1;
            var startCell = Mathf.FloorToInt(position.y + direction * DropHalfExtent);
            var endCell = Mathf.FloorToInt(targetY + direction * DropHalfExtent);
            for (var y = startCell; direction > 0 ? y <= endCell : y >= endCell; y += direction)
            {
                var blocked = false;
                for (var x = minX; x <= maxX && !blocked; x++) blocked = IsSolidCell(x, y);
                if (!blocked) continue;
                collided = true;
                return direction > 0
                    ? y - DropHalfExtent - CollisionSkin
                    : y + 1f + DropHalfExtent + CollisionSkin;
            }
            return targetY;
        }

        private bool IsSolidCell(int x, int y)
        {
            if (tileService == null) return false;
            var cell = new Vector3Int(x, y, 0);
            return !tileService.InBounds(cell) || !tileService.GetTile(cell).IsAir;
        }

        private bool HasGroundBelow(Vector2 position)
        {
            var y = Mathf.FloorToInt(position.y - DropHalfExtent - CollisionSkin * 2f);
            var minX = Mathf.FloorToInt(position.x - DropHalfExtent + CollisionSkin);
            var maxX = Mathf.FloorToInt(position.x + DropHalfExtent - CollisionSkin);
            for (var x = minX; x <= maxX; x++)
                if (IsSolidCell(x, y)) return true;
            return false;
        }

        private void OnDestroy()
        {
            foreach (var entry in drops)
                if (entry?.Root != null) Destroy(entry.Root);
            drops.Clear();
        }
    }
}
