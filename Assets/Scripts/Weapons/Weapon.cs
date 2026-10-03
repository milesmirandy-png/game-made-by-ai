using System.Collections.Generic;

namespace Swat
{
    // A weapon being carried: its settings, attachments and remaining ammo.
    public class Weapon
    {
        public WeaponData Data { get; private set; }
        public int Magazine { get; set; }
        public int Reserve { get; set; }
        public FireMode Mode { get; private set; }
        public bool Automatic { get { return Mode == FireMode.FullAuto; } }
        public bool Burst { get { return Mode == FireMode.Burst; } }
        public float SpreadMultiplier { get; private set; }
        public float RecoilMultiplier { get; private set; }
        public float NoiseMultiplier { get; private set; }
        public float MoveMultiplier { get; private set; }
        public float LightRangeMultiplier { get; private set; }

        public Weapon(WeaponData data, OfficerLoadout loadout)
        {
            Data = data;
            Magazine = data.magazineSize;
            Reserve = data.startingReserve;
            Mode = data.fireMode;
            SpreadMultiplier = RecoilMultiplier = NoiseMultiplier = MoveMultiplier = LightRangeMultiplier = 1f;
            if (loadout == null || data.isSidearm) return;
            foreach (var id in new[] { loadout.lightId, loadout.opticId, loadout.muzzleId, loadout.stockId })
            {
                var attachment = GameData.Attachment(id);
                if (attachment == null) continue;
                SpreadMultiplier *= attachment.spreadMultiplier;
                RecoilMultiplier *= attachment.recoilMultiplier;
                NoiseMultiplier *= attachment.noiseMultiplier;
                MoveMultiplier *= attachment.moveMultiplier;
                LightRangeMultiplier *= attachment.lightRangeMultiplier;
            }
        }

        public bool CanReload { get { return Magazine < Data.magazineSize && Reserve > 0; } }
        public float Spread { get { return Data.spread * SpreadMultiplier; } }
        public float Recoil { get { return Data.recoil * RecoilMultiplier; } }
        public float NoiseRadius { get { return Data.noiseRadius * NoiseMultiplier; } }

        public void FinishReload()
        {
            int taken = System.Math.Min(Data.magazineSize - Magazine, Reserve);
            Magazine += taken;
            Reserve -= taken;
        }

        // Automatic and burst weapons that allow it switch to semi-automatic and back.
        public bool ToggleFireMode()
        {
            if (!Data.canToggleFireMode || Data.fireMode == FireMode.SemiAuto) return false;
            Mode = Mode == FireMode.SemiAuto ? Data.fireMode : FireMode.SemiAuto;
            return true;
        }

        public string ModeName { get { return Mode == FireMode.FullAuto ? "AUTO" : Mode == FireMode.Burst ? Data.burstCount + "-RND BURST" : "SEMI"; } }

        // Fills the magazine and reserve back to what the weapon started with (game-mode respawns).
        public void Refill()
        {
            Magazine = Data.magazineSize;
            Reserve = Data.startingReserve;
        }
    }

    // A stack of one kind of equipment.
    public class EquipmentSlot
    {
        public EquipmentData Data { get; private set; }
        public int Count { get; set; }
        public int Used { get; set; }

        public EquipmentSlot(EquipmentData data, int count)
        {
            Data = data;
            Count = count;
        }
    }

    // Everything one officer carries: primary, sidearm and equipment.
    public class WeaponInventory
    {
        public Weapon Primary { get; private set; }
        public Weapon Sidearm { get; private set; }
        public bool PrimaryBlocked { get; private set; } // carrying a shield
        public int CurrentIndex { get; set; }            // 0 primary, 1 sidearm
        public Weapon Current { get { return CurrentIndex == 0 && Primary != null && !PrimaryBlocked ? Primary : Sidearm; } }
        public readonly List<EquipmentSlot> Equipment = new List<EquipmentSlot>();
        public int SelectedEquipment { get; set; }
        public EquipmentSlot SelectedSlot { get { return Equipment.Count > 0 ? Equipment[UnityEngine.Mathf.Clamp(SelectedEquipment, 0, Equipment.Count - 1)] : null; } }

        public WeaponInventory(OfficerLoadout loadout, List<EquipmentCount> bonus)
        {
            var primary = GameData.Weapon(loadout.primaryId);
            var sidearm = GameData.Weapon(loadout.sidearmId) ?? GameData.Weapon("pistol_p17");
            // Game-mode-only weapons stay at headquarters on real missions.
            var mission = OfficerSelectionManager.Mission;
            bool versus = mission != null && mission.IsVersus;
            if (primary != null && primary.versusOnly && !versus) primary = GameData.Weapon("rifle_compact");
            if (sidearm.versusOnly && !versus) sidearm = GameData.Weapon("pistol_p17");
            if (primary != null) Primary = new Weapon(primary, loadout);
            Sidearm = new Weapon(sidearm, loadout);
            PrimaryBlocked = loadout.useShield || Primary == null;
            CurrentIndex = PrimaryBlocked ? 1 : 0;

            foreach (var entry in loadout.equipment) Add(entry.id, entry.count);
            if (bonus != null) foreach (var entry in bonus) Add(entry.id, entry.count);
        }

        void Add(string id, int count)
        {
            var data = GameData.Equipment(id);
            if (data == null || count <= 0) return;
            foreach (var slot in Equipment)
            {
                if (slot.Data != data) continue;
                slot.Count += count;
                return;
            }
            Equipment.Add(new EquipmentSlot(data, data.consumable ? count : 1));
        }

        public EquipmentSlot Find(EquipmentKind kind)
        {
            foreach (var slot in Equipment)
                if (slot.Data.kind == kind && slot.Count > 0) return slot;
            return null;
        }

        public int CountOf(EquipmentKind kind)
        {
            var slot = Find(kind);
            return slot != null ? slot.Count : 0;
        }

        public bool Consume(EquipmentKind kind)
        {
            var slot = Find(kind);
            if (slot == null) return false;
            slot.Used++;
            if (slot.Data.consumable) slot.Count--;
            return true;
        }

        public void SelectNextEquipment(int direction)
        {
            if (Equipment.Count == 0) return;
            SelectedEquipment = (SelectedEquipment + direction + Equipment.Count) % Equipment.Count;
        }
    }
}
