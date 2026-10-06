using System.Collections.Generic;

namespace Swat
{
    // A weapon being carried: its settings, attachments and remaining ammo.
    public class Weapon
    {
        public WeaponData Data { get; private set; }
        public int Magazine { get; set; }
        // Spare ammo. Magazine-fed guns carry it as separate magazines (Spares): a reload swaps in
        // the fullest one and the one coming out goes back in the pouch with whatever it still holds,
        // so a hasty reload doesn't throw rounds away, but you can end up with a pouch of half-empty
        // magazines. Tube-fed shotguns, revolvers and launchers load loose rounds as before.
        public int Reserve
        {
            get
            {
                if (!UsesMagazines) return looseRounds;
                int sum = 0;
                foreach (int rounds in spares) sum += rounds;
                return sum;
            }
            set
            {
                if (!UsesMagazines) { looseRounds = System.Math.Max(0, value); return; }
                // Refill: full magazines, plus a partial one for the rest.
                spares.Clear();
                int left = System.Math.Max(0, value);
                while (left > 0)
                {
                    int rounds = System.Math.Min(left, MagazineSize);
                    spares.Add(rounds);
                    left -= rounds;
                }
            }
        }
        public bool UsesMagazines { get; private set; }
        public IList<int> Spares { get { return spares; } }
        readonly List<int> spares = new List<int>();
        int looseRounds;
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
            UsesMagazines = FedByMagazine(data);
            Magazine = MagazineSize;
            Reserve = data.startingReserve;
        }

        // Every attachment id in a loadout (some may be empty).
        public static string[] Ids(OfficerLoadout loadout)
        {
            return new[] { loadout.lightId, loadout.opticId, loadout.muzzleId, loadout.stockId, loadout.underbarrelId, loadout.magazineId };
        }

        // Tube-fed shotguns, revolvers and launchers are loaded round by round.
        public static bool FedByMagazine(WeaponData data)
        {
            switch (data.category)
            {
                case WeaponCategory.Shotgun:
                case WeaponCategory.Revolver:
                case WeaponCategory.LessLethal:
                case WeaponCategory.GrenadeLauncher:
                case WeaponCategory.Pepperball:
                    return false;
                default:
                    return true;
            }
        }

        public bool CanReload
        {
            get
            {
                if (Magazine >= MagazineSize) return false;
                if (!UsesMagazines) return looseRounds > 0;
                foreach (int rounds in spares) if (rounds > Magazine) return true;
                return false;
            }
        }

        // Spare magazines with any rounds in them.
        public int MagazinesLeft
        {
            get
            {
                int count = 0;
                foreach (int rounds in spares) if (rounds > 0) count++;
                return count;
            }
        }

        // The ammo line for the HUD and wheel: exact rounds, or with realistic ammo how full the
        // magazine feels and how many magazines are left.
        public string AmmoText(bool realistic)
        {
            if (!realistic) return Magazine + " / " + Reserve;
            if (!UsesMagazines) return MagazineFeel + ", " + Reserve + " loose";
            int mags = MagazinesLeft;
            return MagazineFeel + ", " + mags + (mags == 1 ? " mag" : " mags");
        }

        // How full the magazine in the gun feels, in words (realistic ammo shows no exact count).
        public string MagazineFeel
        {
            get
            {
                if (Magazine <= 0) return "Empty";
                float f = Magazine / (float)MagazineSize;
                return f >= 0.95f ? "Full" : f >= 0.65f ? "Heavy" : f >= 0.35f ? "Half" : "Light";
            }
        }
        public float Spread { get { return Data.spread * SpreadMultiplier; } }
        public float Recoil { get { return Data.recoil * RecoilMultiplier; } }
        public float NoiseRadius { get { return Data.noiseRadius * NoiseMultiplier; } }

        public void FinishReload()
        {
            if (!UsesMagazines)
            {
                int taken = System.Math.Min(MagazineSize - Magazine, looseRounds);
                Magazine += taken;
                looseRounds -= taken;
                return;
            }
            int best = -1;
            for (int i = 0; i < spares.Count; i++)
                if (spares[i] > Magazine && (best < 0 || spares[i] > spares[best])) best = i;
            if (best < 0) return;
            int incoming = spares[best];
            spares.RemoveAt(best);
            // The magazine coming out keeps its rounds (an empty one is dropped).
            if (Magazine > 0) spares.Add(Magazine);
            Magazine = incoming;
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
