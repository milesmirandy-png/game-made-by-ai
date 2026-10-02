using System;
using System.Collections.Generic;

namespace Swat
{
    [Serializable]
    public class EquipmentCount
    {
        public string id;
        public int count;

        public EquipmentCount() { }
        public EquipmentCount(string id, int count)
        {
            this.id = id;
            this.count = count;
        }
    }

    // What one officer takes into a mission. Stored in the save file per officer.
    [Serializable]
    public class OfficerLoadout
    {
        public string officerId;
        public string primaryId;
        public string sidearmId;
        public string armorId = "armor_standard";
        public bool useShield;
        public string lightId;
        public string opticId;
        public string muzzleId;
        public string stockId;
        public int uniformIndex;
        public List<EquipmentCount> equipment = new List<EquipmentCount>();

        public OfficerLoadout Clone()
        {
            var copy = (OfficerLoadout)MemberwiseClone();
            copy.equipment = new List<EquipmentCount>();
            foreach (var item in equipment) copy.equipment.Add(new EquipmentCount(item.id, item.count));
            return copy;
        }

        public int CountOf(string id)
        {
            foreach (var item in equipment)
                if (item.id == id) return item.count;
            return 0;
        }

        public void SetCount(string id, int count)
        {
            for (int i = equipment.Count - 1; i >= 0; i--)
                if (equipment[i].id == id) equipment.RemoveAt(i);
            if (count > 0) equipment.Add(new EquipmentCount(id, count));
        }
    }

    // Capacity rules: each equipment item costs capacity points; roles get
    // discounts on their specialty and armor changes how much you can carry.
    public static class LoadoutRules
    {
        public static int Capacity(OfficerData officer, OfficerLoadout loadout)
        {
            var armor = GameData.Armor(loadout.armorId);
            int capacity = officer.equipmentCapacity + (armor != null ? armor.capacityBonus : 0);
            if (loadout.useShield) capacity -= 2;
            return Math.Max(2, capacity);
        }

        public static int Cost(OfficerData officer, EquipmentData item)
        {
            int cost = item.capacityCost;
            if (officer.role == OfficerRole.Breacher && item.kind == EquipmentKind.BreachingCharge) cost = 1;
            if (officer.role == OfficerRole.Medic && item.kind == EquipmentKind.MedicalKit) cost = 1;
            if (officer.role == OfficerRole.Recon && item.kind == EquipmentKind.ReconCamera) cost = 1;
            return cost;
        }

        public static int Used(OfficerData officer, OfficerLoadout loadout)
        {
            int used = 0;
            foreach (var entry in loadout.equipment)
            {
                var item = GameData.Equipment(entry.id);
                if (item != null) used += Cost(officer, item) * entry.count;
            }
            return used;
        }

        public static bool CanAdd(OfficerData officer, OfficerLoadout loadout, EquipmentData item)
        {
            if (loadout.CountOf(item.id) >= item.maxCarry) return false;
            if (!item.consumable && loadout.CountOf(item.id) >= 1) return false;
            return Used(officer, loadout) + Cost(officer, item) <= Capacity(officer, loadout);
        }

        // Fixes anything invalid (missing ids, locked items, over capacity).
        public static void Validate(OfficerData officer, OfficerLoadout loadout)
        {
            loadout.officerId = officer.id;
            var primary = GameData.Weapon(loadout.primaryId);
            if (primary == null || primary.isSidearm || !primary.AllowedFor(officer.role) || !Progression.IsAvailable(primary))
                loadout.primaryId = officer.defaultLoadout.primaryId;
            var sidearm = GameData.Weapon(loadout.sidearmId);
            if (sidearm == null || !sidearm.isSidearm || !Progression.IsAvailable(sidearm)) loadout.sidearmId = officer.defaultLoadout.sidearmId;
            var armor = GameData.Armor(loadout.armorId);
            if (armor == null || !Progression.IsAvailable(armor)) loadout.armorId = "armor_standard";
            if (officer.role != OfficerRole.Shield) loadout.useShield = false;
            loadout.lightId = ValidAttachment(loadout.lightId, AttachmentSlot.Light);
            loadout.opticId = ValidAttachment(loadout.opticId, AttachmentSlot.Optic);
            loadout.muzzleId = ValidAttachment(loadout.muzzleId, AttachmentSlot.Muzzle);
            loadout.stockId = ValidAttachment(loadout.stockId, AttachmentSlot.Stock);
            for (int i = loadout.equipment.Count - 1; i >= 0; i--)
            {
                var item = GameData.Equipment(loadout.equipment[i].id);
                if (item == null || !Progression.IsAvailable(item) || loadout.equipment[i].count <= 0) loadout.equipment.RemoveAt(i);
            }
            while (Used(officer, loadout) > Capacity(officer, loadout) && loadout.equipment.Count > 0)
            {
                var last = loadout.equipment[loadout.equipment.Count - 1];
                last.count--;
                if (last.count <= 0) loadout.equipment.RemoveAt(loadout.equipment.Count - 1);
            }
        }

        static string ValidAttachment(string id, AttachmentSlot slot)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var attachment = GameData.Attachment(id);
            return attachment != null && attachment.slot == slot && Progression.IsAvailable(attachment) ? id : null;
        }
    }
}
