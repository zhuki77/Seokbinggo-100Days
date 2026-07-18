using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    [RequireComponent(typeof(Camera))]
    public sealed class MainGameParallaxBackground : MonoBehaviour
    {
        [SerializeField] private EnvironmentArtCatalog artCatalog;
        [SerializeField] private float undergroundThreshold;
        private Camera targetCamera;
        private SpriteRenderer distantRenderer;
        private SpriteRenderer cloudRenderer;
        private SpriteRenderer undergroundRenderer;
        private bool? undergroundVisible;

        public bool HasConfiguredArt => artCatalog != null && artCatalog.DistantView != null &&
                                        artCatalog.Clouds != null && artCatalog.Underground != null;

        public void ConfigureForScene(EnvironmentArtCatalog catalog, float undergroundWorldY)
        {
            artCatalog = catalog;
            undergroundThreshold = undergroundWorldY;
        }

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            if (artCatalog == null || targetCamera == null) return;
            distantRenderer = CreateLayer("DistantView", artCatalog.DistantView, -200, 20f);
            cloudRenderer = CreateLayer("Clouds", artCatalog.Clouds, -190, 19f);
            undergroundRenderer = CreateLayer("Underground", artCatalog.Underground, -180, 18f);
            RefreshScale(distantRenderer);
            RefreshScale(cloudRenderer);
            RefreshScale(undergroundRenderer);
            RefreshVisibility();
        }

        private void LateUpdate()
        {
            RefreshScale(distantRenderer);
            RefreshScale(cloudRenderer);
            RefreshScale(undergroundRenderer);
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            var showUnderground = transform.position.y < undergroundThreshold;
            if (distantRenderer != null) distantRenderer.enabled = !showUnderground;
            if (cloudRenderer != null) cloudRenderer.enabled = !showUnderground;
            if (undergroundRenderer != null) undergroundRenderer.enabled = showUnderground;
            if (undergroundVisible == showUnderground) return;
            undergroundVisible = showUnderground;
            Debug.Log($"[Nyangbingo] Background switched: " +
                      $"{(showUnderground ? "underground" : "surface")} " +
                      $"(cameraY={transform.position.y:0.0}, threshold={undergroundThreshold:0.0}).");
        }

        private SpriteRenderer CreateLayer(string objectName, Sprite sprite, int sortingOrder, float depth)
        {
            if (sprite == null) return null;
            var layer = new GameObject(objectName);
            layer.transform.SetParent(transform, false);
            layer.transform.localPosition = new Vector3(0f, 0f, depth);
            var renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void RefreshScale(SpriteRenderer renderer)
        {
            if (renderer?.sprite == null || targetCamera == null || !targetCamera.orthographic) return;
            var bounds = renderer.sprite.bounds.size;
            if (bounds.x <= 0f || bounds.y <= 0f) return;
            var viewHeight = targetCamera.orthographicSize * 2f;
            var viewWidth = viewHeight * targetCamera.aspect;
            var scale = Mathf.Max(viewWidth / bounds.x, viewHeight / bounds.y);
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
