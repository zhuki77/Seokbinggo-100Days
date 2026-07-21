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
            public Rigidbody2D Body;
            public float PickupDelay;
        }

        public const float MagnetRadius = 1.5f;
        private const float PickupRadius = .22f;
        private const float MagnetSpeed = 6f;
        private const float Gravity = 12f;
        private const float MaximumFallSpeed = 10f;
        private const float InitialPickupDelay = .45f;

        private readonly List<Entry> drops = new List<Entry>();
        private Transform player;
        private Nyangbingo.Inventory.Inventory inventory;
        private ItemArtCatalog itemArtCatalog;
        private Collider2D playerCollider;
        private PhysicsMaterial2D dropMaterial;
        private int spawnSequence;

        public int ActiveDropCount => drops.Count;

        public void ConfigureForRuntime(Transform playerTransform, Nyangbingo.Inventory.Inventory playerInventory,
            ItemArtCatalog artCatalog, TileService worldTileService)
        {
            player = playerTransform;
            inventory = playerInventory;
            itemArtCatalog = artCatalog;
            playerCollider = playerTransform != null ? playerTransform.GetComponent<Collider2D>() : null;
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

            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = ResolveGravityScale();
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearDamping = .8f;
            body.linearVelocity = direction * (2.2f + fanIndex % 3 * .25f);
            var dropCollider = root.AddComponent<CircleCollider2D>();
            dropCollider.radius = .22f;
            dropCollider.sharedMaterial = dropMaterial;
            if (playerCollider != null) Physics2D.IgnoreCollision(dropCollider, playerCollider, true);

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
                Body = body,
                PickupDelay = InitialPickupDelay
            });
        }

        private static float ResolveGravityScale()
        {
            var gravity = Mathf.Abs(Physics2D.gravity.y);
            return gravity > Mathf.Epsilon ? Gravity / gravity : 0f;
        }

        private void OnDestroy()
        {
            foreach (var entry in drops)
                if (entry?.Root != null) Destroy(entry.Root);
            drops.Clear();
            if (dropMaterial != null) Destroy(dropMaterial);
        }
    }
}
