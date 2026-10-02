using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // The player's loadout: four guns plus tactical equipment in one list of
    // slots. Q cycles slots, 1-7 pick one, left click fires or uses it.
    // Guns are hitscan (raycasts), so there are no bullet objects to simulate.
    public class WeaponController : MonoBehaviour
    {
        [SerializeField] float laserWidth = 0.02f;

        public int SlotCount { get { return weapons.Count + equipment.Count; } }
        public int CurrentSlot { get; private set; }
        public bool IsWeaponSelected { get { return CurrentSlot < weapons.Count; } }
        public Weapon CurrentWeapon { get { return IsWeaponSelected ? weapons[CurrentSlot] : null; } }
        public EquipmentSlot CurrentEquipment { get { return IsWeaponSelected ? null : equipment[CurrentSlot - weapons.Count]; } }
        public bool IsReloading { get { return reloadEnd > 0f; } }
        public float ReloadProgress { get { return IsReloading ? 1f - (reloadEnd - Time.time) / CurrentWeapon.Data.reloadTime : 0f; } }
        public float Spread { get; private set; }
        public float MoveSpeedMultiplier { get { return IsWeaponSelected ? CurrentWeapon.Data.moveSpeedMultiplier : 1f; } }
        public float LastHitTime { get; private set; }
        public IList<Weapon> Weapons { get { return weapons; } }
        public IList<EquipmentSlot> Equipment { get { return equipment; } }

        readonly List<Weapon> weapons = new List<Weapon>();
        readonly List<EquipmentSlot> equipment = new List<EquipmentSlot>();
        PlayerController player;
        CharacterParts parts;
        Transform laser;
        float nextFireTime, reloadEnd, bloom;
        int lastWeaponSlot;
        bool initialized;

        public void Init(PlayerController owner, CharacterParts characterParts)
        {
            player = owner;
            parts = characterParts;
            foreach (var data in GameData.Weapons) weapons.Add(new Weapon(data));
            foreach (var data in GameData.Equipment) equipment.Add(new EquipmentSlot(data));

            laser = Shapes.Box("Laser", transform, Vector3.zero, Vector3.one, new Color(1f, 0.15f, 0.1f), false, 3f).transform;
            Select(Mathf.Min(2, weapons.Count - 1)); // start with the assault rifle
        }

        public void Tick(float dt, bool active)
        {
            if (IsReloading && Time.time >= reloadEnd)
            {
                reloadEnd = 0f;
                CurrentWeapon.FinishReload();
            }
            bloom = Mathf.MoveTowards(bloom, 0f, (IsWeaponSelected ? CurrentWeapon.Data.recoilRecovery : 10f) * dt);

            if (!active)
            {
                laser.gameObject.SetActive(false);
                return;
            }

            if (GameInput.SwitchItem) Select((CurrentSlot + 1) % SlotCount);
            int pressed = GameInput.SlotPressed;
            if (pressed >= 0 && pressed < SlotCount) Select(pressed);

            if (IsWeaponSelected) UpdateGun();
            else if (GameInput.FirePressed) UseEquipment();

            UpdateLaser();
        }

        void Select(int slot)
        {
            if (slot == CurrentSlot && initialized) return;
            initialized = true;
            CurrentSlot = slot;
            reloadEnd = 0f;
            bloom = 0f;
            if (IsWeaponSelected)
            {
                lastWeaponSlot = slot;
                float length = CurrentWeapon.Data.modelLength;
                parts.gun.gameObject.SetActive(true);
                parts.gun.localScale = new Vector3(0.08f, 0.11f, length);
                parts.gun.localPosition = new Vector3(0.1f, 1.2f, 0.25f + length * 0.5f);
            }
            else
            {
                parts.gun.gameObject.SetActive(false);
            }
            AudioManager.Play2D(Sound.Click, 0.4f);
        }

        void UpdateGun()
        {
            var weapon = CurrentWeapon;
            var data = weapon.Data;
            Spread = data.spread + bloom + (player.IsMoving ? data.spread * 0.5f : 0f) + (player.IsSprinting ? 6f : 0f);

            if (GameInput.Reload && weapon.CanReload && !IsReloading) StartReload();

            bool trigger = data.fireMode == FireMode.FullAuto ? GameInput.FireHeld : GameInput.FirePressed;
            if (!trigger || IsReloading || player.IsSprinting || Time.time < nextFireTime) return;

            if (weapon.Magazine > 0)
            {
                Fire(weapon);
            }
            else
            {
                AudioManager.Play2D(Sound.Empty, 0.6f);
                nextFireTime = Time.time + 0.3f;
                if (weapon.CanReload) StartReload();
            }
        }

        void StartReload()
        {
            reloadEnd = Time.time + CurrentWeapon.Data.reloadTime;
            AudioManager.Play(Sound.Reload, player.Position, 0.7f);
        }

        void Fire(Weapon weapon)
        {
            var data = weapon.Data;
            weapon.Magazine--;
            nextFireTime = Time.time + 1f / Mathf.Max(0.1f, data.fireRate);

            Vector3 origin = player.ChestPosition;
            Vector3 muzzle = parts.muzzle.position;
            var effects = EffectsManager.Instance;
            bool hitSomeone = false;

            for (int i = 0; i < Mathf.Max(1, data.pellets); i++)
            {
                // Average of two randoms gives more shots near the centre of the cone.
                float angle = (Random.value + Random.value - 1f) * Spread;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * player.AimDirection;
                Vector3 end = origin + direction * data.range;

                RaycastHit hit;
                if (Physics.Raycast(origin, direction, out hit, data.range, Layers.ShootableMask, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    var target = hit.collider.GetComponentInParent<IDamageable>();
                    if (target != null && target.IsAlive)
                    {
                        target.TakeDamage(new DamageInfo { amount = data.damage, point = hit.point, direction = direction, attacker = Team.Police });
                        effects.Burst(hit.point, -direction, new Color(0.6f, 0.05f, 0.05f), 4, 2f);
                        hitSomeone = true;
                    }
                    else
                    {
                        effects.Burst(hit.point, hit.normal, new Color(0.75f, 0.72f, 0.65f), 3, 2.5f, 0.05f);
                    }
                }
                effects.SpawnTracer(muzzle, end, data.tracerColor);
            }

            bloom = Mathf.Min(bloom + data.recoil, data.recoil * 6f + 4f);
            effects.FlashLight(muzzle, new Color(1f, 0.8f, 0.45f), 2.5f, 6f, 0.06f);
            AudioManager.Play(data.fireSound, muzzle, 0.8f, Random.Range(0.94f, 1.06f));
            Noise.Emit(origin, data.noiseRadius, NoiseKind.Gunshot);
            GameManager.Instance.CameraRig.Shake(data.recoil * 0.08f);
            if (hitSomeone)
            {
                LastHitTime = Time.time;
                AudioManager.Play2D(Sound.Hit, 0.35f);
            }
        }

        void UseEquipment()
        {
            var slot = CurrentEquipment;
            if (slot.Count <= 0)
            {
                UIManager.Notify("No " + slot.Data.displayName + " left");
                AudioManager.Play2D(Sound.Empty, 0.6f);
                return;
            }

            if (slot.Data.kind == EquipmentKind.BreachingCharge)
            {
                var door = AIManager.Instance.FindBreachableDoor(player.Position, 2.2f);
                if (door == null)
                {
                    UIManager.Notify("Stand next to a locked door to place a breaching charge");
                    return;
                }
                slot.Count--;
                door.PlaceCharge();
            }
            else
            {
                Vector3 target = player.AimPoint;
                Vector3 offset = target - player.Position;
                offset.y = 0f;
                target = player.Position + Vector3.ClampMagnitude(offset, slot.Data.throwRange);
                target.y = 0.1f;
                slot.Count--;
                ThrownGrenade.Throw(slot.Data, player.ChestPosition + player.AimDirection * 0.4f, target);
                AudioManager.Play(Sound.Throw, player.Position, 0.6f);
            }
            Select(lastWeaponSlot);
        }

        // The breaching charge can also be placed by pressing E at a locked door.
        public bool TryUseBreachingCharge()
        {
            foreach (var slot in equipment)
            {
                if (slot.Data.kind != EquipmentKind.BreachingCharge || slot.Count <= 0) continue;
                slot.Count--;
                return true;
            }
            return false;
        }

        public int CountOf(EquipmentKind kind)
        {
            foreach (var slot in equipment)
                if (slot.Data.kind == kind) return slot.Count;
            return 0;
        }

        // A thin laser from the gun to whatever it points at makes aiming from above easy.
        void UpdateLaser()
        {
            if (!IsWeaponSelected || player.IsSprinting)
            {
                laser.gameObject.SetActive(false);
                return;
            }
            Vector3 origin = player.ChestPosition;
            float range = CurrentWeapon.Data.range;
            RaycastHit hit;
            float length = Physics.Raycast(origin, player.AimDirection, out hit, range, Layers.ShootableMask, QueryTriggerInteraction.Ignore) ? hit.distance : range;
            Vector3 start = parts.muzzle.position;
            Vector3 end = origin + player.AimDirection * length;
            end.y = start.y;
            Vector3 delta = end - start;
            if (delta.sqrMagnitude < 0.01f || Vector3.Dot(delta, player.AimDirection) <= 0f)
            {
                laser.gameObject.SetActive(false);
                return;
            }
            laser.gameObject.SetActive(true);
            laser.SetPositionAndRotation(start + delta * 0.5f, Quaternion.LookRotation(delta));
            laser.localScale = new Vector3(laserWidth, laserWidth, delta.magnitude);
        }
    }
}
