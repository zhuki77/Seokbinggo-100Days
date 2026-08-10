using Nyangbingo.Data;

namespace Nyangbingo.Inventory
{
    public sealed class StatSheet
    {
        public int Defense { get; private set; }
        public float MovementMultiplier { get; private set; } = 1f;
        public float MiningCriticalChance { get; private set; }
        public float TemperatureRiseModifier { get; private set; }
        public float FireDamageModifier { get; private set; }
        public bool HasDoubleJump { get; private set; }
        public float DoubleJumpHeightRatio { get; private set; }
        public float VisionRadiusBonus { get; private set; }
        public bool BlocksInventoryTheft { get; private set; }

        public void Recalculate(EquipmentSystem equipment)
        {
            Defense = 0; MovementMultiplier = 1f; MiningCriticalChance = 0f; TemperatureRiseModifier = 0f;
            FireDamageModifier = 0f; HasDoubleJump = false; DoubleJumpHeightRatio = 0f;
            VisionRadiusBonus = 0f; BlocksInventoryTheft = false;
            if (equipment == null) return;
            long defenseTotal = 0;
            double movementTotal = 1d;
            double miningCriticalTotal = 0d;
            double temperatureTotal = 0d;
            double fireTotal = 0d;
            double visionTotal = 0d;
            foreach (var pair in equipment.Export())
            {
                var item = pair.Value; if (item == null) continue;
                defenseTotal = System.Math.Min(int.MaxValue, defenseTotal + System.Math.Max(0, item.Defense));
                if (IsFinite(item.MovementBonus)) movementTotal += item.MovementBonus;
                if (IsFinite(item.MiningCriticalBonus)) miningCriticalTotal += item.MiningCriticalBonus;
                if (IsFinite(item.TemperatureRiseModifier)) temperatureTotal += item.TemperatureRiseModifier;
                if (IsFinite(item.FireDamageModifier)) fireTotal += item.FireDamageModifier;
                HasDoubleJump |= item.GrantsDoubleJump;
                if (item.GrantsDoubleJump && IsFinite(item.DoubleJumpHeightRatio))
                    DoubleJumpHeightRatio = UnityEngine.Mathf.Max(DoubleJumpHeightRatio, item.DoubleJumpHeightRatio);
                if (IsFinite(item.VisionRadiusBonus)) visionTotal += item.VisionRadiusBonus;
                BlocksInventoryTheft |= item.BlocksInventoryTheft;
            }
            var head = equipment.Get(Nyangbingo.Core.EquipmentSlot.Head);
            var body = equipment.Get(Nyangbingo.Core.EquipmentSlot.Body);
            var feet = equipment.Get(Nyangbingo.Core.EquipmentSlot.Feet);
            if (IsCompleteSet(head, body, feet))
            {
                if (IsFinite(head.SetTemperatureRiseModifier)) temperatureTotal += head.SetTemperatureRiseModifier;
                if (IsFinite(head.SetFireDamageModifier)) fireTotal += head.SetFireDamageModifier;
            }
            Defense = (int)defenseTotal;
            MovementMultiplier = UnityEngine.Mathf.Max(0f, ToFiniteFloat(movementTotal));
            MiningCriticalChance = UnityEngine.Mathf.Clamp(ToFiniteFloat(miningCriticalTotal), 0f, .25f);
            TemperatureRiseModifier = UnityEngine.Mathf.Max(ArmorSetRules.TemperatureRiseFloor,
                ToFiniteFloat(temperatureTotal));
            FireDamageModifier = ToFiniteFloat(fireTotal);
            VisionRadiusBonus = UnityEngine.Mathf.Max(0f, ToFiniteFloat(visionTotal));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsCompleteSet(EquipmentDefinition head, EquipmentDefinition body, EquipmentDefinition feet)
        {
            return head != null && body != null && feet != null && !string.IsNullOrWhiteSpace(head.SetId) &&
                   string.Equals(head.SetId, body.SetId, System.StringComparison.Ordinal) &&
                   string.Equals(head.SetId, feet.SetId, System.StringComparison.Ordinal);
        }

        private static float ToFiniteFloat(double value)
        {
            if (double.IsNaN(value)) return 0f;
            if (value >= float.MaxValue) return float.MaxValue;
            if (value <= -float.MaxValue) return -float.MaxValue;
            return (float)value;
        }
    }
}
