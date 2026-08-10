namespace Nyangbingo.Core
{
    public enum CraftingStation { None, Workbench, Furnace, IceAnvil, Foundry, Smithy }
    public enum YokaiKind
    {
        ClubGoblin,
        Bulgasari,
        Yagwanggwi,
        Eoduksini,
        Gangcheori,
        Gaekgwi,
        Imugi
    }
    public enum BossKind { GoblinChief, MotherBulgasari, Imugi, Gangcheori }
    public enum DamageTag { Melee, Fire, Ice, Fall }
    public enum DamageDelivery { Direct, DamageOverTime, Structure, Environmental }
    public enum EquipmentSlot { Head, Body, Feet, AccessoryOne, AccessoryTwo }
    public enum UtilityKind { Hapjukseon, BellRope }
    public enum SmeltingStationKind { Furnace, Foundry }
    public enum CounterAuraKind { Lantern, Sieve, Haetae, BellRope }
    public enum ChestRegion { Ruins, Upper, Middle, Deep }
}
