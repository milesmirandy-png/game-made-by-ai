using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Reporting to TOC (tactical operations centre), as on a real call-out: once a suspect is
    // restrained or down, a civilian is under control, hurt or dead, or an officer is down, someone
    // calls it in. Look at them and press Report (H); squadmates call in what they secure themselves.
    // Each report scores; anything left unreported at the end costs points.
    public static class TocReports
    {
        static readonly HashSet<Object> reported = new HashSet<Object>();
        public const float Range = 6f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            reported.Clear();
        }

        public static void Begin()
        {
            reported.Clear();
        }

        public static bool IsReported(Object target) { return target != null && reported.Contains(target); }

        // What there is to report about someone, or null when there's nothing (yet).
        public static string Status(Object target)
        {
            var enemy = target as EnemyAI;
            if (enemy != null)
            {
                if (enemy.Data.archetype == EnemyArchetype.TrainingDummy) return null;
                if (enemy.State == EnemyState.Restrained) return "suspect secured";
                if (enemy.State == EnemyState.Dead) return "suspect down";
                return null;
            }
            var civilian = target as CivilianAI;
            if (civilian != null)
            {
                if (civilian.State == CivilianState.Dead) return "civilian down";
                if (civilian.State == CivilianState.Evacuated) return null;
                if (civilian.State == CivilianState.Injured) return "injured civilian";
                if (civilian.UnderPoliceControl) return "civilian secured";
                return null;
            }
            var officer = target as SquadAI;
            if (officer != null && !officer.IsAlive) return "officer down";
            return null;
        }

        // The nearest unreported person in front of the player, in sight.
        public static Object Candidate(PlayerController player)
        {
            Object best = null;
            float bestScore = float.MaxValue;
            Vector3 eye = player.EyePosition;
            var ai = AIManager.Instance;
            foreach (var enemy in ai.Enemies) Consider(player, eye, enemy, enemy != null ? enemy.Position : Vector3.zero, ref best, ref bestScore);
            foreach (var civilian in ai.Civilians) Consider(player, eye, civilian, civilian != null ? civilian.Position : Vector3.zero, ref best, ref bestScore);
            foreach (var officer in ai.Officers) Consider(player, eye, officer, officer != null ? officer.Position : Vector3.zero, ref best, ref bestScore);
            return best;
        }

        static void Consider(PlayerController player, Vector3 eye, Object target, Vector3 position, ref Object best, ref float bestScore)
        {
            if (target == null || reported.Contains(target) || Status(target) == null) return;
            Vector3 to = position - player.Position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance > Range) return;
            float angle = distance > 0.3f ? Vector3.Angle(player.AimDirection, to) : 0f;
            if (angle > 70f) return;
            if (Physics.Linecast(eye, position + Vector3.up * 0.6f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return;
            float score = distance + angle * 0.03f;
            if (score >= bestScore) return;
            best = target;
            bestScore = score;
        }

        // The player calls it in.
        public static bool TryReport(PlayerController player)
        {
            var target = Candidate(player);
            if (target == null)
            {
                UIManager.Notify("Nothing to report here");
                return false;
            }
            Report(target, null);
            return true;
        }

        // officer: the squadmate calling it in (null for the player).
        public static void Report(Object target, SquadAI officer)
        {
            string status = Status(target);
            if (status == null || !reported.Add(target)) return;
            var squad = SquadCommandManager.Instance;
            string line = "TOC, " + status + ".";
            if (officer != null) squad.Radio(officer, line);
            else
            {
                squad.PlayerRadio(line);
                AudioManager.RadioChirp(1f);
            }
            squad.TocReply("Copy, " + status + ".");
            MissionManager.Instance.OnReportedToToc();
        }

        // Everyone who should have been called in but wasn't.
        public static int Unreported()
        {
            int count = 0;
            var ai = AIManager.Instance;
            if (ai == null) return 0;
            foreach (var enemy in ai.Enemies) if (enemy != null && !reported.Contains(enemy) && Status(enemy) != null) count++;
            foreach (var civilian in ai.Civilians) if (civilian != null && !reported.Contains(civilian) && Status(civilian) != null) count++;
            foreach (var officer in ai.Officers) if (officer != null && !reported.Contains(officer) && Status(officer) != null) count++;
            return count;
        }
    }
}
