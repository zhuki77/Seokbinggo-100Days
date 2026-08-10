using System;
using Nyangbingo.Data;
using UnityEngine;

namespace Nyangbingo.World
{
    /// <summary>
    /// v7 플레이어 사이드스크롤 물리(점프·중력). globals.csv 정본을 읽어 테라리아급 체감을 맞춘다.
    /// </summary>
    public static class PlayerMovementPhysics
    {
        public const string JumpHeightTilesKey = "player_jump_height_tiles";
        public const string GravityKey = "player_gravity";
        public const string MaxFallSpeedKey = "player_max_fall_speed";
        public const string JumpCutKey = "player_jump_cut";

        // globals 미연결 시 폴백 — 테라리아 약 3.5~4칸 점프 체감(1타일=1유닛).
        public const float DefaultJumpHeightTiles = 3.5f;
        public const float DefaultGravity = 32f;
        public const float DefaultMaxFallSpeed = 12f;
        public const float DefaultJumpCut = 0.5f;
        public const float DefaultCoyoteTimeSeconds = 0.1f;

        public readonly struct Config
        {
            public Config(float jumpHeightTiles, float gravity, float maxFallSpeed, float jumpCutMultiplier)
            {
                JumpHeightTiles = jumpHeightTiles;
                Gravity = gravity;
                MaxFallSpeed = maxFallSpeed;
                JumpCutMultiplier = jumpCutMultiplier;
                JumpVelocity = CalculateJumpVelocity(jumpHeightTiles, gravity);
            }

            public float JumpHeightTiles { get; }
            public float Gravity { get; }
            public float MaxFallSpeed { get; }
            public float JumpCutMultiplier { get; }
            public float JumpVelocity { get; }
        }

        public static bool TryLoadFromCatalog(GameDataCatalog catalog, out Config config)
        {
            config = default;
            if (catalog == null) return false;

            if (!TryReadPositive(catalog, JumpHeightTilesKey, out var jumpHeight) ||
                !TryReadPositive(catalog, GravityKey, out var gravity) ||
                !TryReadPositive(catalog, MaxFallSpeedKey, out var maxFall) ||
                !TryReadPositive(catalog, JumpCutKey, out var jumpCut) ||
                jumpCut > 1f)
                return false;

            config = new Config(jumpHeight, gravity, maxFall, jumpCut);
            return config.JumpVelocity > 0f;
        }

        public static Config CreateDefault() =>
            new Config(DefaultJumpHeightTiles, DefaultGravity, DefaultMaxFallSpeed, DefaultJumpCut);

        /// <summary>연속 근사 h = v₀² / (2g).</summary>
        public static float CalculateJumpVelocity(float jumpHeightTiles, float gravity)
        {
            if (float.IsNaN(jumpHeightTiles) || float.IsInfinity(jumpHeightTiles) ||
                float.IsNaN(gravity) || float.IsInfinity(gravity) ||
                jumpHeightTiles <= 0f || gravity <= 0f)
                return 0f;
            return Mathf.Sqrt(2f * gravity * jumpHeightTiles);
        }

        public static float ApplyGravity(float currentVelocity, float gravity, float maxFallSpeed,
            float deltaSeconds)
        {
            if (float.IsNaN(currentVelocity) || float.IsInfinity(currentVelocity)) currentVelocity = 0f;
            if (float.IsNaN(gravity) || float.IsInfinity(gravity)) gravity = 0f;
            if (float.IsNaN(maxFallSpeed) || float.IsInfinity(maxFallSpeed)) maxFallSpeed = 0f;
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) deltaSeconds = 0f;
            return Mathf.Max(-Mathf.Max(0f, maxFallSpeed),
                currentVelocity - Mathf.Max(0f, gravity) * Mathf.Max(0f, deltaSeconds));
        }

        /// <summary>점프 키를 뗀 뒤 상승 중일 때 매 틱 속도를 깎아 가변 점프 높이를 만든다(테라리아식).</summary>
        public static float ApplyJumpCutWhileAscending(float verticalVelocity, bool jumpHeld,
            float jumpCutMultiplier)
        {
            if (jumpHeld || verticalVelocity <= 0f) return verticalVelocity;
            if (float.IsNaN(jumpCutMultiplier) || float.IsInfinity(jumpCutMultiplier) ||
                jumpCutMultiplier <= 0f || jumpCutMultiplier > 1f)
                return verticalVelocity;
            return verticalVelocity * jumpCutMultiplier;
        }

        public static float CalculateJumpVelocityForHeightRatio(float baseJumpVelocity, float heightRatio)
        {
            if (float.IsNaN(baseJumpVelocity) || float.IsInfinity(baseJumpVelocity) ||
                float.IsNaN(heightRatio) || float.IsInfinity(heightRatio))
                return 0f;
            return Mathf.Max(0f, baseJumpVelocity) * Mathf.Sqrt(Mathf.Max(0f, heightRatio));
        }

        public static float TickCoyoteTime(bool grounded, float remainingSeconds,
            float durationSeconds, float deltaSeconds)
        {
            if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds)) durationSeconds = 0f;
            if (float.IsNaN(remainingSeconds) || float.IsInfinity(remainingSeconds)) remainingSeconds = 0f;
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) deltaSeconds = 0f;
            var duration = Mathf.Max(0f, durationSeconds);
            return grounded
                ? duration
                : Mathf.Max(0f, Mathf.Min(duration, remainingSeconds) - Mathf.Max(0f, deltaSeconds));
        }

        public static bool CanUseGroundJump(bool grounded, float coyoteTimeRemaining) =>
            grounded || coyoteTimeRemaining > 0f &&
            !float.IsNaN(coyoteTimeRemaining) && !float.IsInfinity(coyoteTimeRemaining);

        /// <summary>회귀용 — holdFrames 동안 점프 키를 누른 것으로 가정한 최대 상승 높이(타일).</summary>
        public static float SimulatePeakJumpHeightTiles(Config config, float fixedDeltaSeconds,
            int holdFrames = int.MaxValue, int maxSteps = 600)
        {
            if (fixedDeltaSeconds <= 0f || config.JumpVelocity <= 0f || config.Gravity <= 0f) return 0f;
            var y = 0f;
            var peak = 0f;
            var vy = config.JumpVelocity;
            for (var step = 0; step < maxSteps && vy > -config.MaxFallSpeed; step++)
            {
                var jumpHeld = step < holdFrames;
                vy = ApplyJumpCutWhileAscending(vy, jumpHeld, config.JumpCutMultiplier);
                vy = ApplyGravity(vy, config.Gravity, config.MaxFallSpeed, fixedDeltaSeconds);
                y += vy * fixedDeltaSeconds;
                if (y > peak) peak = y;
                if (y <= 0f && step > 0) break;
            }
            return peak;
        }

        private static bool TryReadPositive(GameDataCatalog catalog, string key, out float value)
        {
            value = 0f;
            var definition = catalog.FindGlobal(key);
            return definition != null && definition.TryGetFloat(out value) && value > 0f &&
                   !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
