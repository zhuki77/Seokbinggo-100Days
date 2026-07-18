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
            GameEvents.OnNapStarted += HandleNapStarted;
            GameEvents.OnTileBroken += HandleTileBroken;
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

        private void HandleTileBroken(Vector3Int cell)
        {
            if (miningEffect == null) return;
            miningEffect.transform.position = new Vector3(cell.x + .5f, cell.y + .5f, 0f);
            miningEffect.Play(.35f);
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
            GameEvents.OnTileBroken -= HandleTileBroken;
        }
    }
}
