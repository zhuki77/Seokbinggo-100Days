using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Combat;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    [DefaultExecutionOrder(20)]
    public sealed class MainGameEffectPresenter : MonoBehaviour
    {
        internal const int WorldPopupFontSize = 64;
        internal const float WorldPopupCharacterSize = .12f;
        public const float PlayerFireHitHeadOffset = .65f;
        public const float PlayerFireHitDurationSeconds = .8f;
        public const int PlayerFireHitFallbackSortingOrder = 19;
        private static int suppressPlayerFireHitEffectDepth;
        public const float WallDurabilityLabelSeconds = 1.25f;
        public const float WallDurabilityLabelHeight = .72f;

        private sealed class WallDurabilityLabel
        {
            public GameObject Root;
            public TextMesh Text;
            public TextMesh Shadow;
            public float HideAt;
        }
        [SerializeField] private GameplayArtCatalog artCatalog;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private MainGameBootstrap bootstrap;
        private RuntimeOneShotSpriteEffect napEffect;
        private RuntimeOneShotSpriteEffect miningEffect;
        private RuntimeOneShotSpriteEffect miningBreakEffect;
        private RuntimeOneShotSpriteEffect miningCriticalEffect;
        private RuntimeOneShotSpriteEffect playerFireHitEffect;
        private Health playerHealth;
        private SpriteRenderer miningProgressRenderer;
        private SpriteRenderer miningTargetRenderer;
        private System.Collections.Generic.IReadOnlyList<Sprite> miningProgressFrames;
        private Vector3Int miningProgressCell;
        private SpriteRenderer miningTargetRenderer;
        private Sprite miningTargetSprite;
        private MiningImpactSurface lastMiningSurface = MiningImpactSurface.Mineral;
        private TilemapRenderer worldRenderer;
        private readonly Dictionary<Vector3Int, WallDurabilityLabel> wallDurabilityLabels =
            new Dictionary<Vector3Int, WallDurabilityLabel>();
        private readonly List<Vector3Int> expiredWallDurabilityCells =
            new List<Vector3Int>();

        public void ConfigureForScene(GameplayArtCatalog catalog, Transform player)
        {
            artCatalog = catalog;
            playerTransform = player;
            bootstrap ??= GetComponent<MainGameBootstrap>() ?? GetComponentInParent<MainGameBootstrap>();
        }

        private void Start()
        {
            bootstrap ??= GetComponent<MainGameBootstrap>() ?? GetComponentInParent<MainGameBootstrap>();
            if (artCatalog == null || playerTransform == null) return;
            worldRenderer = FindAnyObjectByType<TilemapRenderer>();
            miningEffect = CreateEffect("MiningCrackEffect", transform, artCatalog.MiningCrackFrames, 24);
            miningBreakEffect = CreateEffect("MiningBreakEffect", transform, artCatalog.MiningBreakFrames, 29);
            miningCriticalEffect = CreateEffect("MiningCriticalEffect", transform,
                artCatalog.MiningCriticalFrames, 30);
            var playerAnimator = playerTransform.GetComponentInChildren<RuntimeCharacterSpriteAnimator>();
            var playerRenderer = playerAnimator?.Renderer ??
                                 playerTransform.GetComponentInChildren<SpriteRenderer>();
            var playerVisualTransform = playerRenderer != null
                ? playerRenderer.transform
                : playerTransform;
            var fireSortingOrder = playerRenderer != null
                ? playerRenderer.sortingOrder - 1
                : PlayerFireHitFallbackSortingOrder;
            playerFireHitEffect = CreateEffect(
                "PlayerFireHitEffect", playerVisualTransform, artCatalog.PlayerFireHitFrames,
                fireSortingOrder);
            var fireRenderer = playerFireHitEffect.GetComponent<SpriteRenderer>();
            if (playerRenderer != null && fireRenderer != null)
                fireRenderer.sortingLayerID = playerRenderer.sortingLayerID;
            playerFireHitEffect.transform.localPosition =
                Vector3.up * PlayerFireHitHeadOffset;
            playerHealth = playerTransform.GetComponent<Health>();
            if (playerHealth != null) playerHealth.Damaged += HandlePlayerDamaged;
            var progressObject = new GameObject("MiningProgressOverlay");
            progressObject.transform.SetParent(transform, false);
            miningProgressRenderer = progressObject.AddComponent<SpriteRenderer>();
            miningProgressRenderer.sortingOrder = 25;
            miningProgressRenderer.enabled = false;
            miningProgressFrames = artCatalog.MiningCrackFrames;
            var targetObject = new GameObject("MiningTargetOverlay");
            targetObject.transform.SetParent(transform, false);
            miningTargetRenderer = targetObject.AddComponent<SpriteRenderer>();
            miningTargetSprite = CreateMiningTargetSprite();
            miningTargetRenderer.sprite = miningTargetSprite;
            miningTargetRenderer.color = new Color(1f, .92f, .35f, .9f);
            miningTargetRenderer.sortingOrder = 23;
            miningTargetRenderer.enabled = false;
            GameEvents.OnNapStarted += HandleNapStarted;
            RuntimePlaceholderVisual.Configure(
                miningTargetRenderer, new Color(.2f, .9f, 1f, .18f), 1.02f, 24);
            miningTargetRenderer.enabled = false;
            GameEvents.OnMiningTargetChanged += HandleMiningTargetChanged;
            GameEvents.OnMiningProgress += HandleMiningProgress;
            GameEvents.OnMiningImpact += HandleMiningImpact;
            GameEvents.OnTileBroken += HandleTileBroken;
            GameEvents.OnMiningResult += HandleMiningResult;
            GameEvents.OnMiningTarget += HandleMiningTarget;
            GameEvents.OnWallDurabilityChanged += HandleWallDurabilityChanged;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (playerTransform == null || !Input.GetKeyDown(KeyCode.F10)) return;
            var cell = Vector3Int.FloorToInt(playerTransform.position + Vector3.right);
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                HandleMiningResult(cell, "철광석", 2, true);
                Debug.Log("[Nyangbingo] Ctrl+F10 mining critical VFX preview.");
            }
            else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                HandleTileBroken(cell);
                Debug.Log("[Nyangbingo] Shift+F10 mining break VFX preview.");
            }
        }
#endif

        private void HandleMiningImpact(MiningImpactSurface surface) => lastMiningSurface = surface;

        private void HandleMiningTarget(bool visible, Vector3Int cell)
        {
            if (miningTargetRenderer == null) return;
            if (!visible)
            {
                miningTargetRenderer.enabled = false;
                return;
            }

            miningTargetRenderer.transform.position =
                ResolveSpriteCenteredOnCell(cell, miningTargetRenderer.sprite);
            miningTargetRenderer.enabled = true;
        private void HandleMiningTargetChanged(Vector3Int cell, bool visible, bool mineable)
        {
            if (miningTargetRenderer == null) return;
            miningTargetRenderer.enabled = visible;
            if (!visible) return;
            miningTargetRenderer.transform.position = CellCenter(cell);
            miningTargetRenderer.color = mineable
                ? new Color(.2f, .9f, 1f, .18f)
                : new Color(1f, .2f, .2f, .24f);
        }

        private void HandlePlayerDamaged(DamageTag tag, int amount)
        {
            if (amount <= 0) return;
            CreatePlayerDamagePopup(amount);
            if (tag == DamageTag.Fire && suppressPlayerFireHitEffectDepth <= 0 &&
                playerFireHitEffect != null)
                playerFireHitEffect.Play(PlayerFireHitDurationSeconds);
        }

        public static void BeginSuppressPlayerFireHitEffect() =>
            suppressPlayerFireHitEffectDepth++;

        public static void EndSuppressPlayerFireHitEffect() =>
            suppressPlayerFireHitEffectDepth = Mathf.Max(0, suppressPlayerFireHitEffectDepth - 1);

        private void CreatePlayerDamagePopup(int amount)
        {
            if (playerTransform == null || amount <= 0) return;
            var popup = new GameObject("PlayerDamagePopup");
            popup.transform.position = playerTransform.position + Vector3.up * .75f;
            var text = popup.AddComponent<TextMesh>();
            text.text = $"-{amount}";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = WorldPopupFontSize;
            text.characterSize = WorldPopupCharacterSize;
            text.color = new Color(1f, .22f, .18f);
            text.GetComponent<MeshRenderer>().sortingOrder = 40;
            popup.AddComponent<RuntimeFloatingWorldText>().Configure(text, .65f, .8f);
        }

        private void HandleMiningProgress(Vector3Int cell, float normalizedProgress)
        {
            if (miningProgressRenderer == null || miningProgressFrames == null ||
                miningProgressFrames.Count == 0 || normalizedProgress <= 0f)
            {
                if (miningProgressRenderer != null) miningProgressRenderer.enabled = false;
                return;
            }

            miningProgressCell = cell;
            miningProgressRenderer.transform.position = CellVisualAnchor(cell);
            var frameIndex = Mathf.Clamp(
                Mathf.CeilToInt(Mathf.Clamp01(normalizedProgress) * miningProgressFrames.Count) - 1,
                0, miningProgressFrames.Count - 1);
            var frame = miningProgressFrames[frameIndex];
            miningProgressRenderer.sprite = frame;
            // 피벗이 하단이든 중앙이든, 스프라이트 기하 중심을 셀 중심에 맞춘다.
            miningProgressRenderer.transform.position = ResolveSpriteCenteredOnCell(cell, frame);
            miningProgressRenderer.enabled = true;
        }

        private void HandleTileBroken(Vector3Int cell)
        {
            RemoveWallDurabilityLabel(cell);
            if (miningProgressRenderer != null && miningProgressRenderer.enabled && miningProgressCell == cell)
                miningProgressRenderer.enabled = false;
            if (miningEffect == null) return;
            PlaceEffectOnCell(miningEffect, cell);
            miningEffect.Play(.35f);
            if (miningBreakEffect != null && artCatalog.MiningBreakFrames.Count > 0)
            {
                PlaceEffectOnCell(miningBreakEffect, cell);
            var visualAnchor = CellVisualAnchor(cell);
            miningEffect.transform.position = visualAnchor;
            miningEffect.Play(.35f);
            if (miningBreakEffect != null && artCatalog.MiningBreakFrames.Count > 0)
            {
                miningBreakEffect.transform.position = visualAnchor;
                miningBreakEffect.Play(.2f);
            }
            else RuntimeTileDebrisBurst.Create(transform, CellCenter(cell), lastMiningSurface);
        }

        private void HandleWallDurabilityChanged(
            Vector3Int cell, float current, float maximum, bool destroyed)
        {
            if (maximum <= 0f || float.IsNaN(maximum) || float.IsInfinity(maximum)) return;
            if (!wallDurabilityLabels.TryGetValue(cell, out var label) || label?.Root == null)
            {
                label = CreateWallDurabilityLabel(cell);
                wallDurabilityLabels[cell] = label;
            }

            label.Root.transform.position = CellCenter(cell) + Vector3.up * WallDurabilityLabelHeight;
            var currentValue = Mathf.Clamp(Mathf.CeilToInt(current), 0, Mathf.CeilToInt(maximum));
            var maximumValue = Mathf.Max(1, Mathf.CeilToInt(maximum));
            var value = $"{currentValue} / {maximumValue}";
            label.Text.text = value;
            label.Shadow.text = value;
            var ratio = Mathf.Clamp01(current / maximum);
            label.Text.color = Color.Lerp(
                new Color(1f, .22f, .16f, 1f),
                new Color(.35f, 1f, .55f, 1f),
                ratio);
            label.HideAt = Time.unscaledTime +
                           (destroyed ? .75f : WallDurabilityLabelSeconds);
            label.Root.SetActive(true);
        }

        private WallDurabilityLabel CreateWallDurabilityLabel(Vector3Int cell)
        {
            var root = new GameObject($"WallDurability_{cell.x}_{cell.y}");
            root.transform.SetParent(transform, false);
            var text = root.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = .08f;
            text.fontStyle = FontStyle.Bold;
            var renderer = text.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 42;

            var shadowObject = new GameObject("Shadow");
            shadowObject.transform.SetParent(root.transform, false);
            shadowObject.transform.localPosition = new Vector3(.02f, -.02f, .01f);
            var shadow = shadowObject.AddComponent<TextMesh>();
            shadow.anchor = TextAnchor.MiddleCenter;
            shadow.alignment = TextAlignment.Center;
            shadow.font = text.font;
            shadow.fontSize = text.fontSize;
            shadow.characterSize = text.characterSize;
            shadow.fontStyle = text.fontStyle;
            shadow.color = new Color(0f, 0f, 0f, .9f);
            var shadowRenderer = shadow.GetComponent<MeshRenderer>();
            if (shadowRenderer != null) shadowRenderer.sortingOrder = 41;

            return new WallDurabilityLabel
            {
                Root = root,
                Text = text,
                Shadow = shadow
            };
        }

        private void LateUpdate()
        {
            if (wallDurabilityLabels.Count == 0) return;
            expiredWallDurabilityCells.Clear();
            foreach (var pair in wallDurabilityLabels)
                if (pair.Value?.Root == null || Time.unscaledTime >= pair.Value.HideAt)
                    expiredWallDurabilityCells.Add(pair.Key);
            for (var index = 0; index < expiredWallDurabilityCells.Count; index++)
                RemoveWallDurabilityLabel(expiredWallDurabilityCells[index]);
        }

        private void RemoveWallDurabilityLabel(Vector3Int cell)
        {
            if (!wallDurabilityLabels.TryGetValue(cell, out var label)) return;
            wallDurabilityLabels.Remove(cell);
            if (label?.Root != null) Destroy(label.Root);
        }

        private void HandleMiningResult(Vector3Int cell, string itemName, int amount, bool critical)
        {
            if (string.IsNullOrWhiteSpace(itemName) || amount <= 0) return;
            var popupObject = new GameObject("MiningResultPopup");
            popupObject.transform.SetParent(transform, false);
            var center = ResolveCellCenter(cell);
            popupObject.transform.position = center + new Vector3(0f, .35f, 0f);
            popupObject.transform.position = CellCenter(cell) + Vector3.up * .35f;
            var text = popupObject.AddComponent<TextMesh>();
            text.text = $"+{itemName} ×{amount}";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = WorldPopupFontSize;
            text.characterSize = WorldPopupCharacterSize;
            text.fontStyle = FontStyle.Bold;
            text.color = critical ? new Color(1f, .82f, .18f, 1f) : Color.white;
            var renderer = text.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 28;
            popupObject.AddComponent<RuntimeFloatingWorldText>().Configure(text, .8f, .65f);
            if (!critical) return;
            if (miningCriticalEffect != null && artCatalog.MiningCriticalFrames.Count > 0)
            {
                PlaceEffectOnCell(miningCriticalEffect, cell);
                miningCriticalEffect.transform.position = CellVisualAnchor(cell);
                miningCriticalEffect.Play(.3f);
            }
            else RuntimeMiningCriticalSparkle.Create(transform, CellCenter(cell));
        }

        private void PlaceEffectOnCell(RuntimeOneShotSpriteEffect effect, Vector3Int cell)
        {
            if (effect == null) return;
            var spriteRenderer = effect.GetComponent<SpriteRenderer>();
            effect.transform.position = ResolveSpriteCenteredOnCell(cell, spriteRenderer != null
                ? spriteRenderer.sprite
                : null);
        }

        private Vector3 ResolveCellCenter(Vector3Int cell) => bootstrap?.WorldRenderer != null
            ? bootstrap.WorldRenderer.GetTileVisualCenterWorld(cell)
            : new Vector3(cell.x + .5f, cell.y + 1f, 0f);

        /// <summary>
        /// transform.position은 스프라이트 피벗이다. bounds.center만큼 보정해야
        /// 그림의 기하 중심이 보이는 타일 중심에 온다.
        /// </summary>
        private Vector3 ResolveSpriteCenteredOnCell(Vector3Int cell, Sprite sprite)
        {
            var center = ResolveCellCenter(cell);
            if (sprite == null) return center;
            return center - (Vector3)sprite.bounds.center;
        }
        private Vector3 CellCenter(Vector3Int cell) => worldRenderer != null
            ? worldRenderer.GetCellCenterWorld(cell)
            : new Vector3(cell.x + .5f, cell.y + .5f, cell.z);

        private Vector3 CellVisualAnchor(Vector3Int cell) => worldRenderer != null
            ? worldRenderer.GetCellVisualAnchorWorld(cell)
            : new Vector3(cell.x + .5f, cell.y, cell.z);

        private static RuntimeOneShotSpriteEffect CreateEffect(string name, Transform parent,
            System.Collections.Generic.IReadOnlyList<Sprite> frames, int sortingOrder)
        {
            var effectObject = new GameObject(name);
            effectObject.transform.SetParent(parent, false);
            effectObject.AddComponent<SpriteRenderer>();
            var effect = effectObject.AddComponent<RuntimeOneShotSpriteEffect>();
            effect.Configure(frames, sortingOrder);
            return effect;
        }

        private static Sprite CreateMiningTargetSprite()
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "MiningTargetTex"
            };
            var clear = new Color(1f, 1f, 1f, .12f);
            var edge = Color.white;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var border = x <= 1 || y <= 1 || x >= size - 2 || y >= size - 2;
                texture.SetPixel(x, y, border ? edge : clear);
            }

            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
        }

        private void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Damaged -= HandlePlayerDamaged;
            GameEvents.OnMiningTargetChanged -= HandleMiningTargetChanged;
            GameEvents.OnMiningProgress -= HandleMiningProgress;
            GameEvents.OnMiningImpact -= HandleMiningImpact;
            GameEvents.OnTileBroken -= HandleTileBroken;
            GameEvents.OnMiningResult -= HandleMiningResult;
            GameEvents.OnMiningTarget -= HandleMiningTarget;
            if (miningTargetSprite != null)
            {
                if (miningTargetSprite.texture != null) Destroy(miningTargetSprite.texture);
                Destroy(miningTargetSprite);
            }
            GameEvents.OnWallDurabilityChanged -= HandleWallDurabilityChanged;
            wallDurabilityLabels.Clear();
        }
    }

    internal sealed class RuntimeWorldDamagePopup : MonoBehaviour
    {
        private Health health;

        private void Awake() => health = GetComponent<Health>() ?? GetComponentInParent<Health>();

        private void OnEnable()
        {
            health ??= GetComponent<Health>() ?? GetComponentInParent<Health>();
            if (health != null) health.Damaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= HandleDamaged;
        }

        private void HandleDamaged(DamageTag tag, int amount)
        {
            if (amount <= 0) return;
            var popup = new GameObject("DamagePopup");
            popup.transform.position = transform.position + Vector3.up * .75f;
            var text = popup.AddComponent<TextMesh>();
            text.text = $"-{amount}";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = MainGameEffectPresenter.WorldPopupFontSize;
            text.characterSize = MainGameEffectPresenter.WorldPopupCharacterSize;
            text.fontStyle = FontStyle.Bold;
            text.color = tag == DamageTag.Fire
                ? new Color(1f, .42f, .15f, 1f)
                : tag == DamageTag.Ice
                    ? new Color(.4f, .9f, 1f, 1f)
                    : Color.white;
            var renderer = text.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 31;
            popup.AddComponent<RuntimeFloatingWorldText>().Configure(text, .65f, .8f);
        }
    }

    internal sealed class RuntimeMiningCriticalSparkle : MonoBehaviour
    {
        private readonly System.Collections.Generic.List<SpriteRenderer> renderers =
            new System.Collections.Generic.List<SpriteRenderer>();
        private float remaining = .5f;

        public static void Create(Transform parent, Vector3 worldPosition)
        {
            var root = new GameObject("MiningCriticalSparkle");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPosition;
            var effect = root.AddComponent<RuntimeMiningCriticalSparkle>();
            effect.Build();
        }

        private void Build()
        {
            var directions = new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
            for (var index = 0; index < directions.Length; index++)
            {
                var piece = new GameObject($"Spark_{index}");
                piece.transform.SetParent(transform, false);
                piece.transform.localPosition = directions[index] * .18f;
                piece.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var renderer = piece.AddComponent<SpriteRenderer>();
                RuntimePlaceholderVisual.Configure(renderer, new Color(1f, .86f, .18f, 1f), .11f, 30);
                renderers.Add(renderer);
            }
        }

        private void Update()
        {
            remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
            var ratio = remaining / .5f;
            for (var index = 0; index < renderers.Count; index++)
            {
                var renderer = renderers[index];
                if (renderer == null) continue;
                var direction = index == 0 ? Vector2.up : index == 1 ? Vector2.right :
                    index == 2 ? Vector2.down : Vector2.left;
                renderer.transform.localPosition += (Vector3)(direction * (.8f * Time.unscaledDeltaTime));
                var color = renderer.color;
                color.a = ratio;
                renderer.color = color;
            }
            if (remaining <= 0f) Destroy(gameObject);
        }
    }

    internal sealed class RuntimeTileDebrisBurst : MonoBehaviour
    {
        private sealed class Piece
        {
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Spin;
        }

        private readonly System.Collections.Generic.List<Piece> pieces =
            new System.Collections.Generic.List<Piece>();
        private float remaining = .45f;

        public static void Create(Transform parent, Vector3 worldPosition, MiningImpactSurface surface)
        {
            var root = new GameObject("TileDebrisBurst");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPosition;
            var effect = root.AddComponent<RuntimeTileDebrisBurst>();
            effect.Build(surface);
        }

        private void Build(MiningImpactSurface surface)
        {
            var color = surface == MiningImpactSurface.Dirt
                ? new Color(.5f, .34f, .2f, 1f)
                : new Color(.55f, .7f, .82f, 1f);
            for (var index = 0; index < 6; index++)
            {
                var radians = Mathf.Lerp(25f, 155f, index / 5f) * Mathf.Deg2Rad;
                var pieceObject = new GameObject($"Debris_{index}");
                pieceObject.transform.SetParent(transform, false);
                var renderer = pieceObject.AddComponent<SpriteRenderer>();
                RuntimePlaceholderVisual.Configure(renderer, color, index % 2 == 0 ? .09f : .065f, 29);
                pieces.Add(new Piece
                {
                    Renderer = renderer,
                    Velocity = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * (1.2f + index * .12f),
                    Spin = index % 2 == 0 ? 240f : -240f
                });
            }
        }

        private void Update()
        {
            var delta = Time.unscaledDeltaTime;
            remaining = Mathf.Max(0f, remaining - delta);
            var ratio = remaining / .45f;
            foreach (var piece in pieces)
            {
                if (piece.Renderer == null) continue;
                piece.Velocity += Vector2.down * (4f * delta);
                piece.Renderer.transform.localPosition += (Vector3)(piece.Velocity * delta);
                piece.Renderer.transform.Rotate(0f, 0f, piece.Spin * delta);
                var color = piece.Renderer.color;
                color.a = ratio;
                piece.Renderer.color = color;
            }
            if (remaining <= 0f) Destroy(gameObject);
        }
    }

    public sealed class RuntimeFloatingWorldText : MonoBehaviour
    {
        private TextMesh text;
        private TextMesh shadow;
        private float duration;
        private float remaining;
        private float risePerSecond;
        private Color baseColor;

        public void Configure(TextMesh target, float lifetimeSeconds, float verticalSpeed)
        {
            text = target;
            duration = Mathf.Max(.01f, lifetimeSeconds);
            remaining = duration;
            risePerSecond = Mathf.Max(0f, verticalSpeed);
            baseColor = text != null ? text.color : Color.white;
            if (text != null) shadow = CreateShadow(text);
        }

        private void Update()
        {
            if (text == null) { Destroy(gameObject); return; }
            var delta = Time.unscaledDeltaTime;
            remaining = Mathf.Max(0f, remaining - delta);
            transform.position += Vector3.up * (risePerSecond * delta);
            var ratio = remaining / duration;
            text.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(ratio));
            if (shadow != null) shadow.color = new Color(0f, 0f, 0f, Mathf.Clamp01(ratio) * .85f);
            if (remaining <= 0f) Destroy(gameObject);
        }

        private static TextMesh CreateShadow(TextMesh source)
        {
            var shadowObject = new GameObject("TextShadow");
            shadowObject.transform.SetParent(source.transform, false);
            shadowObject.transform.localPosition = new Vector3(.025f, -.025f, .01f);
            var result = shadowObject.AddComponent<TextMesh>();
            result.text = source.text;
            result.font = source.font;
            result.fontSize = source.fontSize;
            result.fontStyle = source.fontStyle;
            result.characterSize = source.characterSize;
            result.anchor = source.anchor;
            result.alignment = source.alignment;
            result.color = new Color(0f, 0f, 0f, .85f);
            var sourceRenderer = source.GetComponent<MeshRenderer>();
            var shadowRenderer = result.GetComponent<MeshRenderer>();
            if (sourceRenderer != null && shadowRenderer != null)
                shadowRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
            return result;
        }
    }
}
