using Nyangbingo.Core;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    public sealed class MainGameEffectPresenter : MonoBehaviour
    {
        [SerializeField] private GameplayArtCatalog artCatalog;
        [SerializeField] private Transform playerTransform;
        private RuntimeOneShotSpriteEffect napEffect;
        private RuntimeOneShotSpriteEffect miningEffect;
        private SpriteRenderer miningProgressRenderer;
        private System.Collections.Generic.IReadOnlyList<Sprite> miningProgressFrames;
        private Vector3Int miningProgressCell;

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
            text.fontSize = 28;
            text.characterSize = .04f;
            text.color = critical ? new Color(1f, .82f, .18f, 1f) : Color.white;
            var renderer = text.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 28;
            popupObject.AddComponent<RuntimeFloatingWorldText>().Configure(text, .8f, .65f);
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
            GameEvents.OnTileBroken -= HandleTileBroken;
            GameEvents.OnMiningResult -= HandleMiningResult;
        }
    }

    public sealed class RuntimeFloatingWorldText : MonoBehaviour
    {
        private TextMesh text;
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
        }

        private void Update()
        {
            if (text == null) { Destroy(gameObject); return; }
            var delta = Time.unscaledDeltaTime;
            remaining = Mathf.Max(0f, remaining - delta);
            transform.position += Vector3.up * (risePerSecond * delta);
            var ratio = remaining / duration;
            text.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(ratio));
            if (remaining <= 0f) Destroy(gameObject);
        }
    }
}
