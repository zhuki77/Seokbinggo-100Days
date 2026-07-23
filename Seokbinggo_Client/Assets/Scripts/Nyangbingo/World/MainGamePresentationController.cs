using Nyangbingo.Data;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nyangbingo.World
{
    /// <summary>
    /// MainGame 표시 전용 런타임. 최신 기획 정본의 960x540/PPU 16 카메라와
    /// 낮·밤·지하·백중 Global Light 2D를 월드 로직과 분리해 적용한다.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(Camera))]
    public sealed class MainGamePresentationController : MonoBehaviour
    {
        private const int AssetsPixelsPerUnit = 16;
        private const int ReferenceWidth = 960;
        private const int ReferenceHeight = 540;
        // Soft day/night ramps in the Stardew/Terraria range: readable nights, warm days,
        // and a dimmer underground ambient so portable lanterns actually matter.
        private const float TransitionSeconds = 90f;

        private static readonly Color DayStage1Color = HtmlColor("#FFF6E8");
        private static readonly Color DayStage2Color = HtmlColor("#FFE2B8");
        private static readonly Color DayStage3Color = HtmlColor("#FFC994");
        private static readonly Color NightColor = HtmlColor("#6B7FA8");
        private static readonly Color UndergroundColor = HtmlColor("#3E4A5C");
        private static readonly Color BaekjungNightColor = HtmlColor("#7A6BA8");

        [SerializeField] private DayNightService timeService;
        [SerializeField] private MainGameParallaxBackground background;
        [SerializeField] private Light2D globalLight;

        private bool initialized;

        public bool IsInitialized => initialized;
        public Light2D GlobalLight => globalLight;

        public void ConfigureForRuntime(DayNightService clock, MainGameParallaxBackground parallax)
        {
            timeService = clock;
            background = parallax;
            Initialize();
        }

        private void Awake() => ConfigurePixelPerfectCamera();

        private void Start() => Initialize();

        private void LateUpdate()
        {
            if (!initialized && !Initialize()) return;
            ApplyLighting();
        }

        private bool Initialize()
        {
            if (initialized) return true;
            timeService ??= FindAnyObjectByType<DayNightService>();
            background ??= GetComponent<MainGameParallaxBackground>();
            ConfigurePixelPerfectCamera();
            EnsureGlobalLight();
            if (timeService == null || globalLight == null) return false;

            initialized = true;
            ApplyLighting();
            Debug.Log("[Nyangbingo] MainGame presentation ready " +
                      $"(camera={ReferenceWidth}x{ReferenceHeight}, ppu={AssetsPixelsPerUnit}, " +
                      "globalLight=day/night/underground, transition=90 game seconds).");
            return true;
        }

        private void ConfigurePixelPerfectCamera()
        {
            var targetCamera = GetComponent<Camera>();
            if (targetCamera == null) return;
            targetCamera.orthographic = true;
            targetCamera.allowMSAA = false;

            var pixelPerfect = GetComponent<PixelPerfectCamera>() ?? gameObject.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = AssetsPixelsPerUnit;
            pixelPerfect.refResolutionX = ReferenceWidth;
            pixelPerfect.refResolutionY = ReferenceHeight;
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            // Windowbox only renders the largest integer multiple of 960x540. On a 2560x1440
            // display that leaves a 320/180 black border around the 1920x1080 world while the
            // screen-space UI still fills the display. StretchFill keeps the 16:9 framing and
            // lets URP's RetroAA final blit cover the complete display at non-integer scales.
            pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.StretchFill;
        }

        private void EnsureGlobalLight()
        {
            if (globalLight != null) return;
            var sceneLights = FindObjectsByType<Light2D>(FindObjectsInactive.Include);
            foreach (var light in sceneLights)
            {
                if (light != null && light.lightType == Light2D.LightType.Global)
                {
                    globalLight = light;
                    return;
                }
            }

            var lightObject = new GameObject("NyangbingoGlobalLight2D");
            lightObject.transform.SetParent(transform, false);
            globalLight = lightObject.AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
        }

        private void ApplyLighting()
        {
            var state = EvaluateLighting(timeService, background != null && background.IsUnderground);
            globalLight.color = state.color;
            globalLight.intensity = state.intensity;
        }

        public static (Color color, float intensity) EvaluateLighting(DayNightService clock, bool underground)
        {
            if (underground) return (UndergroundColor, .22f);
            if (clock == null) return (NightColor, .62f);

            var dayState = DayLighting(clock.CurrentDayCurve?.HeatStage ?? 1);
            var nightState = IsBaekjungNight(clock)
                ? (BaekjungNightColor, .58f)
                : (NightColor, .62f);

            if (clock.IsNight) return nightState;

            var elapsed = Mathf.Max(0f, clock.TimeOfDayGameSeconds);
            var remaining = Mathf.Max(0f, clock.DayDurationSeconds - elapsed);
            if (elapsed < TransitionSeconds)
                return Lerp(nightState, dayState, SmoothStep(elapsed / TransitionSeconds));
            if (remaining < TransitionSeconds)
                return Lerp(dayState, nightState, SmoothStep(1f - remaining / TransitionSeconds));
            return dayState;
        }

        private static (Color color, float intensity) DayLighting(int heatStage)
        {
            switch (Mathf.Clamp(heatStage, 1, 3))
            {
                case 2: return (DayStage2Color, 1.12f);
                case 3: return (DayStage3Color, 1.2f);
                default: return (DayStage1Color, 1.05f);
            }
        }

        private static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static bool IsBaekjungNight(DayNightService clock) =>
            clock.IsNight && clock.OfficialGlobals != null &&
            clock.OfficialGlobals.TryGetInt(GlobalKeys.BaekjungDay, out var baekjungDay) &&
            clock.Day == baekjungDay;

        private static (Color color, float intensity) Lerp(
            (Color color, float intensity) from, (Color color, float intensity) to, float t)
        {
            t = Mathf.Clamp01(t);
            return (Color.Lerp(from.color, to.color, t), Mathf.Lerp(from.intensity, to.intensity, t));
        }

        private static Color HtmlColor(string value) =>
            ColorUtility.TryParseHtmlString(value, out var color) ? color : Color.white;
    }
}
