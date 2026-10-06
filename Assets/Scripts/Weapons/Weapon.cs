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
        // From the magazine, optic, grip and muzzle attachments.
        public int MagazineSize { get; private set; }
        public float ReloadTime { get; private set; }
        public float AimSpeed { get; private set; }          // how fast the sights come up (first person)
        public float Zoom { get; private set; }              // optic magnification (1 = none)
        public float LookAhead { get; private set; }         // extra camera reach while steady aiming from above
        public float FlashMultiplier { get; private set; }
        public bool HasLaser { get; private set; }
        public AttachmentData Optic { get; private set; }

        public Weapon(WeaponData data, OfficerLoadout loadout)
        {
            Data = data;
            Mode = data.fireMode;
            SpreadMultiplier = RecoilMultiplier = NoiseMultiplier = MoveMultiplier = LightRangeMultiplier = 1f;
            float magazine = 1f, reload = 1f;
            AimSpeed = Zoom = FlashMultiplier = 1f;
            LookAhead = data.steadyLookAhead;
            if (loadout != null && !data.isSidearm)
                foreach (var id in Ids(loadout))
                {
                    var attachment = GameData.Attachment(id);
                    if (attachment == null) continue;
                    SpreadMultiplier *= attachment.spreadMultiplier;
                    RecoilMultiplier *= attachment.recoilMultiplier;
                    NoiseMultiplier *= attachment.noiseMultiplier;
                    MoveMultiplier *= attachment.moveMultiplier;
                    LightRangeMultiplier *= attachment.lightRangeMultiplier;
                    magazine *= attachment.magazineMultiplier;
                    reload *= attachment.reloadMultiplier;
                    AimSpeed *= attachment.aimSpeedMultiplier;
                    FlashMultiplier *= attachment.flashMultiplier;
                    LookAhead += attachment.lookAhead;
                    if (attachment.zoom > Zoom) Zoom = attachment.zoom;
                    if (attachment.laser) HasLaser = true;
                    if (attachment.slot == AttachmentSlot.Optic) Optic = attachment;
                }
            MagazineSize = System.Math.Max(1, (int)System.Math.Round(data.magazineSize * magazine));
            ReloadTime = data.reloadTime * reload;
            Magazine = MagazineSize;
            Reserve = data.startingReserve;
        }

        // Every attachment id in a loadout (some may be empty).
        public static string[] Ids(OfficerLoadout loadout)
        {
            return new[] { loadout.lightId, loadout.opticId, loadout.muzzleId, loadout.stockId, loadout.underbarrelId, loadout.magazineId };
        }

        public bool CanReload { get { return Magazine < MagazineSize && Reserve > 0; } }
        public float Spread { get { return Data.spread * SpreadMultiplier; } }
        public float Recoil { get { return Data.recoil * RecoilMultiplier; } }
        public float NoiseRadius { get { return Data.noiseRadius * NoiseMultiplier; } }

        public void FinishReload()
        {
            int taken = System.Math.Min(MagazineSize - Magazine, Reserve);
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
            Magazine = MagazineSize;
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

        // Gun Game: one gun and nothing else (no sidearm to swap to, no shield in the way).
        public void SetOnly(Weapon weapon)
        {
            Primary = weapon;
            Sidearm = weapon;
            PrimaryBlocked = false;
            CurrentIndex = 0;
        }

        public bool SingleWeapon { get { return Primary != null && Primary == Sidearm; } }

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
