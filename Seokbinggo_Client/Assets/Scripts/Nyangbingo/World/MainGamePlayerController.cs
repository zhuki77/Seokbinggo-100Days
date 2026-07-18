using Nyangbingo.Combat;
using Nyangbingo.Data;
using Nyangbingo.Inventory;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nyangbingo.World
{
    [DefaultExecutionOrder(-60)]
    [RequireComponent(typeof(Health), typeof(Rigidbody2D), typeof(CircleCollider2D))]
    [RequireComponent(typeof(MeleeArcAttack), typeof(SpriteRenderer))]
    public sealed class MainGamePlayerController : MonoBehaviour
    {
        private const string MoveSpeedKey = "player_move_speed";
        private const string BareClawId = "bare_claw";
        private const string HapjukseonId = "hapjukseon";
        private const string CheolseonId = "cheolseon";

        [SerializeField] private GameDataCatalog catalog;
        [SerializeField] private MainGameBootstrap bootstrap;
        [SerializeField] private MainGameRuntimeServices runtimeServices;
        [SerializeField] private Camera followCamera;
        [Min(0f)][SerializeField] private float cameraFollowSharpness = 12f;

        private readonly StatSheet statSheet = new StatSheet();
        private Rigidbody2D body;
        private Health health;
        private MeleeArcAttack attack;
        private WireSnareAbility wireSnare;
        private Vector2 movementInput;
        private Vector2 facing = Vector2.down;
        private float baseMoveSpeed;
        private float currentMoveSpeed;
        private float attackCooldown;
        private CombatProfileDefinition activeProfile;
        private SpriteRenderer attackIndicator;
        private float attackIndicatorRemaining;
        private bool loggedFirstAttackInput;
        private bool loggedFirstAttackHit;
        private bool dead;
        private bool initialized;

        public bool IsInitialized => initialized;
        public string ActiveCombatProfileId => activeProfile != null ? activeProfile.Id : string.Empty;
        public float CurrentMoveSpeed => currentMoveSpeed;
        public bool IsDead => dead;

        public void ConfigureForScene(GameDataCatalog gameDataCatalog, MainGameBootstrap mainBootstrap,
            MainGameRuntimeServices services, Camera camera)
        {
            catalog = gameDataCatalog;
            bootstrap = mainBootstrap;
            runtimeServices = services;
            followCamera = camera;
        }

        private void Start() => Initialize();

        public bool Initialize()
        {
            if (initialized) return true;
            bootstrap ??= GetComponentInParent<MainGameBootstrap>();
            runtimeServices ??= GetComponentInParent<MainGameRuntimeServices>();
            if (catalog == null) catalog = bootstrap != null ? bootstrap.GameDataCatalog : null;
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            attack = GetComponent<MeleeArcAttack>();
            followCamera ??= Camera.main;

            var moveSpeedDefinition = catalog != null ? catalog.FindGlobal(MoveSpeedKey) : null;
            var defaultProfile = catalog != null ? catalog.FindCombatProfile(BareClawId) : null;
            if (catalog == null || bootstrap == null || runtimeServices == null ||
                !runtimeServices.Initialize() || body == null || health == null || attack == null ||
                moveSpeedDefinition == null || !moveSpeedDefinition.TryGetFloat(out baseMoveSpeed) ||
                baseMoveSpeed <= 0f || defaultProfile == null)
            {
                Debug.LogError("[Nyangbingo] MainGamePlayerController: 플레이어 이동·전투 필수 데이터가 준비되지 않았습니다.");
                return false;
            }

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            var collider = GetComponent<CircleCollider2D>();
            collider.radius = .38f;
            collider.isTrigger = false;
            RuntimePlaceholderVisual.Configure(GetComponent<SpriteRenderer>(), new Color(.25f, .85f, 1f), .8f, 20);
            var indicatorObject = new GameObject("AttackIndicator");
            indicatorObject.transform.SetParent(transform, false);
            attackIndicator = indicatorObject.AddComponent<SpriteRenderer>();
            RuntimePlaceholderVisual.Configure(attackIndicator, new Color(1f, .9f, .2f, .75f), .65f, 19);
            attackIndicator.enabled = false;

            runtimeServices.PlayerInventory.Changed += RefreshCombatProfile;
            runtimeServices.EquipmentSystem.Changed += RefreshEquipmentStats;
            health.Died += HandleDied;
            wireSnare = new WireSnareAbility(attack);
            RefreshEquipmentStats();
            RefreshCombatProfile();
            initialized = activeProfile != null;
            if (initialized)
                Debug.Log($"[Nyangbingo] MainGamePlayerController: 이동·체력·근접 공격·카메라 연결 완료 " +
                          $"(speed={currentMoveSpeed:0.##}, profile={ActiveCombatProfileId}).");
            return initialized;
        }

        private void Update()
        {
            if (!initialized) return;
            if (dead)
            {
                movementInput = Vector2.zero;
                if (Input.GetKeyDown(KeyCode.R))
                {
                    Time.timeScale = 1f;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
                return;
            }
            movementInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (movementInput.sqrMagnitude > 1f) movementInput.Normalize();
            if (movementInput.sqrMagnitude > Mathf.Epsilon) facing = movementInput.normalized;

            attackCooldown = Mathf.Max(0f, attackCooldown - Time.deltaTime);
            wireSnare.Tick(Time.deltaTime);
            if (attackIndicatorRemaining > 0f)
            {
                attackIndicatorRemaining = Mathf.Max(0f, attackIndicatorRemaining - Time.deltaTime);
                if (attackIndicatorRemaining <= 0f) attackIndicator.enabled = false;
            }
            if (attackCooldown <= 0f && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)))
                TryBasicAttack();
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.E))
                TryFanAbility();
        }

        private void FixedUpdate()
        {
            if (!initialized || movementInput.sqrMagnitude <= Mathf.Epsilon) return;
            var destination = body.position + movementInput * (currentMoveSpeed * Time.fixedDeltaTime);
            if (bootstrap.TileService != null)
            {
                destination.x = Mathf.Clamp(destination.x, .5f, bootstrap.TileService.Width - .5f);
                destination.y = Mathf.Clamp(destination.y, .5f, bootstrap.TileService.Height - .5f);
            }
            body.MovePosition(destination);
        }

        private void LateUpdate()
        {
            if (!initialized || followCamera == null) return;
            var current = followCamera.transform.position;
            var target = new Vector3(transform.position.x, transform.position.y, current.z);
            var factor = cameraFollowSharpness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-cameraFollowSharpness * Time.unscaledDeltaTime);
            followCamera.transform.position = Vector3.Lerp(current, target, factor);
        }

        private void TryBasicAttack()
        {
            if (activeProfile == null || !activeProfile.HasBasicAttack || activeProfile.AttacksPerSecond <= 0f) return;
            attack.Strike(facing);
            ShowAttackFeedback();
            attackCooldown = 1f / activeProfile.AttacksPerSecond;
            if (!loggedFirstAttackInput || attack.LastHitCount > 0 && !loggedFirstAttackHit)
            {
                Debug.Log($"[Nyangbingo] Player attack accepted (profile={activeProfile.Id}, hits={attack.LastHitCount}).");
                loggedFirstAttackInput = true;
                loggedFirstAttackHit |= attack.LastHitCount > 0;
            }
        }

        private void TryFanAbility()
        {
            if (activeProfile == null ||
                (activeProfile.Id != HapjukseonId && activeProfile.Id != CheolseonId)) return;
            if (wireSnare.TryUse(facing)) ShowAttackFeedback();
        }

        private void ShowAttackFeedback()
        {
            if (attackIndicator == null) return;
            attackIndicator.transform.localPosition = facing * .85f;
            attackIndicator.transform.right = facing;
            attackIndicator.enabled = true;
            attackIndicatorRemaining = .12f;
        }

        private void RefreshEquipmentStats()
        {
            if (runtimeServices?.EquipmentSystem == null || health == null) return;
            statSheet.Recalculate(runtimeServices.EquipmentSystem);
            currentMoveSpeed = baseMoveSpeed * statSheet.MovementMultiplier;
            health.SetDefense(statSheet.Defense);
        }

        private void RefreshCombatProfile()
        {
            var inventory = runtimeServices?.PlayerInventory;
            var profileId = inventory != null && inventory.Count(CheolseonId) > 0
                ? CheolseonId
                : inventory != null && inventory.Count(HapjukseonId) > 0
                    ? HapjukseonId
                    : BareClawId;
            var profile = catalog != null ? catalog.FindCombatProfile(profileId) : null;
            if (profile == null || !attack.ConfigureForRuntime(transform, ~0, profile)) return;
            activeProfile = profile;
            attackCooldown = 0f;
        }

        private void HandleDied()
        {
            if (dead) return;
            dead = true;
            movementInput = Vector2.zero;
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.color = new Color(.35f, .35f, .4f);
            Nyangbingo.Core.GameEvents.RaisePlayerDied();
            Debug.Log("[Nyangbingo] MainGamePlayerController: 플레이어 사망. R 키로 현재 월드를 재시작할 수 있습니다.");
        }

        private void OnDestroy()
        {
            if (runtimeServices?.PlayerInventory != null)
                runtimeServices.PlayerInventory.Changed -= RefreshCombatProfile;
            if (runtimeServices?.EquipmentSystem != null)
                runtimeServices.EquipmentSystem.Changed -= RefreshEquipmentStats;
            if (health != null) health.Died -= HandleDied;
        }
    }

    internal static class RuntimePlaceholderVisual
    {
        private static Sprite sprite;

        public static void Configure(SpriteRenderer renderer, Color color, float size, int sortingOrder)
        {
            if (renderer == null) return;
            if (sprite == null)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "NyangbingoRuntimePlaceholderTexture",
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(.5f, .5f), 1f);
                sprite.name = "NyangbingoRuntimePlaceholderSprite";
                sprite.hideFlags = HideFlags.HideAndDontSave;
            }
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            renderer.transform.localScale = new Vector3(size, size, 1f);
        }
    }

    internal sealed class RuntimeDamageFlash : MonoBehaviour
    {
        private Health health;
        private SpriteRenderer spriteRenderer;
        private Color baseColor;
        private float remaining;

        private void Awake()
        {
            health = GetComponent<Health>() ?? GetComponentInParent<Health>();
            spriteRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null) baseColor = spriteRenderer.color;
        }

        private void OnEnable()
        {
            if (health == null) health = GetComponent<Health>() ?? GetComponentInParent<Health>();
            if (health != null) health.Damaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= HandleDamaged;
        }

        private void HandleDamaged(Nyangbingo.Core.DamageTag tag, int amount)
        {
            if (spriteRenderer == null || amount <= 0) return;
            spriteRenderer.color = Color.white;
            remaining = .1f;
        }

        private void Update()
        {
            if (remaining <= 0f) return;
            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            if (remaining <= 0f && spriteRenderer != null) spriteRenderer.color = baseColor;
        }
    }
}
