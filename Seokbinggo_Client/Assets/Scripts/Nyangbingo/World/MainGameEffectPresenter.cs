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
        private RuntimeOneShotSpriteEffect miningEffect;
        private RuntimeOneShotSpriteEffect miningBreakEffect;
        private RuntimeOneShotSpriteEffect miningCriticalEffect;
        private RuntimeOneShotSpriteEffect playerFireHitEffect;
        private Health playerHealth;
        private SpriteRenderer miningProgressRenderer;
        private SpriteRenderer miningTargetRenderer;
        private System.Collections.Generic.IReadOnlyList<Sprite> miningProgressFrames;
        private Vector3Int miningProgressCell;
        private MiningImpactSurface lastMiningSurface = MiningImpactSurface.Mineral;
        private TilemapRenderer worldRenderer;
        private readonly Dictionary<Vector3Int, WallDurabilityLabel> wallDurabilityLabels =
            new Dictionary<Vector3Int, WallDurabilityLabel>();
        private readonly List<Vector3Int> expiredWallDurabilityCells =
            new List<Vector3Int>();

        private TileService tileService;

        public void ConfigureForScene(GameplayArtCatalog catalog, Transform player, TileService tiles = null)
        {
            artCatalog = catalog;
            playerTransform = player;
            tileService = tiles;
        }

        private void Start()
        {
            if (artCatalog == null || playerTransform == null) return;
            tileService ??= FindAnyObjectByType<MainGameBootstrap>()?.TileService;
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
            RuntimePlaceholderVisual.Configure(
                miningTargetRenderer, new Color(.2f, .9f, 1f, .18f), 1.02f, 24);
            miningTargetRenderer.enabled = false;
            GameEvents.OnMiningTargetChanged += HandleMiningTargetChanged;
            GameEvents.OnMiningProgress += HandleMiningProgress;
            GameEvents.OnMiningImpact += HandleMiningImpact;
            GameEvents.OnTileBroken += HandleTileBroken;
            GameEvents.OnMiningResult += HandleMiningResult;
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

        private void HandleMiningTargetChanged(Vector3Int cell, bool visible, bool mineable)
        {
            if (miningTargetRenderer == null) return;
            miningTargetRenderer.enabled = visible;
            if (!visible) return;
            miningTargetRenderer.transform.position = CellVisualAnchor(cell);
            AlignMiningOverlayToCell(miningTargetRenderer, cell);
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
            miningProgressRenderer.sprite = miningProgressFrames[frameIndex];
            AlignMiningOverlayToCell(miningProgressRenderer, cell);
            miningProgressRenderer.enabled = true;
        }

        private void HandleTileBroken(Vector3Int cell)
        {
            RemoveWallDurabilityLabel(cell);
            if (miningProgressRenderer != null && miningProgressRenderer.enabled && miningProgressCell == cell)
                miningProgressRenderer.enabled = false;
            if (miningEffect == null) return;
            PlayMiningCellEffect(miningEffect, cell, .35f);
            if (miningBreakEffect != null && artCatalog.MiningBreakFrames.Count > 0)
                PlayMiningCellEffect(miningBreakEffect, cell, .2f);
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
                PlayMiningCellEffect(miningCriticalEffect, cell, .3f);
            else RuntimeMiningCriticalSparkle.Create(transform, CellCenter(cell));
        }

        private Vector3 CellCenter(Vector3Int cell) => worldRenderer != null
            ? worldRenderer.GetCellCenterWorld(cell)
            : new Vector3(cell.x + .5f, cell.y + .5f, cell.z);

        private Vector3 CellVisualAnchor(Vector3Int cell) => worldRenderer != null
            ? worldRenderer.GetCellVisualAnchorWorld(cell)
            : new Vector3(cell.x + .5f, cell.y, cell.z);

        private void AlignMiningOverlayToCell(SpriteRenderer renderer, Vector3Int cell)
        {
            if (renderer == null) return;
            if (tileService != null)
                tileService.AlignSpriteBoundsToCellBase(renderer, cell);
        }

        private void PlayMiningCellEffect(RuntimeOneShotSpriteEffect effect, Vector3Int cell, float duration)
        {
            if (effect == null) return;
            var renderer = effect.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.transform.position = CellVisualAnchor(cell);
                AlignMiningOverlayToCell(renderer, cell);
            }
            else effect.transform.position = CellVisualAnchor(cell);
            effect.Play(duration);
        }

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

        private void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Damaged -= HandlePlayerDamaged;
            GameEvents.OnMiningTargetChanged -= HandleMiningTargetChanged;
            GameEvents.OnMiningProgress -= HandleMiningProgress;
            GameEvents.OnMiningImpact -= HandleMiningImpact;
            GameEvents.OnTileBroken -= HandleTileBroken;
            GameEvents.OnMiningResult -= HandleMiningResult;
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
