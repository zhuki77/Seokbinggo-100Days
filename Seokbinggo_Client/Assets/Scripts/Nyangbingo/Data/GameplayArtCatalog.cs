using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyangbingo.Data
{
    [CreateAssetMenu(menuName = "Nyangbingo/Data/Gameplay Art Catalog")]
    public sealed class GameplayArtCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class WeaponAttackArt
        {
            [SerializeField] private string itemId;
            [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
            public string ItemId => itemId;
            public IReadOnlyList<Sprite> Frames => frames ?? Array.Empty<Sprite>();
        }

        [SerializeField] private WeaponAttackArt[] weaponAttackAnimations = Array.Empty<WeaponAttackArt>();
        [SerializeField] private Sprite arrowProjectile;
        [SerializeField] private Sprite slingStoneProjectile;
        [SerializeField] private Sprite[] fanWindFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] openingIllustrations = Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> OpeningIllustrations => openingIllustrations ?? Array.Empty<Sprite>();

        public IReadOnlyList<Sprite> FindWeaponAttackFrames(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || weaponAttackAnimations == null) return Array.Empty<Sprite>();
            foreach (var animation in weaponAttackAnimations)
                if (animation != null && string.Equals(animation.ItemId, itemId, StringComparison.Ordinal))
                    return animation.Frames;
            return Array.Empty<Sprite>();
        }

        public Sprite ArrowProjectile => arrowProjectile;
        public Sprite SlingStoneProjectile => slingStoneProjectile;
        public IReadOnlyList<Sprite> FanWindFrames => fanWindFrames ?? Array.Empty<Sprite>();

        [SerializeField] private Sprite[] temperatureFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] playerAttackFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] miningCrackFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] miningBreakFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] miningCriticalFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] bossWarningFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] gangcheoriSpecialFireFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] playerFireHitFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] imugiElectricAttackFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] blueProjectileFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] dayCounterFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] dayNightClockFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] yokaiTearBalanceFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] fuelGaugeFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] saveIndicatorFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] playerVitalsFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] button1x1Frames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] button1x2Frames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] button1x4Frames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] button1x6Frames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] shellNumberGlyphs = Array.Empty<Sprite>();
        [SerializeField] private Sprite dangerIcon;
        [SerializeField] private Sprite yokaiDamageIcon;
        [SerializeField] private Sprite burnStatusIcon;
        [SerializeField] private Sprite sealLeakStatusIcon;
        [SerializeField] private Sprite sealLeakStaticMarker;
        [SerializeField] private Sprite goalDirectionArrow;
        [SerializeField] private Sprite[] sealLeakMarkerFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite sealRepairCheck;
        [SerializeField] private Sprite[] sealSuccessFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite hypothermiaStatusIcon;
        [SerializeField] private Sprite hypothermiaStatusIconAlert;
        [SerializeField] private Sprite nightSpawnBlockedIcon;
        [SerializeField] private Sprite deathTearPouch;
        [SerializeField] private Sprite bossWarningLarge;
        [SerializeField] private Sprite bossWarningSmall;
        [SerializeField] private Sprite bossHealthFrame;
        [SerializeField] private Sprite bossHealthGaekgwi;
        [SerializeField] private Sprite resultContinue;
        [SerializeField] private Sprite bossHealthGangcheol;
        [SerializeField] private Sprite bossHealthKingDokkaebi;
        [SerializeField] private Sprite bossHealthMotherBulgasari;
        [SerializeField] private Sprite bossHealthImugi;
        [SerializeField] private Sprite inventoryPanel;
        [SerializeField] private Sprite inventorySlot;
        [SerializeField] private Sprite inventorySlotSelected;
        [SerializeField] private Sprite inventorySlotTopSelected;
        [SerializeField] private Sprite equipmentCharacter;
        [SerializeField] private Sprite equipmentHeadSlot;
        [SerializeField] private Sprite equipmentHeadSlotSelected;
        [SerializeField] private Sprite equipmentBodySlot;
        [SerializeField] private Sprite equipmentBodySlotSelected;
        [SerializeField] private Sprite equipmentFeetSlot;
        [SerializeField] private Sprite equipmentFeetSlotSelected;
        [SerializeField] private Sprite equipmentAccessorySlot;
        [SerializeField] private Sprite equipmentAccessorySlotSelected;
        [SerializeField] private Sprite activeItemSlot;
        [SerializeField] private Sprite activeItemSlotSelected;
        [SerializeField] private Sprite tilePaletteSlotSelected;
        [SerializeField] private Sprite jangdokStorageGrid;
        [SerializeField] private Sprite codexCard;
        [SerializeField] private Sprite shellTitleLogo;
        [SerializeField] private Sprite shellStart;
        [SerializeField] private Sprite shellContinue;
        [SerializeField] private Sprite shellResume;
        [SerializeField] private Sprite shellSave;
        [SerializeField] private Sprite shellSettings;
        [SerializeField] private Sprite shellLeave;
        [SerializeField] private Sprite shellReturnTitle;
        [SerializeField] private Sprite shellApply;
        [SerializeField] private Sprite shellBack;
        [SerializeField] private Sprite shellBgmLabel;
        [SerializeField] private Sprite shellSfxLabel;
        [SerializeField] private Sprite shellPauseTitle;
        [SerializeField] private Sprite shellPauseIcon;
        [SerializeField] private Sprite shellPlayIcon;
        [SerializeField] private Sprite shellCheckOn;
        [SerializeField] private Sprite shellCheckOff;
        [SerializeField] private Sprite shellSpeakerHigh;
        [SerializeField] private Sprite shellSpeakerLow;
        [SerializeField] private Sprite shellSpeakerMuted;
        [SerializeField] private Sprite shellVolumeBar;
        [SerializeField] private Sprite shellVolumeHandle;
        [SerializeField] private Sprite[] shellLoadingFrames = Array.Empty<Sprite>();

        public IReadOnlyList<Sprite> TemperatureFrames => temperatureFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> PlayerAttackFrames => playerAttackFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> MiningCrackFrames => miningCrackFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> MiningBreakFrames => miningBreakFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> MiningCriticalFrames => miningCriticalFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> BossWarningFrames => bossWarningFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> GangcheoriSpecialFireFrames =>
            gangcheoriSpecialFireFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> PlayerFireHitFrames =>
            playerFireHitFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> ImugiElectricAttackFrames =>
            imugiElectricAttackFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> BlueProjectileFrames => blueProjectileFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> DayCounterFrames => dayCounterFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> DayNightClockFrames => dayNightClockFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> YokaiTearBalanceFrames => yokaiTearBalanceFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> FuelGaugeFrames => fuelGaugeFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> SaveIndicatorFrames => saveIndicatorFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> PlayerVitalsFrames => playerVitalsFrames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> Button1x1Frames => button1x1Frames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> Button1x2Frames => button1x2Frames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> Button1x4Frames => button1x4Frames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> Button1x6Frames => button1x6Frames ?? Array.Empty<Sprite>();
        public IReadOnlyList<Sprite> ShellNumberGlyphs => shellNumberGlyphs ?? Array.Empty<Sprite>();
        public Sprite DangerIcon => dangerIcon;
        public Sprite YokaiDamageIcon => yokaiDamageIcon;
        public Sprite BurnStatusIcon => burnStatusIcon;
        public Sprite SealLeakStatusIcon => sealLeakStatusIcon;
        public Sprite SealLeakStaticMarker => sealLeakStaticMarker;
        public Sprite GoalDirectionArrow => goalDirectionArrow;
        public IReadOnlyList<Sprite> SealLeakMarkerFrames => sealLeakMarkerFrames ?? Array.Empty<Sprite>();
        public Sprite SealRepairCheck => sealRepairCheck;
        public IReadOnlyList<Sprite> SealSuccessFrames => sealSuccessFrames ?? Array.Empty<Sprite>();
        public Sprite HypothermiaStatusIcon => hypothermiaStatusIcon;
        // 저체온 피해 임박 시 쓰는 경고 변형(HypothermiaStatus_Icon2). 없으면 기본 아이콘을 쓴다.
        public Sprite HypothermiaStatusIconAlert => hypothermiaStatusIconAlert;
        public Sprite NightSpawnBlockedIcon => nightSpawnBlockedIcon;
        public Sprite DeathTearPouch => deathTearPouch;
        public Sprite BossWarningLarge => bossWarningLarge;
        public Sprite BossWarningSmall => bossWarningSmall;
        public Sprite BossHealthFrame => bossHealthFrame;
        public Sprite BossHealthGaekgwi => bossHealthGaekgwi;
        public Sprite ResultContinue => resultContinue;
        public Sprite BossHealthGangcheol => bossHealthGangcheol;
        public Sprite BossHealthKingDokkaebi => bossHealthKingDokkaebi;
        public Sprite BossHealthMotherBulgasari => bossHealthMotherBulgasari;
        public Sprite BossHealthImugi => bossHealthImugi;
        public Sprite InventoryPanel => inventoryPanel;
        public Sprite InventorySlot => inventorySlot;
        public Sprite InventorySlotSelected => inventorySlotSelected;
        public Sprite InventorySlotTopSelected => inventorySlotTopSelected;
        public Sprite EquipmentCharacter => equipmentCharacter;
        public Sprite EquipmentHeadSlot => equipmentHeadSlot;
        public Sprite EquipmentHeadSlotSelected => equipmentHeadSlotSelected;
        public Sprite EquipmentBodySlot => equipmentBodySlot;
        public Sprite EquipmentBodySlotSelected => equipmentBodySlotSelected;
        public Sprite EquipmentFeetSlot => equipmentFeetSlot;
        public Sprite EquipmentFeetSlotSelected => equipmentFeetSlotSelected;
        public Sprite EquipmentAccessorySlot => equipmentAccessorySlot;
        public Sprite EquipmentAccessorySlotSelected => equipmentAccessorySlotSelected;
        public Sprite ActiveItemSlot => activeItemSlot;
        public Sprite ActiveItemSlotSelected => activeItemSlotSelected;
        public Sprite TilePaletteSlotSelected => tilePaletteSlotSelected;
        public Sprite JangdokStorageGrid => jangdokStorageGrid;
        public Sprite CodexCard => codexCard;
        public Sprite ShellTitleLogo => shellTitleLogo;
        public Sprite ShellStart => shellStart;
        public Sprite ShellContinue => shellContinue;
        public Sprite ShellResume => shellResume;
        public Sprite ShellSave => shellSave;
        public Sprite ShellSettings => shellSettings;
        public Sprite ShellLeave => shellLeave;
        public Sprite ShellReturnTitle => shellReturnTitle;
        public Sprite ShellApply => shellApply;
        public Sprite ShellBack => shellBack;
        public Sprite ShellBgmLabel => shellBgmLabel;
        public Sprite ShellSfxLabel => shellSfxLabel;
        public Sprite ShellPauseTitle => shellPauseTitle;
        public Sprite ShellPauseIcon => shellPauseIcon;
        public Sprite ShellPlayIcon => shellPlayIcon;
        public Sprite ShellCheckOn => shellCheckOn;
        public Sprite ShellCheckOff => shellCheckOff;
        public Sprite ShellSpeakerHigh => shellSpeakerHigh;
        public Sprite ShellSpeakerLow => shellSpeakerLow;
        public Sprite ShellSpeakerMuted => shellSpeakerMuted;
        public Sprite ShellVolumeBar => shellVolumeBar;
        public Sprite ShellVolumeHandle => shellVolumeHandle;
        public IReadOnlyList<Sprite> ShellLoadingFrames => shellLoadingFrames ?? Array.Empty<Sprite>();
    }
}
