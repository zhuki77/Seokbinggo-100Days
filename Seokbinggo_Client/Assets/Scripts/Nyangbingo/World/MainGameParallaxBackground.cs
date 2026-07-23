using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    [RequireComponent(typeof(Camera))]
    public sealed class MainGameParallaxBackground : MonoBehaviour
    {
        private const float SurfaceCanvasWidthPixels = 320f;
        private const float SurfaceCanvasHeightPixels = 160f;
        private const float SurfacePixelsPerUnit = 16f;
        [SerializeField] private EnvironmentArtCatalog artCatalog;
        [SerializeField] private float undergroundThreshold;
        private Camera targetCamera;
        private DayNightService timeService;
        private SpriteRenderer skyRenderer;
        private SpriteRenderer mountainRenderer;
        private SpriteRenderer frontCloudRenderer;
        private SpriteRenderer celestialRenderer;
        private SpriteRenderer distantRenderer;
        private SpriteRenderer cloudRenderer;
        private SpriteRenderer undergroundRenderer;
        private bool? undergroundVisible;
        private bool? nightVisual;

        public bool HasConfiguredArt => artCatalog != null && artCatalog.Underground != null &&
                                        (artCatalog.HasDayNightSurfaceSet ||
                                         artCatalog.DistantView != null && artCatalog.Clouds != null);
        public bool IsUnderground => transform.position.y < undergroundThreshold;
        public float UndergroundThreshold => undergroundThreshold;

        public void ConfigureForScene(EnvironmentArtCatalog catalog, float undergroundWorldY)
        {
            artCatalog = catalog;
            undergroundThreshold = undergroundWorldY;
        }

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            if (artCatalog == null || targetCamera == null) return;
            timeService = FindAnyObjectByType<DayNightService>();
            if (artCatalog.HasDayNightSurfaceSet)
            {
                skyRenderer = CreateLayer("SurfaceSky", artCatalog.DaySky, -220, 20f);
                // The delivered reference composites omit the upper/rear ribbon. Keep that source in
                // the catalog for future parallax variants, but the product composition uses only the
                // lower/front ribbon so it matches day.png/night.png exactly.
                celestialRenderer = CreateLayer("SurfaceCelestial", artCatalog.Sun, -212, 19f);
                mountainRenderer = CreateLayer("SurfaceMountains", artCatalog.DayMountains, -210, 18.5f);
                frontCloudRenderer = CreateLayer("SurfaceFrontClouds", artCatalog.DayFrontClouds, -205, 18f);
            }
            else
            {
                distantRenderer = CreateLayer("DistantView", artCatalog.DistantView, -200, 20f);
                cloudRenderer = CreateLayer("Clouds", artCatalog.Clouds, -190, 19f);
            }
            undergroundRenderer = CreateLayer("Underground", artCatalog.Underground, -180, 18f);
            RefreshSurfaceSprites(true);
            RefreshLayout();
            RefreshVisibility();
        }

        private void LateUpdate()
        {
            if (artCatalog == null || targetCamera == null) return;
            timeService ??= FindAnyObjectByType<DayNightService>();
            RefreshSurfaceSprites(false);
            RefreshLayout();
            RefreshVisibility();
        }

        private void RefreshSurfaceSprites(bool force)
        {
            if (!artCatalog.HasDayNightSurfaceSet) return;
            var showNight = timeService != null && timeService.IsNight;
            if (!force && nightVisual == showNight) return;
            nightVisual = showNight;
            skyRenderer.sprite = showNight ? artCatalog.NightSky : artCatalog.DaySky;
            mountainRenderer.sprite = showNight ? artCatalog.NightMountains : artCatalog.DayMountains;
            frontCloudRenderer.sprite = showNight ? artCatalog.NightFrontClouds : artCatalog.DayFrontClouds;
            celestialRenderer.sprite = showNight ? artCatalog.Moon : artCatalog.Sun;
        }

        private void RefreshLayout()
        {
            if (artCatalog.HasDayNightSurfaceSet) RefreshSurfaceCanvasLayout();
            else
            {
                RefreshScale(distantRenderer);
                RefreshScale(cloudRenderer);
            }
            RefreshScale(undergroundRenderer);
            RefreshCelestialLayout();
        }

        private void RefreshSurfaceCanvasLayout()
        {
            if (targetCamera == null || !targetCamera.orthographic) return;
            var viewHeight = targetCamera.orthographicSize * 2f;
            var viewWidth = viewHeight * targetCamera.aspect;
            var canvasWidth = SurfaceCanvasWidthPixels / SurfacePixelsPerUnit;
            var canvasHeight = SurfaceCanvasHeightPixels / SurfacePixelsPerUnit;
            // The delivered background is 2:1 while gameplay is 16:9. Fit to width and top-align it:
            // the complete authored landscape remains visible, and the unavoidable short margin sits
            // behind the terrain-heavy bottom of the gameplay view instead of cropping both sides.
            var scale = viewWidth / canvasWidth;
            var canvasWorldHeight = canvasHeight * scale;
            var canvasOriginY = viewHeight * .5f - canvasWorldHeight;
            ApplySurfaceCanvasTransform(skyRenderer, scale, canvasOriginY);
            ApplySurfaceCanvasTransform(mountainRenderer, scale, canvasOriginY);
            ApplySurfaceCanvasTransform(frontCloudRenderer, scale, canvasOriginY);
        }

        private static void ApplySurfaceCanvasTransform(SpriteRenderer renderer, float scale, float canvasOriginY)
        {
            if (renderer == null) return;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            // Aseprite keeps each cropped cel's pivot relative to the original canvas bottom-center.
            // Sharing one origin preserves the authored 320x160 composite instead of centering each crop.
            renderer.transform.localPosition = new Vector3(0f, canvasOriginY,
                renderer.transform.localPosition.z);
        }

        private void RefreshVisibility()
        {
            var showUnderground = transform.position.y < undergroundThreshold;
            if (skyRenderer != null) skyRenderer.enabled = !showUnderground;
            if (mountainRenderer != null) mountainRenderer.enabled = !showUnderground;
            if (frontCloudRenderer != null) frontCloudRenderer.enabled = !showUnderground;
            if (celestialRenderer != null) celestialRenderer.enabled = !showUnderground;
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
            var spriteCenter = renderer.sprite.bounds.center;
            renderer.transform.localPosition = new Vector3(
                -spriteCenter.x * scale,
                -spriteCenter.y * scale,
                renderer.transform.localPosition.z);
        }

        private void RefreshCelestialLayout()
        {
            if (celestialRenderer?.sprite == null || targetCamera == null || !targetCamera.orthographic) return;
            var bounds = celestialRenderer.sprite.bounds.size;
            if (bounds.x <= 0f || bounds.y <= 0f) return;
            var viewHeight = targetCamera.orthographicSize * 2f;
            var viewWidth = viewHeight * targetCamera.aspect;
            var targetHeight = viewHeight * .32f;
            var scale = targetHeight / bounds.y;
            celestialRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            var spriteCenter = celestialRenderer.sprite.bounds.center;
            celestialRenderer.transform.localPosition = new Vector3(
                viewWidth * .31f - spriteCenter.x * scale,
                viewHeight * .25f - spriteCenter.y * scale,
                celestialRenderer.transform.localPosition.z);
        }
    }
}
