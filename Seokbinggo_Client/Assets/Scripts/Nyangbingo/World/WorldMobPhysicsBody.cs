using System.Collections.Generic;
using Nyangbingo.Core;
using UnityEngine;

namespace Nyangbingo.World
{
    public enum WorldMobLocomotion
    {
        Grounded,
        Flying
    }

    /// <summary>
    /// Keeps yokai AI measured in game seconds while applying its displacement through
    /// the official foreground physics world. Ground units receive gravity; flying units
    /// remain airborne, but both are blocked by foreground colliders.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class WorldMobPhysicsBody : MonoBehaviour
    {
        private static readonly List<WorldMobPhysicsBody> ActiveMobBodies = new List<WorldMobPhysicsBody>();
        private const float GroundGravityScale = 3f;
        private const float CollisionSkin = .02f;
        private const float GroundProbeDepth = .12f;
        private const float StepJumpVelocity = 8.5f;
        public const float GroundBossStepJumpVelocity = 10.5f;
        private const float StepJumpCooldownSeconds = .35f;
        private const float NavigationReversalHoldSeconds = .5f;
        private const float DirectPathConfirmationSeconds = .2f;
        private const int PathTargetCellTolerance = 3;
        private readonly RaycastHit2D[] castHits = new RaycastHit2D[16];
        private Rigidbody2D body;
        private WorldMobLocomotion locomotion;
        private ContactFilter2D movementFilter;
        private Collider2D attachedCollider;
        private TileService navigationTiles;
        private readonly HashSet<Collider2D> ignoredCollisionColliders = new HashSet<Collider2D>();
        private readonly List<Vector3Int> path = new List<Vector3Int>();
        private Vector3Int pathTarget;
        private int pathIndex;
        private float nextPathRebuildTime;
        private float nextStepJumpTime;
        private Vector2 stableNavigationDirection;
        private float navigationDirectionHoldUntil;
        private float directPathClearSince = -1f;

        public WorldMobLocomotion Locomotion => locomotion;
        public bool IsFlying => locomotion == WorldMobLocomotion.Flying;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            attachedCollider = GetComponent<Collider2D>();
            movementFilter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = false,
                useDepth = false,
                useNormalAngle = false
            };
        }

        public void ConfigureForRuntime(WorldMobLocomotion value, TileService tiles = null)
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            locomotion = value;
            navigationTiles = tiles;
            path.Clear();
            pathIndex = 0;
            nextPathRebuildTime = float.NegativeInfinity;
            nextStepJumpTime = float.NegativeInfinity;
            stableNavigationDirection = Vector2.zero;
            navigationDirectionHoldUntil = 0f;
            directPathClearSince = -1f;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.simulated = true;
            body.useAutoMass = false;
            body.mass = 1f;
            body.gravityScale = IsFlying ? 0f : GroundGravityScale;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            if (IsFlying) body.linearVelocity = Vector2.zero;

            if (attachedCollider == null) attachedCollider = GetComponent<Collider2D>();
            if (attachedCollider != null) attachedCollider.isTrigger = false;
            RegisterMobCollisionIgnores();
            MainGameWorldDropRuntime.IgnoreCollisionWithActiveDrops(this);
        }

        public void IgnoreCollisionWith(Transform actorRoot)
        {
            if (actorRoot == null) return;
            if (attachedCollider == null) attachedCollider = GetComponent<Collider2D>();
            if (attachedCollider == null) return;
            var actorColliders = actorRoot.GetComponentsInChildren<Collider2D>(true);
            for (var index = 0; index < actorColliders.Length; index++)
            {
                var actorCollider = actorColliders[index];
                if (actorCollider == null || actorCollider == attachedCollider) continue;
                ignoredCollisionColliders.Add(actorCollider);
                Physics2D.IgnoreCollision(attachedCollider, actorCollider, true);
            }
        }

        public void IgnoreCollisionWith(Collider2D otherCollider)
        {
            if (otherCollider == null) return;
            if (attachedCollider == null) attachedCollider = GetComponent<Collider2D>();
            if (attachedCollider == null || otherCollider == attachedCollider) return;
            ignoredCollisionColliders.Add(otherCollider);
            Physics2D.IgnoreCollision(attachedCollider, otherCollider, true);
        }

        public static void IgnoreCollisionWithActiveMobs(Collider2D otherCollider)
        {
            if (otherCollider == null) return;
            for (var index = ActiveMobBodies.Count - 1; index >= 0; index--)
            {
                var mobBody = ActiveMobBodies[index];
                if (mobBody == null)
                {
                    ActiveMobBodies.RemoveAt(index);
                    continue;
                }
                mobBody.IgnoreCollisionWith(otherCollider);
            }
        }

        public void SetEncounterPaused(bool paused)
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (body == null) return;
            if (paused)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            body.simulated = !paused;
        }

        public Vector2 NavigationOffset(Vector2 worldOffset) =>
            IsFlying ? worldOffset : new Vector2(worldOffset.x, 0f);

        public Vector2 NavigationDirection(Vector2 worldOffset)
        {
            var targetOffset = NavigationOffset(worldOffset);
            if (targetOffset.sqrMagnitude <= Mathf.Epsilon) return Vector2.zero;
            if (!IsFlying || navigationTiles == null)
                return StabilizeNavigationDirection(targetOffset.normalized, targetOffset);

            var current = (Vector2)transform.position;
            var target = current + targetOffset;
            var startCell = ToCell(current);
            var targetCell = ToCell(target);
            var directPathClear = HasClearNavigationLine(current, target);
            if (directPathClear && (path.Count == 0 || pathIndex >= path.Count))
            {
                path.Clear();
                pathIndex = 0;
                directPathClearSince = -1f;
                return StabilizeNavigationDirection(targetOffset.normalized, targetOffset);
            }
            if (directPathClear)
            {
                if (directPathClearSince < 0f) directPathClearSince = Time.unscaledTime;
                if (Time.unscaledTime - directPathClearSince >= DirectPathConfirmationSeconds)
                {
                    path.Clear();
                    pathIndex = 0;
                    directPathClearSince = -1f;
                    return StabilizeNavigationDirection(targetOffset.normalized, targetOffset);
                }
            }
            else directPathClearSince = -1f;

            var targetMovedBeyondTolerance = path.Count > 0 &&
                                             Manhattan(pathTarget, targetCell) > PathTargetCellTolerance;
            var pathInvalid = pathIndex >= path.Count || targetMovedBeyondTolerance ||
                              !IsTraversable(path[pathIndex]);
            if (pathInvalid && Time.unscaledTime >= nextPathRebuildTime)
            {
                RebuildPath(startCell, targetCell);
                var startCenter = CellCenter(startCell);
                if (path.Count > 0 && (startCenter - current).sqrMagnitude > .01f)
                    path.Insert(0, startCell);
                nextPathRebuildTime = Time.unscaledTime + .25f;
            }

            while (pathIndex < path.Count)
            {
                var waypointOffset = CellCenter(path[pathIndex]) - current;
                if (waypointOffset.sqrMagnitude > .04f)
                    return StabilizeNavigationDirection(waypointOffset.normalized, targetOffset);
                pathIndex++;
            }
            return StabilizeNavigationDirection(targetOffset.normalized, targetOffset);
        }

        private Vector2 StabilizeNavigationDirection(Vector2 proposed, Vector2 targetOffset)
        {
            if (proposed.sqrMagnitude <= Mathf.Epsilon) return Vector2.zero;
            proposed.Normalize();
            if (stableNavigationDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                stableNavigationDirection = proposed;
                navigationDirectionHoldUntil = Time.unscaledTime + NavigationReversalHoldSeconds;
                return proposed;
            }

            var reversesCurrentDirection = Vector2.Dot(stableNavigationDirection, proposed) < -.35f;
            var targetActuallyCrossedBehind = targetOffset.sqrMagnitude > Mathf.Epsilon &&
                                              Vector2.Dot(stableNavigationDirection,
                                                  targetOffset.normalized) < -.35f;
            if (reversesCurrentDirection && !targetActuallyCrossedBehind &&
                Time.unscaledTime < navigationDirectionHoldUntil)
                return stableNavigationDirection;

            if (Vector2.Dot(stableNavigationDirection, proposed) < .95f)
                navigationDirectionHoldUntil = Time.unscaledTime + NavigationReversalHoldSeconds;
            stableNavigationDirection = proposed;
            return proposed;
        }

        public bool HasClearAttackLine(Vector2 worldTarget)
        {
            if (navigationTiles == null) return true;
            return HasClearPointLine(ToCell(body != null ? body.position : (Vector2)transform.position),
                ToCell(worldTarget));
        }

        public float Move(Vector2 requestedDisplacement)
        {
            if (body == null || !body.simulated || !IsFinite(requestedDisplacement)) return 0f;
            if (!IsFlying) requestedDisplacement.y = 0f;
            var requestedDistance = requestedDisplacement.magnitude;
            if (requestedDistance <= Mathf.Epsilon) return 0f;

            var direction = requestedDisplacement / requestedDistance;
            if (!IsFlying) TryBeginStepJump(direction);
            var allowedDistance = requestedDistance;
            var hitCount = body.Cast(direction, movementFilter, castHits, requestedDistance + CollisionSkin);
            for (var index = 0; index < hitCount; index++)
            {
                var hit = castHits[index];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) ||
                    ignoredCollisionColliders.Contains(hit.collider)) continue;
                allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - CollisionSkin));
            }

            if (allowedDistance <= Mathf.Epsilon) return 0f;
            body.position += direction * allowedDistance;
            return allowedDistance;
        }

        private bool TryBeginStepJump(Vector2 direction)
        {
            if (navigationTiles == null || body == null || Mathf.Abs(direction.x) <= .1f ||
                Time.unscaledTime < nextStepJumpTime || !IsGroundedOnTiles()) return false;
            if (attachedCollider == null) attachedCollider = GetComponent<Collider2D>();
            var extents = attachedCollider != null ? attachedCollider.bounds.extents : Vector3.one * .4f;
            var horizontalSign = direction.x > 0f ? 1 : -1;
            var forwardProbe = body.position + Vector2.right * horizontalSign * (extents.x + .08f);
            var blockingCell = ToCell(forwardProbe);
            if (IsAirCell(blockingCell)) return false;

            var rise = 1f + Mathf.Max(0f, extents.y - .45f);
            var verticalClearPosition = body.position + Vector2.up * rise;
            var landingClearPosition = verticalClearPosition + Vector2.right * horizontalSign;
            if (!IsPositionClear(verticalClearPosition) || !IsPositionClear(landingClearPosition)) return false;

            body.linearVelocity = new Vector2(body.linearVelocity.x,
                StepJumpVelocityForCollider(extents.x));
            nextStepJumpTime = Time.unscaledTime + StepJumpCooldownSeconds;
            return true;
        }

        public static float StepJumpVelocityForCollider(float horizontalExtent) =>
            horizontalExtent > .5f ? GroundBossStepJumpVelocity : StepJumpVelocity;

        private bool IsGroundedOnTiles()
        {
            if (attachedCollider == null) attachedCollider = GetComponent<Collider2D>();
            var extentY = attachedCollider != null ? attachedCollider.bounds.extents.y : .4f;
            var groundProbe = body.position + Vector2.down * (extentY + GroundProbeDepth);
            return !IsAirCell(ToCell(groundProbe));
        }

        public static WorldMobLocomotion ForYokai(YokaiKind kind)
        {
            switch (kind)
            {
                case YokaiKind.Eoduksini:
                case YokaiKind.Gangcheori:
                    return WorldMobLocomotion.Flying;
                default:
                    return WorldMobLocomotion.Grounded;
            }
        }

        public static WorldMobLocomotion ForBoss(BossKind kind)
        {
            switch (kind)
            {
                case BossKind.Imugi:
                case BossKind.Gangcheori:
                    return WorldMobLocomotion.Flying;
                default:
                    return WorldMobLocomotion.Grounded;
            }
        }

        public static float PhysicalRadiusForBoss(BossKind kind) =>
            ForBoss(kind) == WorldMobLocomotion.Flying ? .34f : .65f;

        public static float ColliderVerticalOffsetForBoss(BossKind kind) =>
            ForBoss(kind) == WorldMobLocomotion.Grounded ? PhysicalRadiusForBoss(kind) : 0f;

        private void RebuildPath(Vector3Int start, Vector3Int target)
        {
            path.Clear();
            pathIndex = 0;
            pathTarget = target;
            if (!IsTraversable(start) || !IsTraversable(target)) return;

            const int margin = 12;
            const int maximumVisited = 4096;
            var minX = Mathf.Max(0, Mathf.Min(start.x, target.x) - margin);
            var maxX = Mathf.Min(navigationTiles.Width - 1, Mathf.Max(start.x, target.x) + margin);
            var minY = Mathf.Max(0, Mathf.Min(start.y, target.y) - margin);
            var maxY = Mathf.Min(navigationTiles.Height - 1, Mathf.Max(start.y, target.y) + margin);
            var open = new List<Vector3Int> { start };
            var openSet = new HashSet<Vector3Int> { start };
            var closed = new HashSet<Vector3Int>();
            var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
            var costs = new Dictionary<Vector3Int, int> { [start] = 0 };
            var directions = new[] { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down };

            while (open.Count > 0 && closed.Count < maximumVisited)
            {
                var bestIndex = 0;
                var bestScore = int.MaxValue;
                for (var index = 0; index < open.Count; index++)
                {
                    var candidate = open[index];
                    var score = costs[candidate] + Manhattan(candidate, target);
                    if (score >= bestScore) continue;
                    bestScore = score;
                    bestIndex = index;
                }

                var current = open[bestIndex];
                open.RemoveAt(bestIndex);
                openSet.Remove(current);
                if (current == target)
                {
                    BuildPath(cameFrom, start, target);
                    return;
                }
                closed.Add(current);

                for (var directionIndex = 0; directionIndex < directions.Length; directionIndex++)
                {
                    var next = current + directions[directionIndex];
                    if (next.x < minX || next.x > maxX || next.y < minY || next.y > maxY ||
                        closed.Contains(next) || !IsTraversable(next)) continue;
                    var nextCost = costs[current] + 1;
                    if (costs.TryGetValue(next, out var previousCost) && nextCost >= previousCost) continue;
                    cameFrom[next] = current;
                    costs[next] = nextCost;
                    if (openSet.Add(next)) open.Add(next);
                }
            }
        }

        private void BuildPath(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int start, Vector3Int target)
        {
            var current = target;
            while (current != start)
            {
                path.Add(current);
                if (!cameFrom.TryGetValue(current, out current))
                {
                    path.Clear();
                    return;
                }
            }
            path.Reverse();
        }

        private bool HasClearNavigationLine(Vector2 startPoint, Vector2 endPoint)
        {
            var distance = Vector2.Distance(startPoint, endPoint);
            var steps = Mathf.Max(1, Mathf.CeilToInt(distance * 4f));
            for (var step = 1; step <= steps; step++)
                if (!IsPositionClear(Vector2.Lerp(startPoint, endPoint, step / (float)steps))) return false;
            return true;
        }

        private bool HasClearPointLine(Vector3Int start, Vector3Int target)
        {
            var startPoint = CellCenter(start);
            var endPoint = CellCenter(target);
            var distance = Vector2.Distance(startPoint, endPoint);
            var steps = Mathf.Max(1, Mathf.CeilToInt(distance * 4f));
            for (var step = 1; step < steps; step++)
                if (!IsAirCell(ToCell(Vector2.Lerp(startPoint, endPoint, step / (float)steps)))) return false;
            return true;
        }

        private bool IsTraversable(Vector3Int cell) => IsPositionClear(CellCenter(cell));

        private bool IsPositionClear(Vector2 position)
        {
            if (attachedCollider == null) attachedCollider = GetComponent<Collider2D>();
            var extents = attachedCollider != null ? attachedCollider.bounds.extents : Vector3.one * .4f;
            var colliderCenterOffset = attachedCollider != null
                ? (Vector2)attachedCollider.bounds.center - (body != null ? body.position : (Vector2)transform.position)
                : Vector2.zero;
            var colliderCenter = position + colliderCenterOffset;
            var min = ToCell(colliderCenter - new Vector2(Mathf.Max(0f, extents.x - CollisionSkin),
                Mathf.Max(0f, extents.y - CollisionSkin)));
            var max = ToCell(colliderCenter + new Vector2(Mathf.Max(0f, extents.x - CollisionSkin),
                Mathf.Max(0f, extents.y - CollisionSkin)));
            for (var x = min.x; x <= max.x; x++)
            for (var y = min.y; y <= max.y; y++)
                if (!IsAirCell(new Vector3Int(x, y, 0))) return false;
            return true;
        }

        private bool IsAirCell(Vector3Int cell) =>
            navigationTiles != null && navigationTiles.InBounds(cell) && navigationTiles.GetTile(cell).IsAir;

        private static Vector3Int ToCell(Vector2 position) =>
            new Vector3Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), 0);

        private static Vector2 CellCenter(Vector3Int cell) => new Vector2(cell.x + .5f, cell.y + .5f);

        private static int Manhattan(Vector3Int first, Vector3Int second) =>
            Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y);

        private void RegisterMobCollisionIgnores()
        {
            if (attachedCollider == null) return;
            for (var index = ActiveMobBodies.Count - 1; index >= 0; index--)
            {
                var other = ActiveMobBodies[index];
                if (other == null)
                {
                    ActiveMobBodies.RemoveAt(index);
                    continue;
                }
                if (other == this) continue;
                if (other.attachedCollider == null) other.attachedCollider = other.GetComponent<Collider2D>();
                if (other.attachedCollider == null) continue;
                Physics2D.IgnoreCollision(attachedCollider, other.attachedCollider, true);
                ignoredCollisionColliders.Add(other.attachedCollider);
                other.ignoredCollisionColliders.Add(attachedCollider);
            }
            if (!ActiveMobBodies.Contains(this)) ActiveMobBodies.Add(this);
        }

        private void OnDestroy() => ActiveMobBodies.Remove(this);

        private static bool IsFinite(Vector2 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }
}
