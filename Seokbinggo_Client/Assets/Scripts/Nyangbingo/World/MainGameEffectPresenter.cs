using Nyangbingo.Core;
using Nyangbingo.Combat;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    public sealed class MainGameEffectPresenter : MonoBehaviour
    {
        internal const int WorldPopupFontSize = 64;
        internal const float WorldPopupCharacterSize = .12f;
        [SerializeField] private GameplayArtCatalog artCatalog;
        [SerializeField] private Transform playerTransform;
        private RuntimeOneShotSpriteEffect napEffect;
        private RuntimeOneShotSpriteEffect miningEffect;
        private SpriteRenderer miningProgressRenderer;
        private System.Collections.Generic.IReadOnlyList<Sprite> miningProgressFrames;
        private Vector3Int miningProgressCell;
        private MiningImpactSurface lastMiningSurface = MiningImpactSurface.Mineral;

        public void ConfigureForScene(GameplayArtCatalog catalog, Transform player)
        {
            artCatalog = catalog;
            playerTransform = player;
        }

        private void Start()
        {
            if (artCatalog == null || playerTransform == null) return;
            napEffect = CreateEffect("NapEffect", playerTransform, artCatalog.NapFrames, 26);
            napEffect.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            miningEffect = CreateEffect("MiningCrackEffect", transform, artCatalog.MiningCrackFrames, 24);
            var progressObject = new GameObject("MiningProgressOverlay");
            progressObject.transform.SetParent(transform, false);
            miningProgressRenderer = progressObject.AddComponent<SpriteRenderer>();
            miningProgressRenderer.sortingOrder = 25;
            miningProgressRenderer.enabled = false;
            miningProgressFrames = artCatalog.MiningCrackFrames;
            GameEvents.OnNapStarted += HandleNapStarted;
            GameEvents.OnMiningProgress += HandleMiningProgress;
            GameEvents.OnMiningImpact += HandleMiningImpact;
            GameEvents.OnTileBroken += HandleTileBroken;
            GameEvents.OnMiningResult += HandleMiningResult;
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (playerTransform == null || !Input.GetKeyDown(KeyCode.F10)) return;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                var cell = Vector3Int.FloorToInt(playerTransform.position + Vector3.right);
                HandleTileBroken(cell);
                Debug.Log("[Nyangbingo] Shift+F10 mining crack VFX preview.");
            }
            else
            {
                HandleNapStarted();
                Debug.Log("[Nyangbingo] F10 nap VFX preview.");
            }
        }
#endif

        private void HandleNapStarted() => napEffect?.Play(1.2f);

        private void HandleMiningImpact(MiningImpactSurface surface) => lastMiningSurface = surface;

        private void HandleMiningProgress(Vector3Int cell, float normalizedProgress)
        {
            if (miningProgressRenderer == null || miningProgressFrames == null ||
                miningProgressFrames.Count == 0 || normalizedProgress <= 0f)
            {
                if (miningProgressRenderer != null) miningProgressRenderer.enabled = false;
                return;
            }

            miningProgressCell = cell;
            miningProgressRenderer.transform.position = new Vector3(cell.x + .5f, cell.y + .5f, 0f);
            var frameIndex = Mathf.Clamp(
                Mathf.CeilToInt(Mathf.Clamp01(normalizedProgress) * miningProgressFrames.Count) - 1,
                0, miningProgressFrames.Count - 1);
            miningProgressRenderer.sprite = miningProgressFrames[frameIndex];
            miningProgressRenderer.enabled = true;
        }

        private void HandleTileBroken(Vector3Int cell)
        {
            if (miningProgressRenderer != null && miningProgressRenderer.enabled && miningProgressCell == cell)
                miningProgressRenderer.enabled = false;
            if (miningEffect == null) return;
            miningEffect.transform.position = new Vector3(cell.x + .5f, cell.y + .5f, 0f);
            miningEffect.Play(.35f);
            RuntimeTileDebrisBurst.Create(transform, cell, lastMiningSurface);
        }

        private void HandleMiningResult(Vector3Int cell, string itemName, int amount, bool critical)
        {
            if (string.IsNullOrWhiteSpace(itemName) || amount <= 0) return;
            var popupObject = new GameObject("MiningResultPopup");
            popupObject.transform.SetParent(transform, false);
            popupObject.transform.position = new Vector3(cell.x + .5f, cell.y + .85f, 0f);
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
            if (critical) RuntimeMiningCriticalSparkle.Create(transform, cell);
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
            GameEvents.OnNapStarted -= HandleNapStarted;
            GameEvents.OnMiningProgress -= HandleMiningProgress;
            GameEvents.OnMiningImpact -= HandleMiningImpact;
            GameEvents.OnTileBroken -= HandleTileBroken;
            GameEvents.OnMiningResult -= HandleMiningResult;
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

        public static void Create(Transform parent, Vector3Int cell)
        {
            var root = new GameObject("MiningCriticalSparkle");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(cell.x + .5f, cell.y + .5f, 0f);
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

        public static void Create(Transform parent, Vector3Int cell, MiningImpactSurface surface)
        {
            var root = new GameObject("TileDebrisBurst");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(cell.x + .5f, cell.y + .5f, 0f);
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
