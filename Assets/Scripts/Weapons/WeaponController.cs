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

        // Weapon wheel (hold the switch-weapon key): primary, sidearm and each equipment item.
        public struct WheelEntry
        {
            public string label;
            public int weapon;     // 0 primary, 1 sidearm, -1 equipment
            public int equipment;  // index into Inventory.Equipment
            public bool enabled, current;
            public string detail;
            public WeaponData weaponData;
            public EquipmentKind kind;
        }
        public bool WheelOpen { get; private set; }
        public Vector2 WheelCenter { get; private set; } // GUI pixels (y down)
        public int WheelHovered { get; private set; }
        public readonly System.Collections.Generic.List<WheelEntry> WheelEntries = new System.Collections.Generic.List<WheelEntry>();
        float switchHeldSince = -1f;
        bool lowAmmoWarned;

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
            player.Animator.SetSwitch(IsSwitching ? Mathf.Clamp01(1f - (switchEnd - Time.time) / Mathf.Max(0.05f, Current.Data.switchTime)) : 0f);

            if (!active)
            {
                laser.gameObject.SetActive(false);
                WheelOpen = false;
                switchHeldSince = -1f;
                return;
            }

            // Tap the switch key to swap weapons; hold it for the weapon wheel.
            bool wheelWasOpen = WheelOpen;
            if (GameInput.Down(InputAction.SwitchWeapon)) switchHeldSince = Time.unscaledTime;
            if (switchHeldSince >= 0f && !WheelOpen && GameInput.Held(InputAction.SwitchWeapon) && Time.unscaledTime - switchHeldSince > 0.25f) OpenWheel();
            if (WheelOpen) UpdateWheel();
            if (GameInput.Released(InputAction.SwitchWeapon))
            {
                if (WheelOpen) ChooseWheel();
                else if (switchHeldSince >= 0f) Switch(Inventory.CurrentIndex == 0 ? 1 : 0);
                switchHeldSince = -1f;
            }
            // The click that picks a wheel entry must not also fire.
            if (WheelOpen || wheelWasOpen)
            {
                laser.gameObject.SetActive(false);
                return;
            }
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
            lowAmmoWarned = Current.Magazine <= Current.Data.magazineSize / 4;
            ApplyWeaponModel();
            AudioManager.Play(Sound.WeaponRaise, player.Position, 0.45f, 1f, SoundCategory.Weapons);
            AudioManager.Play(Sound.Equip, player.Position, 0.35f);
            MissionManager.Instance.Report(ObjectiveType.TrainingSwitchWeapon, index);
        }

        void OpenWheel()
        {
            WheelEntries.Clear();
            if (Inventory.Primary != null)
                WheelEntries.Add(WeaponEntry(Inventory.Primary, 0, !Inventory.PrimaryBlocked));
            if (Inventory.Sidearm != null)
                WheelEntries.Add(WeaponEntry(Inventory.Sidearm, 1, true));
            for (int i = 0; i < Inventory.Equipment.Count; i++)
            {
                var slot = Inventory.Equipment[i];
                WheelEntries.Add(new WheelEntry
                {
                    label = slot.Data.displayName, weapon = -1, equipment = i, enabled = slot.Count > 0, kind = slot.Data.kind,
                    current = slot == Inventory.SelectedSlot,
                    detail = slot.Data.consumable ? slot.Count + " left" : "equipment",
                });
            }
            if (WheelEntries.Count == 0) return;
            Vector2 mouse = GameInput.MousePosition;
            float margin = 200f * UITheme.Scale;
            WheelCenter = new Vector2(Mathf.Clamp(mouse.x, margin, Screen.width - margin), Mathf.Clamp(Screen.height - mouse.y, margin, Screen.height - margin));
            WheelHovered = -1;
            WheelOpen = true;
            AudioManager.Ui(Sound.UiHover, 0.4f);
        }

        WheelEntry WeaponEntry(Weapon weapon, int index, bool usable)
        {
            return new WheelEntry
            {
                label = weapon.Data.displayName, weapon = index, equipment = -1, enabled = usable, weaponData = weapon.Data,
                current = Inventory.CurrentIndex == index,
                detail = usable ? weapon.Magazine + " / " + weapon.Reserve : "blocked by shield",
            };
        }

        void UpdateWheel()
        {
            Vector2 mouse = GameInput.MousePosition;
            Vector2 offset = new Vector2(mouse.x, Screen.height - mouse.y) - WheelCenter;
            int previous = WheelHovered;
            if (GameInput.UsingGamepad)
            {
                // The right stick points at an entry; letting go keeps the last one highlighted.
                Vector2 stick = GameInput.RightStick;
                offset = stick.sqrMagnitude > 0.25f ? new Vector2(stick.x, -stick.y) * 100f * UITheme.Scale : Vector2.zero;
            }
            if (offset.magnitude < 40f * UITheme.Scale) { if (!GameInput.UsingGamepad) WheelHovered = -1; }
            else
            {
                float angle = Mathf.Atan2(offset.x, -offset.y) * Mathf.Rad2Deg;
                if (angle < 0f) angle += 360f;
                float step = 360f / WheelEntries.Count;
                WheelHovered = Mathf.FloorToInt((angle + step * 0.5f) / step) % WheelEntries.Count;
            }
            if (WheelHovered != previous && WheelHovered >= 0) AudioManager.Ui(Sound.UiHover, 0.25f);
            if (GameInput.Cancel || GameInput.KeyDown(KeyCode.Mouse1)) { WheelOpen = false; switchHeldSince = -1f; }
            else if (GameInput.KeyDown(KeyCode.Mouse0) && WheelHovered >= 0) { ChooseWheel(); switchHeldSince = -1f; }
        }

        void ChooseWheel()
        {
            WheelOpen = false;
            if (WheelHovered < 0 || WheelHovered >= WheelEntries.Count) return;
            var entry = WheelEntries[WheelHovered];
            if (!entry.enabled)
            {
                AudioManager.Ui(Sound.Empty, 0.5f);
                return;
            }
            if (entry.weapon >= 0) Switch(entry.weapon);
            else
            {
                Inventory.SelectedEquipment = entry.equipment;
                AudioManager.Play(Sound.Equip, player.Position, 0.5f);
                UIManager.Notify("Equipment: " + Inventory.SelectedSlot.Data.displayName);
            }
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
                AudioManager.Play2D(Sound.Empty, 0.6f, 1f, SoundCategory.Weapons);
                nextFireTime = Time.time + 0.3f;
                if (weapon.CanReload) StartReload();
            }
        }

        void StartReload()
        {
            reloadEnd = Time.time + Current.Data.reloadTime;
            lowAmmoWarned = false;
            AudioManager.Play(Sound.Reload, player.Position, 0.7f, 1f, SoundCategory.Weapons);
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
            if (!data.lessLethal) WeaponEffects.EjectShell(player.Parts.gunRoot.position, player.transform.right, data.category == WeaponCategory.Shotgun);
            GameManager.Instance.CameraRig.Shake(weapon.Recoil * 0.08f);
            AmmoFeedback(weapon);
            if (!hitSomeone) return;
            ShotsHit++;
            LastHitTime = Time.time;
            if (SaveManager.Settings.hitMarker) AudioManager.Play2D(Sound.Hit, 0.35f, 1f, SoundCategory.Interface);
        }

        // Low-ammo cue, automatic reload when the magazine runs dry, optional switch to the sidearm.
        void AmmoFeedback(Weapon weapon)
        {
            var settings = SaveManager.Settings;
            if (!lowAmmoWarned && weapon.Magazine > 0 && weapon.Magazine <= weapon.Data.magazineSize / 4)
            {
                lowAmmoWarned = true;
                AudioManager.Play2D(Sound.Empty, 0.25f, 1.4f, SoundCategory.Weapons);
            }
            if (weapon.Magazine > 0) return;
            if (weapon.CanReload)
            {
                if (settings.autoReload) StartReload();
            }
            else if (Inventory.CurrentIndex == 0 && settings.autoSwitchWhenEmpty && Inventory.Sidearm != null)
            {
                UIManager.Notify("Out of ammo: switching to sidearm");
                Switch(1);
            }
            else UIManager.Notify(weapon.Data.displayName + " is out of ammunition", true);
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
                AudioManager.Play2D(Sound.Empty, 0.6f, 1f, SoundCategory.Weapons);
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
