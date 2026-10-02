using UnityEngine;

namespace Swat
{
    // Medical kit: restores a limited amount of health over a few seconds
    // (never an instant full heal). Medics get 50% more out of each kit.
    public static class MedicalKit
    {
        public static float HealAmount(EquipmentData data, OfficerData user)
        {
            return data.healAmount * (user.role == OfficerRole.Medic ? 1.5f : 1f);
        }

        // Treats the most hurt squadmate within reach, otherwise the player.
        public static bool Use(PlayerController player, EquipmentData data)
        {
            SquadAI patient = null;
            float worst = 0.9f;
            foreach (var officer in AIManager.Instance.Officers)
            {
                if (!officer.IsAlive || Vector3.Distance(officer.Position, player.Position) > 2.5f) continue;
                if (officer.Health.Fraction < worst && officer.Health.Fraction < player.Health.Fraction)
                {
                    worst = officer.Health.Fraction;
                    patient = officer;
                }
            }
            float amount = HealAmount(data, player.Officer);
            if (patient != null)
            {
                patient.Health.HealOverTime(amount, 4f);
                UIManager.Notify("Treating " + patient.Data.callsign);
            }
            else if (player.Health.Fraction < 0.99f)
            {
                player.Health.HealOverTime(amount, 4f);
                UIManager.Notify("Treating your injuries");
            }
            else
            {
                UIManager.Notify("Nobody here needs a medical kit");
                return false;
            }
            player.PlayTreatAnimation(1.2f);
            AudioManager.Play(Sound.Medkit, player.Position, 0.7f);
            return true;
        }
    }
}
