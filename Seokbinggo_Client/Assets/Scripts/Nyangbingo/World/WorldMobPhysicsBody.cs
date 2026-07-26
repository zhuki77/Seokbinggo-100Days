using System.Collections.Generic;
using Nyangbingo.Core;
using Nyangbingo.Data;
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
    /// the official foreground physics world. Ground units receive gravity. Flying units
    /// use a cast-driven kinematic core so terrain blocks them without leaving persistent
    /// collision impulses that fight their pursuit direction.
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
        private const int GroundRouteSearchMargin = 32;
        private const int GroundRouteMaximumVisited = 8192;
        private const int GroundRouteMaximumDropCells = 32;
        private const float GroundRouteStuckSeconds = 1f;
        private const float GroundRouteRetrySeconds = .5f;
        private const int MaximumCollisionSlidePasses = 2;
        private const int MaximumDepenetrationPasses = 3;
        public const float KnockbackDurationSeconds = .24f;
        private readonly RaycastHit2D[] castHits = new RaycastHit2D[16];
        private readonly Collider2D[] overlapHits = new Collider2D[16];
        private Rigidbody2D body;
        private WorldMobLocomotion locomotion;
        private ContactFilter2D movementFilter;
        private Collider2D attachedCollider;
        private TileService navigationTiles;
        private System.Func<YokaiWallMaterial, bool> canDestroyWallMaterial;
        private readonly HashSet<Collider2D> ignoredCollisionColliders = new HashSet<Collider2D>();
        private readonly List<Vector3Int> path = new List<Vector3Int>();
        private readonly List<Vector3Int> groundPath = new List<Vector3Int>();
        private Vector3Int pathTarget;
        private int pathIndex;
        private int groundPathIndex;
        private float nextPathRebuildTime;
        private float nextStepJumpTime;
        private Vector2 stableNavigationDirection;
        private float navigationFacingX;
        private float navigationDirectionHoldUntil;
        private float directPathClearSince = -1f;
        private Vector2 groundRouteDirection;
        private Vector3Int groundRouteTarget;
        private Vector2 groundRouteLastProgressPosition;
        private float groundRouteLastProgressTime;
        private float nextGroundRouteRetryTime;
        private int groundRouteTieBias = 1;
        private bool groundRouteReachesTarget;
        private bool groundDropCommitted;
        private Vector3Int groundDropLandingCell;
        private Vector2 knockbackRemainingDisplacement;
        private float knockbackRemainingSeconds;
        private Vector2 encounterPausedLinearVelocity;
        private float encounterPausedAngularVelocity;
        private bool hasEncounterPausedVelocity;

        public WorldMobLocomotion Locomotion => locomotion;
        public bool IsFlying => locomotion == WorldMobLocomotion.Flying;
        public bool HasTraversableGroundRoute =>
            locomotion == WorldMobLocomotion.Grounded &&
            groundRouteReachesTarget &&
            groundPathIndex < groundPath.Count;
        public Vector2 NavigationFacingDirection =>
            Mathf.Abs(navigationFacingX) > Mathf.Epsilon
                ? new Vector2(navigationFacingX, 0f)
                : Vector2.zero;
        public Vector2 LastMoveDisplacement { get; private set; }
        public bool IsKnockbackActive => knockbackRemainingSeconds > 0f &&
                                         knockbackRemainingDisplacement.sqrMagnitude > Mathf.Epsilon;

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

        public void ConfigureForRuntime(
            WorldMobLocomotion value,
            TileService tiles = null,
            System.Func<YokaiWallMaterial, bool> destroyableWallMaterial = null)
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            locomotion = value;
            navigationTiles = tiles;
            canDestroyWallMaterial = destroyableWallMaterial;
            path.Clear();
            pathIndex = 0;
            groundPath.Clear();
            groundPathIndex = 0;
            nextPathRebuildTime = float.NegativeInfinity;
            nextStepJumpTime = float.NegativeInfinity;
            stableNavigationDirection = Vector2.zero;
            navigationFacingX = 0f;
            navigationDirectionHoldUntil = 0f;
            directPathClearSince = -1f;
            groundRouteDirection = Vector2.zero;
            groundRouteTarget = default;
            groundRouteLastProgressPosition = body.position;
            groundRouteLastProgressTime = Time.unscaledTime;
            nextGroundRouteRetryTime = float.NegativeInfinity;
            groundRouteTieBias = 1;
            groundRouteReachesTarget = false;
            groundDropCommitted = false;
            groundDropLandingCell = default;
            LastMoveDisplacement = Vector2.zero;
            knockbackRemainingDisplacement = Vector2.zero;
            knockbackRemainingSeconds = 0f;
            body.bodyType = IsFlying ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
            body.simulated = true;
            body.useAutoMass = false;
            body.mass = 1f;
            body.gravityScale = IsFlying ? 0f : GroundGravityScale;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = IsFlying
                ? CollisionDetectionMode2D.Discrete
                : CollisionDetectionMode2D.Continuous;
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
            var mobColliders = transform.GetComponentsInChildren<Collider2D>(true);
            var actorColliders = actorRoot.GetComponentsInChildren<Collider2D>(true);
            for (var actorIndex = 0; actorIndex < actorColliders.Length; actorIndex++)
            {
                var actorCollider = actorColliders[actorIndex];
                if (actorCollider == null) continue;
                ignoredCollisionColliders.Add(actorCollider);
                for (var mobIndex = 0; mobIndex < mobColliders.Length; mobIndex++)
                {
                    var mobCollider = mobColliders[mobIndex];
                    if (mobCollider == null || mobCollider == actorCollider) continue;
                    Physics2D.IgnoreCollision(mobCollider, actorCollider, true);
                }
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
                encounterPausedLinearVelocity = body.linearVelocity;
                encounterPausedAngularVelocity = body.angularVelocity;
                hasEncounterPausedVelocity = true;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = false;
                return;
            }
            body.simulated = true;
            if (!hasEncounterPausedVelocity) return;
            body.linearVelocity = encounterPausedLinearVelocity;
            body.angularVelocity = encounterPausedAngularVelocity;
            hasEncounterPausedVelocity = false;
        }

        public Vector2 NavigationOffset(Vector2 worldOffset) => worldOffset;

        public Vector2 NavigationDirection(Vector2 worldOffset)
        {
            var targetOffset = NavigationOffset(worldOffset);
            if (targetOffset.sqrMagnitude <= Mathf.Epsilon) return Vector2.zero;
            if (!IsFlying)
                return GroundNavigationDirection(targetOffset);
            if (navigationTiles == null)
                return WithNavigationFacing(
                    StabilizeNavigationDirection(targetOffset.normalized, targetOffset),
                    targetOffset.x);

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
                return WithNavigationFacing(
                    StabilizeNavigationDirection(targetOffset.normalized, targetOffset),
                    targetOffset.x);
            }
            if (directPathClear)
            {
                if (directPathClearSince < 0f) directPathClearSince = Time.unscaledTime;
                if (Time.unscaledTime - directPathClearSince >= DirectPathConfirmationSeconds)
                {
                    path.Clear();
                    pathIndex = 0;
                    directPathClearSince = -1f;
                    return WithNavigationFacing(
                        StabilizeNavigationDirection(targetOffset.normalized, targetOffset),
                        targetOffset.x);
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
                {
                    var routeCellDeltaX = path[pathIndex].x - startCell.x;
                    return WithNavigationFacing(
                        StabilizeNavigationDirection(waypointOffset.normalized, targetOffset),
                        routeCellDeltaX);
                }
                pathIndex++;
            }
            return WithNavigationFacing(
                StabilizeNavigationDirection(targetOffset.normalized, targetOffset),
                targetOffset.x);
        }

        private Vector2 GroundNavigationDirection(Vector2 targetOffset)
        {
            var directHorizontal = new Vector2(Mathf.Sign(targetOffset.x), 0f);
            if (navigationTiles == null || Mathf.Abs(targetOffset.y) <= 1f)
                return WithNavigationFacing(
                    Mathf.Abs(targetOffset.x) > .05f
                        ? directHorizontal
                        : Vector2.zero,
                    targetOffset.x);

            var current = body != null ? body.position : (Vector2)transform.position;
            if (Vector2.Distance(current, groundRouteLastProgressPosition) >= .15f)
            {
                groundRouteLastProgressPosition = current;
                groundRouteLastProgressTime = Time.unscaledTime;
            }
            var rawStartCell = ToCell(current);
            var startsOnStandingCell = IsGroundStandingCell(rawStartCell);
            var groundedOnTiles = IsGroundedOnTiles();
            if (groundedOnTiles)
                groundDropCommitted = false;
            // A drop edge is represented by a direct graph edge from the platform edge to
            // its lower landing. Only that edge may steer toward the landing while airborne.
            // Step jumps are also briefly airborne, but steering them toward the next
            // one-cell waypoint makes the direction reverse every time its center is crossed.
            if (!groundedOnTiles && groundDropCommitted)
            {
                var landingCenterX = CellCenter(groundDropLandingCell).x;
                var horizontalToLanding = landingCenterX - current.x;
                if (Mathf.Abs(horizontalToLanding) <= .05f)
                    return Vector2.zero;
                groundRouteDirection =
                    new Vector2(Mathf.Sign(horizontalToLanding), 0f);
                return groundRouteDirection;
            }
            if (!groundedOnTiles)
                return groundRouteDirection.sqrMagnitude > Mathf.Epsilon
                    ? groundRouteDirection
                    : directHorizontal;

            var startCell = startsOnStandingCell
                ? rawStartCell
                : ResolveGroundStandingCell(rawStartCell, 3);
            var targetCell = ResolveGroundTargetCell(ToCell(current + targetOffset));
            AdvanceGroundPath(startCell);
            var targetChanged =
                Manhattan(groundRouteTarget, targetCell) > PathTargetCellTolerance;
            var routeStuck = groundPathIndex < groundPath.Count &&
                             Time.unscaledTime - groundRouteLastProgressTime >=
                             GroundRouteStuckSeconds &&
                             IsGroundedOnTiles();
            var routeInvalid = groundPathIndex >= groundPath.Count ||
                               !IsGroundStandingCell(groundPath[groundPathIndex]) ||
                               Mathf.Abs(groundPath[groundPathIndex].x - startCell.x) > 1;
            if (routeStuck)
            {
                groundRouteTieBias *= -1;
                groundPath.Clear();
                groundPathIndex = 0;
                groundRouteReachesTarget = false;
                groundDropCommitted = false;
                routeInvalid = true;
                groundRouteLastProgressTime = Time.unscaledTime;
            }
            if (targetChanged ||
                routeInvalid && Time.unscaledTime >= nextGroundRouteRetryTime)
                RebuildGroundRoute(startCell, targetCell);

            if (groundPathIndex < groundPath.Count)
            {
                var nextCell = groundPath[groundPathIndex];
                var horizontalDelta = nextCell.x - startCell.x;
                if (horizontalDelta != 0)
                {
                    if (nextCell.y < startCell.y)
                    {
                        groundDropCommitted = true;
                        groundDropLandingCell = nextCell;
                    }
                    groundRouteDirection = new Vector2(Mathf.Sign(horizontalDelta), 0f);
                    return WithNavigationFacing(
                        groundRouteDirection, horizontalDelta);
                }
            }
            if (Mathf.Abs(targetOffset.x) > .05f)
                return WithNavigationFacing(directHorizontal, targetOffset.x);
            var transitionDirection =
                FindGroundTransitionDirection(startCell, targetCell);
            return WithNavigationFacing(
                transitionDirection, transitionDirection.x);
        }

        private void AdvanceGroundPath(Vector3Int currentCell)
        {
            // Game-clock movement can cross more than one logical cell between AI ticks.
            // Recover to any later waypoint that was reached instead of treating the next
            // (now behind the body) waypoint as an invalid route and selecting the other exit.
            for (var index = groundPathIndex; index < groundPath.Count; index++)
            {
                if (groundPath[index] != currentCell) continue;
                groundPathIndex = index + 1;
                return;
            }
        }

        private Vector3Int ResolveGroundStandingCell(Vector3Int preferred, int maximumDrop)
        {
            if (IsGroundStandingCell(preferred)) return preferred;
            for (var distance = 1; distance <= maximumDrop; distance++)
            {
                var below = preferred + Vector3Int.down * distance;
                if (IsGroundStandingCell(below)) return below;
            }
            return preferred;
        }

        private Vector3Int ResolveGroundTargetCell(Vector3Int preferred)
        {
            var directlyBelow =
                ResolveGroundStandingCell(preferred, GroundRouteMaximumDropCells);
            if (IsGroundStandingCell(directlyBelow)) return directlyBelow;
            for (var radius = 1; radius <= GroundRouteSearchMargin; radius++)
            {
                var left = ResolveGroundStandingCell(
                    preferred + Vector3Int.left * radius,
                    GroundRouteMaximumDropCells);
                var right = ResolveGroundStandingCell(
                    preferred + Vector3Int.right * radius,
                    GroundRouteMaximumDropCells);
                if (IsGroundStandingCell(left)) return left;
                if (IsGroundStandingCell(right)) return right;
            }
            return preferred;
        }

        private void RebuildGroundRoute(Vector3Int start, Vector3Int target)
        {
            groundPath.Clear();
            groundPathIndex = 0;
            groundRouteReachesTarget = false;
            groundDropCommitted = false;
            groundRouteTarget = target;
            nextGroundRouteRetryTime = Time.unscaledTime + GroundRouteRetrySeconds;
            if (!IsGroundStandingCell(start)) return;
            var minX = Mathf.Max(0, Mathf.Min(start.x, target.x) - GroundRouteSearchMargin);
            var maxX = Mathf.Min(
                navigationTiles.Width - 1,
                Mathf.Max(start.x, target.x) + GroundRouteSearchMargin);
            var minY = Mathf.Max(1, Mathf.Min(start.y, target.y) - GroundRouteSearchMargin);
            var maxY = Mathf.Min(
                navigationTiles.Height - 2,
                Mathf.Max(start.y, target.y) + GroundRouteSearchMargin);
            var open = new List<Vector3Int> { start };
            var openSet = new HashSet<Vector3Int> { start };
            var closed = new HashSet<Vector3Int>();
            var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
            var costs = new Dictionary<Vector3Int, float> { [start] = 0f };
            var best = start;
            var bestScore = GroundRouteScore(start, target);
            var preferredSign = target.x == start.x
                ? groundRouteTieBias
                : target.x > start.x ? 1 : -1;

            while (open.Count > 0 && closed.Count < GroundRouteMaximumVisited)
            {
                var bestOpenIndex = 0;
                var bestOpenScore = float.PositiveInfinity;
                for (var index = 0; index < open.Count; index++)
                {
                    var candidate = open[index];
                    var score = costs[candidate] + GroundRouteHeuristic(candidate, target);
                    if (score >= bestOpenScore) continue;
                    bestOpenScore = score;
                    bestOpenIndex = index;
                }
                var current = open[bestOpenIndex];
                open.RemoveAt(bestOpenIndex);
                openSet.Remove(current);
                closed.Add(current);
                var currentScore = GroundRouteScore(current, target);
                if (currentScore < bestScore)
                {
                    best = current;
                    bestScore = currentScore;
                }
                if (current == target)
                {
                    best = current;
                    groundRouteReachesTarget = true;
                    break;
                }

                AddGroundWalkAndStepNeighbours(
                    current, preferredSign, minX, maxX, minY, maxY,
                    open, openSet, closed, costs, cameFrom);
                AddGroundWalkAndStepNeighbours(
                    current, -preferredSign, minX, maxX, minY, maxY,
                    open, openSet, closed, costs, cameFrom);
                AddGroundDropNeighbour(
                    current, preferredSign, minX, maxX, minY, maxY,
                    open, openSet, closed, costs, cameFrom);
                AddGroundDropNeighbour(
                    current, -preferredSign, minX, maxX, minY, maxY,
                    open, openSet, closed, costs, cameFrom);
            }

            if (best == start || !cameFrom.ContainsKey(best)) return;
            var routeCell = best;
            while (routeCell != start)
            {
                groundPath.Add(routeCell);
                if (!cameFrom.TryGetValue(routeCell, out routeCell))
                {
                    groundPath.Clear();
                    groundRouteReachesTarget = false;
                    return;
                }
            }
            groundPath.Reverse();
            groundRouteLastProgressPosition =
                body != null ? body.position : (Vector2)transform.position;
            groundRouteLastProgressTime = Time.unscaledTime;
        }

        private void AddGroundWalkAndStepNeighbours(
            Vector3Int from,
            int horizontalSign,
            int minX,
            int maxX,
            int minY,
            int maxY,
            List<Vector3Int> open,
            HashSet<Vector3Int> openSet,
            HashSet<Vector3Int> closed,
            Dictionary<Vector3Int, float> costs,
            Dictionary<Vector3Int, Vector3Int> cameFrom)
        {
            var walk = from + Vector3Int.right * horizontalSign;
            TryAddGroundRouteNode(
                from, walk, 1f, minX, maxX, minY, maxY,
                open, openSet, closed, costs, cameFrom);
            // A step can begin underneath a platform edge when the body exits sideways
            // before rising. Requiring the cell directly above the source to be empty made
            // the route graph reject this physically reachable staircase shape even though
            // the collision-driven jump can clear the outside corner.
            var stepUp = walk + Vector3Int.up;
            TryAddGroundRouteNode(
                from, stepUp, 1.25f, minX, maxX, minY, maxY,
                open, openSet, closed, costs, cameFrom);
        }

        private void AddGroundDropNeighbour(
            Vector3Int from,
            int horizontalSign,
            int minX,
            int maxX,
            int minY,
            int maxY,
            List<Vector3Int> open,
            HashSet<Vector3Int> openSet,
            HashSet<Vector3Int> closed,
            Dictionary<Vector3Int, float> costs,
            Dictionary<Vector3Int, Vector3Int> cameFrom)
        {
            var x = from.x + horizontalSign;
            if (x < minX || x > maxX) return;
            var edgeCell = new Vector3Int(x, from.y, 0);
            if (!IsGroundBodyCellClear(edgeCell) ||
                !IsNavigationPassableCell(edgeCell + Vector3Int.down)) return;
            for (var drop = 1; drop <= GroundRouteMaximumDropCells; drop++)
            {
                var candidate = edgeCell + Vector3Int.down * drop;
                if (candidate.y < minY) return;
                if (!IsGroundStandingCell(candidate)) continue;
                TryAddGroundRouteNode(
                    from, candidate, 1f + drop * .15f,
                    minX, maxX, minY, maxY,
                    open, openSet, closed, costs, cameFrom);
                return;
            }
        }

        private void TryAddGroundRouteNode(
            Vector3Int from,
            Vector3Int candidate,
            float traversalCost,
            int minX,
            int maxX,
            int minY,
            int maxY,
            List<Vector3Int> open,
            HashSet<Vector3Int> openSet,
            HashSet<Vector3Int> closed,
            Dictionary<Vector3Int, float> costs,
            Dictionary<Vector3Int, Vector3Int> cameFrom)
        {
            if (candidate.x < minX || candidate.x > maxX ||
                candidate.y < minY || candidate.y > maxY ||
                closed.Contains(candidate) || !IsGroundStandingCell(candidate)) return;
            var nextCost = costs[from] + traversalCost;
            if (costs.TryGetValue(candidate, out var previousCost) &&
                nextCost >= previousCost) return;
            costs[candidate] = nextCost;
            cameFrom[candidate] = from;
            if (openSet.Add(candidate)) open.Add(candidate);
        }

        private Vector2 FindGroundTransitionDirection(Vector3Int start, Vector3Int target)
        {
            var wantsToDrop = target.y < start.y;
            var preferredDistance =
                FindGroundTransitionDistance(start, groundRouteTieBias, wantsToDrop);
            var oppositeDistance =
                FindGroundTransitionDistance(start, -groundRouteTieBias, wantsToDrop);
            if (preferredDistance > 0 &&
                (oppositeDistance <= 0 || preferredDistance <= oppositeDistance))
                return new Vector2(groundRouteTieBias, 0f);
            if (oppositeDistance > 0)
                return new Vector2(-groundRouteTieBias, 0f);
            return Vector2.zero;
        }

        private int FindGroundTransitionDistance(
            Vector3Int start,
            int horizontalSign,
            bool wantsToDrop)
        {
            for (var distance = 1; distance <= GroundRouteSearchMargin; distance++)
            {
                var candidate =
                    start + Vector3Int.right * (distance * horizontalSign);
                if (candidate.x < 0 || candidate.x >= navigationTiles.Width)
                    return -1;
                if (IsGroundStandingCell(candidate)) continue;
                if (wantsToDrop && HasGroundDropLanding(candidate))
                    return distance;
                if (!wantsToDrop)
                {
                    if (IsGroundStandingCell(candidate + Vector3Int.up))
                        return distance;
                    // Even when no complete route exists, approaching the first wall preserves
                    // the product rule that grounded pursuers visibly attempt a step jump.
                    if (!IsGroundBodyCellClear(candidate))
                        return distance;
                }
                return -1;
            }
            return -1;
        }

        private bool HasGroundDropLanding(Vector3Int edgeCell)
        {
            if (!IsGroundBodyCellClear(edgeCell) ||
                !IsNavigationPassableCell(edgeCell + Vector3Int.down)) return false;
            for (var drop = 1; drop <= GroundRouteMaximumDropCells; drop++)
                if (IsGroundStandingCell(edgeCell + Vector3Int.down * drop))
                    return true;
            return false;
        }

        private bool IsGroundStandingCell(Vector3Int cell) =>
            IsGroundBodyCellClear(cell) &&
            !IsNavigationPassableCell(cell + Vector3Int.down);

        private bool IsGroundBodyCellClear(Vector3Int cell)
        {
            if (!IsNavigationPassableCell(cell)) return false;
            if (attachedCollider == null) attachedCollider = GetComponent<Collider2D>();
            var colliderHeight = attachedCollider != null
                ? attachedCollider.bounds.size.y
                : .84f;
            return colliderHeight <= 1f - CollisionSkin * 2f ||
                   IsNavigationPassableCell(cell + Vector3Int.up);
        }

        private static int GroundRouteScore(Vector3Int cell, Vector3Int target) =>
            Mathf.Abs(cell.x - target.x) +
            Mathf.Abs(cell.y - target.y) * (GroundRouteSearchMargin * 2 + 1);

        private static float GroundRouteHeuristic(Vector3Int cell, Vector3Int target) =>
            Mathf.Abs(cell.x - target.x) +
            Mathf.Abs(cell.y - target.y) * .15f;

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

        private Vector2 WithNavigationFacing(Vector2 direction, float routeHorizontal)
        {
            // Facing follows the selected route segment, never collision correction,
            // waypoint-center overshoot, or the direct target bearing.
            if (Mathf.Abs(routeHorizontal) > Mathf.Epsilon)
                navigationFacingX = Mathf.Sign(routeHorizontal);
            return direction;
        }

        public bool HasClearAttackLine(Vector2 worldTarget)
        {
            if (navigationTiles == null) return true;
            return HasClearPointLine(ToCell(body != null ? body.position : (Vector2)transform.position),
                ToCell(worldTarget));
        }

        public float Move(Vector2 requestedDisplacement)
        {
            LastMoveDisplacement = Vector2.zero;
            if (body == null || !body.simulated || !IsFinite(requestedDisplacement)) return 0f;
            if (IsKnockbackActive) return 0f;
            if (IsFlying && body.linearVelocity.sqrMagnitude > Mathf.Epsilon)
                body.linearVelocity = Vector2.zero;
            if (!IsFlying) requestedDisplacement.y = 0f;
            var startPosition = body.position;
            var movedDistance = MoveWithCollisionCast(requestedDisplacement);
            LastMoveDisplacement = body.position - startPosition;
            return movedDistance;
        }

        public bool TryApplyKnockback(Vector2 requestedDisplacement)
        {
            if (body == null || !body.simulated || !IsFinite(requestedDisplacement) ||
                requestedDisplacement.sqrMagnitude <= Mathf.Epsilon) return false;
            body.linearVelocity = Vector2.zero;
            knockbackRemainingDisplacement = requestedDisplacement;
            knockbackRemainingSeconds = KnockbackDurationSeconds;
            return true;
        }

        private void Update() => TickKnockback(Time.deltaTime);

        public float TickKnockback(float deltaSeconds)
        {
            if (!IsKnockbackActive || deltaSeconds <= 0f || float.IsNaN(deltaSeconds) ||
                float.IsInfinity(deltaSeconds)) return 0f;

            var consumedSeconds = Mathf.Min(deltaSeconds, knockbackRemainingSeconds);
            var nextRemainingSeconds = Mathf.Max(0f, knockbackRemainingSeconds - consumedSeconds);
            var timeRatio = knockbackRemainingSeconds > Mathf.Epsilon
                ? nextRemainingSeconds / knockbackRemainingSeconds
                : 0f;
            var stepFraction = 1f - timeRatio * timeRatio;
            var requestedStep = knockbackRemainingDisplacement * stepFraction;
            var movedDistance = MoveWithCollisionCast(requestedStep);
            var requestedDistance = requestedStep.magnitude;

            knockbackRemainingDisplacement -= requestedStep;
            knockbackRemainingSeconds = nextRemainingSeconds;
            if (requestedDistance > Mathf.Epsilon &&
                movedDistance + CollisionSkin < requestedDistance)
            {
                knockbackRemainingDisplacement = Vector2.zero;
                knockbackRemainingSeconds = 0f;
            }
            else if (knockbackRemainingSeconds <= Mathf.Epsilon ||
                     knockbackRemainingDisplacement.sqrMagnitude <= Mathf.Epsilon)
            {
                knockbackRemainingDisplacement = Vector2.zero;
                knockbackRemainingSeconds = 0f;
            }
            return movedDistance;
        }

        private float MoveWithCollisionCast(Vector2 requestedDisplacement)
        {
            var requestedDistance = requestedDisplacement.magnitude;
            if (requestedDistance <= Mathf.Epsilon) return 0f;

            var direction = requestedDisplacement / requestedDistance;
            if (!IsFlying && Mathf.Abs(requestedDisplacement.y) <= Mathf.Epsilon)
                TryBeginStepJump(direction);

            ResolveInitialTerrainOverlap();
            var remainingDisplacement = requestedDisplacement;
            var movedDistance = 0f;
            for (var pass = 0; pass < MaximumCollisionSlidePasses; pass++)
            {
                var remainingDistance = remainingDisplacement.magnitude;
                if (remainingDistance <= Mathf.Epsilon) break;
                direction = remainingDisplacement / remainingDistance;
                var allowedDistance = remainingDistance;
                var blockingNormal = Vector2.zero;
                var hitCount = body.Cast(
                    direction, movementFilter, castHits, remainingDistance + CollisionSkin);
                for (var index = 0; index < hitCount; index++)
                {
                    var hit = castHits[index];
                    if (!IsBlockingHit(hit, direction)) continue;
                    var candidateDistance = Mathf.Max(0f, hit.distance - CollisionSkin);
                    if (candidateDistance >= allowedDistance) continue;
                    allowedDistance = candidateDistance;
                    blockingNormal = hit.normal;
                }

                if (allowedDistance > Mathf.Epsilon)
                {
                    body.position += direction * allowedDistance;
                    movedDistance += allowedDistance;
                    remainingDisplacement -= direction * allowedDistance;
                }
                if (blockingNormal.sqrMagnitude <= Mathf.Epsilon) break;

                remainingDisplacement =
                    ProjectAlongCollisionSurface(remainingDisplacement, blockingNormal);
            }
            return movedDistance;
        }

        private bool IsBlockingHit(RaycastHit2D hit, Vector2 direction)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform) ||
                ignoredCollisionColliders.Contains(hit.collider)) return false;
            // Contacts behind the requested movement (most commonly the floor or the side
            // the mob is leaving) must not freeze every cast from a touching position.
            return Vector2.Dot(direction, hit.normal) < -.001f;
        }

        private void ResolveInitialTerrainOverlap()
        {
            if (attachedCollider == null) attachedCollider = GetComponent<Collider2D>();
            if (attachedCollider == null) return;

            for (var pass = 0; pass < MaximumDepenetrationPasses; pass++)
            {
                var corrected = false;
                var overlapCount =
                    attachedCollider.Overlap(movementFilter, overlapHits);
                for (var index = 0; index < overlapCount; index++)
                {
                    var other = overlapHits[index];
                    if (other == null || other.transform.IsChildOf(transform) ||
                        ignoredCollisionColliders.Contains(other)) continue;
                    var distance = attachedCollider.Distance(other);
                    if (!distance.isOverlapped || distance.distance >= 0f ||
                        !IsFinite(distance.normal)) continue;
                    var correction = -distance.normal *
                                     (Mathf.Min(-distance.distance, CollisionSkin * 4f) +
                                      CollisionSkin);
                    if (!IsFinite(correction) ||
                        correction.sqrMagnitude <= Mathf.Epsilon) continue;
                    body.position += correction;
                    corrected = true;
                }
                if (!corrected) break;
            }
        }

        public static Vector2 ProjectAlongCollisionSurface(
            Vector2 displacement,
            Vector2 surfaceNormal)
        {
            if (!IsFinite(displacement) || !IsFinite(surfaceNormal) ||
                surfaceNormal.sqrMagnitude <= Mathf.Epsilon) return Vector2.zero;
            surfaceNormal.Normalize();
            var intoSurface = Vector2.Dot(displacement, surfaceNormal);
            return intoSurface < 0f
                ? displacement - surfaceNormal * intoSurface
                : displacement;
        }

        private bool TryBeginStepJump(Vector2 direction)
        {
            if (navigationTiles == null || body == null || Mathf.Abs(direction.x) <= .1f ||
                Time.unscaledTime < nextStepJumpTime || !IsGroundedOnTiles()) return false;
            if (attachedCollider == null) attachedCollider = GetComponent<Collider2D>();
            var bounds = attachedCollider != null
                ? attachedCollider.bounds
                : new Bounds(body.position, Vector3.one * .8f);
            var extents = bounds.extents;
            var horizontalSign = direction.x > 0f ? 1 : -1;
            // Probe from the collider centre, not the Rigidbody pivot. Ground bosses use a
            // bottom pivot with an upward collider offset, while ordinary yokai use a centred
            // pivot; body.position therefore does not represent the same physical point.
            var forwardProbe = (Vector2)bounds.center +
                               Vector2.right * horizontalSign * (extents.x + .08f);
            var blockingCell = ToCell(forwardProbe);
            if (IsAirCell(blockingCell)) return false;

            // Jump even when the obstacle is taller than one tile. Collision still prevents the
            // mob from passing through an uncleared wall, but repeated attempts look intentional
            // instead of leaving grounded pursuers walking against it forever.
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
            var groundProbe = attachedCollider != null
                ? new Vector2(attachedCollider.bounds.center.x,
                    attachedCollider.bounds.min.y - GroundProbeDepth)
                : body.position + Vector2.down * (.4f + GroundProbeDepth);
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
                if (!IsNavigationPassableCell(new Vector3Int(x, y, 0))) return false;
            return true;
        }

        public bool IsNavigationPassableCell(Vector3Int cell)
        {
            if (navigationTiles == null || !navigationTiles.InBounds(cell)) return false;
            if (navigationTiles.GetTile(cell).IsAir || navigationTiles.IsDoorOpen(cell)) return true;
            return canDestroyWallMaterial != null &&
                   navigationTiles.TryGetDamageableWallMaterial(cell, out var material) &&
                   canDestroyWallMaterial(material);
        }

        private bool IsAirCell(Vector3Int cell) =>
            navigationTiles != null && navigationTiles.InBounds(cell) && navigationTiles.GetTile(cell).IsAir;

        private Vector3Int ToCell(Vector2 position) => navigationTiles != null
            ? navigationTiles.WorldToCell(position)
            : new Vector3Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), 0);

        private Vector2 CellCenter(Vector3Int cell) => navigationTiles != null
            ? navigationTiles.GetCellCenterWorld(cell)
            : new Vector2(cell.x + .5f, cell.y + .5f);

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
