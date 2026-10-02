using UnityEngine;

namespace Swat
{
    // The player's weapons and equipment: firing (hitscan), reloading,
    // switching between primary and sidearm, fire mode, steady aim, the aiming
    // laser, and using the selected tactical equipment (G).
    public class WeaponController : MonoBehaviour
    {
        [SerializeField] float laserWidth = 0.02f;

        public WeaponInventory Inventory { get; private set; }
        public Weapon Current { get { return Inventory.Current; } }
        public bool IsReloading { get { return reloadEnd > 0f; } }
        public bool IsSwitching { get { return Time.time < switchEnd; } }
        public float ReloadProgress { get { return IsReloading ? Mathf.Clamp01(1f - (reloadEnd - Time.time) / Current.Data.reloadTime) : 0f; } }
        public float Spread { get; private set; }
        public float LastHitTime { get; private set; }
        public int ShotsFired { get; private set; }
        public int ShotsHit { get; private set; }
        public bool Overcharged { get; set; }

        public float MoveSpeedMultiplier
        {
            get { return Current.Data.moveSpeedMultiplier * Current.MoveMultiplier; }
        }

        PlayerController player;
        Transform laser;
        float nextFireTime, reloadEnd, switchEnd, bloom;

        public void Init(PlayerController owner, WeaponInventory inventory)
        {
            player = owner;
            Inventory = inventory;
            laser = Shapes.Box("Laser", transform, Vector3.zero, Vector3.one, new Color(1f, 0.15f, 0.1f), false, 3f).transform;
            ApplyWeaponModel();
        }

        public void Tick(float dt, bool active)
        {
            if (IsReloading && Time.time >= reloadEnd)
            {
                reloadEnd = 0f;
                Current.FinishReload();
            }
            bloom = Mathf.MoveTowards(bloom, 0f, Current.Data.recoilRecovery * dt);
            player.Animator.SetReload(ReloadProgress);

            if (!active)
            {
                laser.gameObject.SetActive(false);
                return;
            }

            if (GameInput.Down(InputAction.SwitchWeapon)) Switch(Inventory.CurrentIndex == 0 ? 1 : 0);
            if (GameInput.Down(InputAction.Slot1)) Switch(0);
            if (GameInput.Down(InputAction.Slot2)) Switch(1);
            if (GameInput.Down(InputAction.Slot3)) CycleEquipment(-1);
            if (GameInput.Down(InputAction.Slot4)) CycleEquipment(1);
            if (GameInput.Down(InputAction.FireMode) && Current.ToggleFireMode())
            {
                AudioManager.Ui(Sound.Click);
                UIManager.Notify(Current.Data.displayName + ": " + (Current.Automatic ? "automatic" : "semi-automatic"));
            }
            if (GameInput.Down(InputAction.UseEquipment)) UseSelectedEquipment();

            UpdateGun();
            UpdateLaser();
        }

        void Switch(int index)
        {
            if (index == 0 && Inventory.PrimaryBlocked)
            {
                if (GameInput.Down(InputAction.Slot1)) UIManager.Notify("You can't use a primary weapon while carrying a shield");
                return;
            }
            if (index == Inventory.CurrentIndex) return;
            Inventory.CurrentIndex = index;
            reloadEnd = 0f;
            bloom = 0f;
            switchEnd = Time.time + Current.Data.switchTime;
            ApplyWeaponModel();
            AudioManager.Play(Sound.Click, player.Position, 0.4f);
            MissionManager.Instance.Report(ObjectiveType.TrainingSwitchWeapon, index);
        }

        void ApplyWeaponModel()
        {
            CharacterFactory.SetWeapon(player.Parts, Current.Data, Inventory.CurrentIndex == 0 ? player.Loadout : null);
        }

        void CycleEquipment(int direction)
        {
            Inventory.SelectNextEquipment(direction);
            var slot = Inventory.SelectedSlot;
            if (slot != null) UIManager.Notify("Equipment: " + slot.Data.displayName + " (" + slot.Count + ")");
            AudioManager.Ui(Sound.UiHover);
        }

        void UpdateGun()
        {
            var weapon = Current;
            var data = weapon.Data;
            float stance = (player.IsCrouched ? 0.75f : 1f) * (player.IsSteadyAiming ? 0.6f : 1f);
            Spread = (weapon.Spread + bloom) * stance + (player.IsMoving ? weapon.Spread * 0.5f : 0f) + (player.IsSprinting ? 6f : 0f);

            if (GameInput.Down(InputAction.Reload) && weapon.CanReload && !IsReloading) StartReload();

            bool trigger = weapon.Automatic ? GameInput.Held(InputAction.Fire) : GameInput.Down(InputAction.Fire);
            if (!trigger || IsReloading || IsSwitching || player.IsSprinting) return;
            if (Time.time < nextFireTime) return;

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
            reloadEnd = Time.time + Current.Data.reloadTime;
            AudioManager.Play(Sound.Reload, player.Position, 0.7f);
            MissionManager.Instance.Report(ObjectiveType.TrainingReload, 1);
        }

        void Fire(Weapon weapon)
        {
            var data = weapon.Data;
            weapon.Magazine--;
            nextFireTime = Time.time + 1f / Mathf.Max(0.1f, data.fireRate);
            ShotsFired++;

            Vector3 origin = player.ChestPosition;
            Vector3 muzzle = player.Parts.muzzle.position;
            bool hitSomeone = false;
            float boost = Overcharged && data.lessLethal ? 1.5f : 1f;
            var damage = new DamageInfo { amount = data.damage, attacker = Team.Police, lessLethal = data.lessLethal, stun = data.stunDuration * boost };

            for (int i = 0; i < Mathf.Max(1, data.pellets); i++)
            {
                Vector3 direction = WeaponEffects.Scatter(player.AimDirection, Spread);
                if (WeaponEffects.Shoot(origin, direction, data.range, damage, muzzle, data.tracerColor) != null) hitSomeone = true;
            }
            if (data.lessLethal && Overcharged) Overcharged = false;

            bloom = Mathf.Min(bloom + weapon.Recoil, weapon.Recoil * 6f + 4f);
            player.Animator.Fire(Mathf.Clamp(weapon.Recoil * 0.6f, 0.4f, 1.5f));
            WeaponEffects.MuzzleFlash(muzzle, data.fireSound, 0.8f, weapon.NoiseRadius, NoiseKind.Gunshot);
            GameManager.Instance.CameraRig.Shake(weapon.Recoil * 0.08f);
            if (!hitSomeone) return;
            ShotsHit++;
            LastHitTime = Time.time;
            AudioManager.Play2D(Sound.Hit, 0.35f);
        }

        void UseSelectedEquipment()
        {
            var slot = Inventory.SelectedSlot;
            if (slot == null)
            {
                UIManager.Notify("You aren't carrying any equipment");
                return;
            }
            if (slot.Count <= 0)
            {
                UIManager.Notify("No " + slot.Data.displayName + " left");
                AudioManager.Play2D(Sound.Empty, 0.6f);
                return;
            }
            TacticalEquipment.UseByPlayer(player, slot);
        }

        // A thin laser from the gun to whatever it points at makes aiming from above easy.
        void UpdateLaser()
        {
            if (player.IsSprinting || IsSwitching)
            {
                laser.gameObject.SetActive(false);
                return;
            }
            Vector3 origin = player.ChestPosition;
            float range = Current.Data.range;
            RaycastHit hit;
            float length = Physics.Raycast(origin, player.AimDirection, out hit, range, Layers.ShootableMask, QueryTriggerInteraction.Ignore) ? hit.distance : range;
            Vector3 start = player.Parts.muzzle.position;
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
            float width = player.IsSteadyAiming ? laserWidth * 1.6f : laserWidth;
            laser.localScale = new Vector3(width, width, delta.magnitude);
        }
    }
}
