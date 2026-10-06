using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    public enum SquadOrder { Follow, Hold, Regroup, MoveTo, Cover, Stack, StayBehind, ReturnToPlayer, AssistCivilians, Wait, Clear }
    public enum DoorAction { None, Open, Breach, Flash, Mirror, Shotgun, Gas }

    // One AI squadmate: a lightweight state machine driven by orders.
    // Combat runs on top of any order: officers shout compliance at suspects
    // who aren't attacking and only open fire on ones who are. When idle they
    // restrain surrendered suspects nearby; medics revive downed teammates.
    public class SquadAI : MonoBehaviour, ICombatTarget, IInteractable
    {
        public OfficerData Data { get; private set; }
        public OfficerLoadout Loadout { get; private set; }
        public int Index { get; private set; }
        public OfficerHealth Health { get; private set; }
        public WeaponInventory Inventory { get; private set; }
        public FlashlightController Flashlight { get; private set; }
        public SquadOrder Order { get; private set; }
        public string Status { get; private set; }
        public bool Selected { get; set; }
        public float NextThink { get; set; }
        public float ThinkScale { get { return 1f / Mathf.Max(0.5f, Data.commandResponsiveness); } }
        public DoorController StackDoor { get { return Order == SquadOrder.Stack ? stackDoor : null; } }
        public bool IsStacked { get { return Order == SquadOrder.Stack && stacked; } }
        public bool HoldsDoorsClosed { get { return Order == SquadOrder.StayBehind; } }
        public int Area { get; set; }
        public EnemyAI RestrainTarget { get { return restrainTarget; } }
        // +1 = stacking on the door's front side, -1 = back side.
        public float StackSide { get { return stackSide; } }
        public float HealthFraction { get { return Health.Fraction; } }

        // ICombatTarget
        public Transform Transform { get { return transform; } }
        public Vector3 Position { get { return transform.position; } }
        public Vector3 ChestPosition { get { return transform.position + Vector3.up * (crouched ? 0.85f : 1.2f); } }
        public bool IsAlive { get { return Health.IsAlive; } }
        public bool IsMoving { get { return mover.IsMoving; } }
        public bool IsCrouched { get { return crouched; } }
        public bool FlashlightOn { get { return Flashlight.On; } }
        public IDamageable Damageable { get { return Health; } }

        // IInteractable: reviving a downed officer.
        public string Prompt { get { return "[E] Revive " + Data.callsign + " (hold)"; } }
        public Vector3 InteractPosition { get { return transform.position + Vector3.up * 0.5f; } }

        AgentMover mover;
        OfficerController body;
        readonly Queue<Vector3> waypoints = new Queue<Vector3>();
        Vector3 holdPoint, facePoint, entryPoint;
        DoorController stackDoor;
        float stackSide;
        bool stacked, crouched;
        DoorAction doorAction;
        float doorActionAt = -1f, orderReadyAt, lastHurt = -100f, dazzledUntil, nextScan, nextRecon, taskTimer;
        EnemyAI target, restrainTarget;
        float targetSince, lastTargetSeen = -100f, nextShout, nextShot, reloadEnd;
        SquadAI reviveTarget;
        CivilianAI escort;
        PlayerController player;

        public static SquadAI Spawn(Transform parent, OfficerData data, OfficerLoadout loadout, int index, Vector3 position, float yaw)
        {
            var go = new GameObject(data.displayName);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.9f, 0f);
            collider.height = 1.8f;
            collider.radius = 0.35f;
            go.AddComponent<NavMeshAgent>();

            var squad = go.AddComponent<SquadAI>();
            squad.Data = data;
            squad.Loadout = loadout;
            squad.Index = index;
            var armor = GameData.Armor(loadout.armorId);
            float armorSpeed = armor != null ? armor.speedMultiplier : 1f;
            squad.mover = go.AddComponent<AgentMover>();
            squad.mover.Init(3.2f * data.moveSpeed * armorSpeed, 6.2f * data.moveSpeed * armorSpeed);
            squad.mover.Agent.avoidancePriority = 20 + index;
            squad.Health = go.AddComponent<OfficerHealth>();
            squad.Health.Init(data.maxHealth, armor, data.armorRating, loadout.useShield);
            squad.Inventory = new WeaponInventory(loadout, null);

            var parts = CharacterFactory.Build(go.transform, PlayerController.OfficerAppearance(data, loadout, false));
            CharacterFactory.SetWeapon(parts, squad.Inventory.Current.Data, squad.Inventory.CurrentIndex == 0 ? loadout : null);
            squad.body = go.AddComponent<OfficerController>();
            squad.body.Init(parts);
            squad.Flashlight = go.AddComponent<FlashlightController>();
            float range = 12f * squad.Inventory.Primary.LightRangeMultiplierOrOne();
            // Squad flashlight beams are only drawn on the highest tiers; their gameplay effect always applies.
            squad.Flashlight.Init(CharacterFactory.AddFlashlight(parts, range), range, (int)QualityManager.Instance.Tier >= (int)QualityTier.High);

            squad.Order = SquadOrder.Follow;
            squad.Status = "Following";
            squad.holdPoint = position;
            Shapes.SetLayer(go, Layers.Characters);
            return squad;
        }

        // ---- Orders ----

        public void Command(SquadOrder order, Vector3 point, DoorController door, DoorAction action, bool append = false)
        {
            if (!IsAlive) return;
            player = GameManager.Instance.Player;
            // Command responsiveness: a short delay before the officer reacts.
            orderReadyAt = Time.time + 0.35f / Mathf.Max(0.5f, Data.commandResponsiveness) / SquadCommandManager.Instance.ResponseBoost;
            escort = null;
            if (!append) waypoints.Clear();
            stacked = false;
            doorAction = DoorAction.None;
            doorActionAt = -1f;
            Order = order;
            switch (order)
            {
                case SquadOrder.Hold:
                case SquadOrder.StayBehind:
                case SquadOrder.Wait:
                    holdPoint = transform.position;
                    facePoint = point;
                    mover.Stop();
                    break;
                case SquadOrder.MoveTo:
                    waypoints.Enqueue(SquadFormation.Spread(point, SquadCommandManager.Instance.SlotFor(this)));
                    break;
                case SquadOrder.Cover:
                    facePoint = point;
                    holdPoint = transform.position;
                    if (Vector3.Distance(point, transform.position) > 12f) holdPoint = Vector3.MoveTowards(point, transform.position, 6f);
                    break;
                case SquadOrder.Stack:
                    stackDoor = door;
                    doorAction = action;
                    var leader = player != null ? player.Position : transform.position;
                    stackSide = Vector3.Dot(leader - door.transform.position, door.transform.forward) >= 0f ? 1f : -1f;
                    break;
            }
            Status = OrderLabel(order);
        }

        public void AddWaypoint(Vector3 point)
        {
            if (Order != SquadOrder.MoveTo) Command(SquadOrder.MoveTo, point, null, DoorAction.None);
            else waypoints.Enqueue(point);
        }

        public IEnumerable<Vector3> Waypoints { get { return waypoints; } }

        public void ExecuteDoorAction(DoorAction action)
        {
            if (Order != SquadOrder.Stack) return;
            doorAction = action;
        }

        // The door they were stacked on opened: flow into the room.
        public void BeginClear(DoorController door, int slot)
        {
            if (!IsAlive) return;
            var room = door.FarRoom(door.transform.position + door.transform.forward * stackSide * 2f);
            Order = SquadOrder.Clear;
            entryPoint = SquadFormation.EntryPoint(room, door, slot);
            facePoint = room != null ? room.Bounds.center : door.transform.position;
            mover.MoveTo(entryPoint, true);
            Status = "Clearing";
        }

        public static string OrderLabel(SquadOrder order)
        {
            switch (order)
            {
                case SquadOrder.Follow: return "Following";
                case SquadOrder.Hold: return "Holding";
                case SquadOrder.Regroup: return "Regrouping";
                case SquadOrder.MoveTo: return "Moving";
                case SquadOrder.Cover: return "Covering";
                case SquadOrder.Stack: return "Stacking";
                case SquadOrder.StayBehind: return "Staying behind";
                case SquadOrder.ReturnToPlayer: return "Returning";
                case SquadOrder.AssistCivilians: return "Assisting civilians";
                case SquadOrder.Clear: return "Clearing";
                default: return "Waiting";
            }
        }

        // ---- Every frame ----

        public void FrameUpdate(float dt)
        {
            if (!IsAlive) return;
            if (IsReloading && Time.time >= reloadEnd) FinishReload();

            bool engaging = target != null && Time.time - lastTargetSeen < 1.5f;
            if (engaging)
            {
                mover.Face(target.Position, 420f, dt);
                if (Time.time >= targetSince && CanShoot()) TryFire();
            }
            else if (!mover.IsMoving && (Order == SquadOrder.Hold || Order == SquadOrder.Cover || Order == SquadOrder.Clear || Order == SquadOrder.Wait || Order == SquadOrder.StayBehind))
            {
                if ((facePoint - transform.position).sqrMagnitude > 1f) mover.Face(facePoint, 180f, dt);
            }
            else if (IsStacked && stackDoor != null)
            {
                mover.Face(stackDoor.transform.position, 240f, dt);
            }

            Pose pose = taskTimer > 0f ? Pose.Treating : Health.Bracing ? Pose.Shielding : Pose.Aim;
            body.Animator.SetPose(pose);
            body.Animator.SetReload(IsReloading ? 1f - (reloadEnd - Time.time) / Inventory.Current.ReloadTime : 0f);
            body.Animator.Tick(dt, mover.Speed, mover.IsRunning);
        }

        bool CanShoot()
        {
            if (Time.time < dazzledUntil || IsReloading || target == null || !target.IsArmedThreat) return false;
            if (Order == SquadOrder.StayBehind && Time.time - lastHurt > 3f) return false; // hold fire unless fired upon
            Vector3 to = target.Position - transform.position;
            to.y = 0f;
            return Vector3.Angle(transform.forward, to) < 12f;
        }

        // ---- A few times a second ----

        public void Think()
        {
            if (!IsAlive) return;
            if (player == null) player = GameManager.Instance.Player;
            mover.TrackProgress();
            body.ShowSelected(Selected, false);

            Perceive();
            bool engaging = target != null && Time.time - lastTargetSeen < 1.5f;
            if (engaging && Order != SquadOrder.Regroup && Order != SquadOrder.ReturnToPlayer && Order != SquadOrder.Clear)
            {
                mover.Stop();
                Status = "Engaging";
                return;
            }
            if (Time.time < orderReadyAt) return;

            RoleUpkeep();
            if (gassedUntil > 0f && Time.time > gassedUntil) gassedUntil = 0f;
            UpdatePace();
            // A loose weapon right next to them gets picked up on the way past.
            var loose = DroppedWeapon.Nearest(transform.position, 1.8f);
            if (loose != null) loose.Secure(this);
            if (DoSideTasks()) return;

            switch (Order)
            {
                case SquadOrder.Follow:
                    if (player == null) break;
                    Vector3 slot = SquadFormation.FollowSlot(SquadCommandManager.Instance.SlotFor(this), player.Position, SquadCommandManager.Instance.Heading);
                    float distance = Vector3.Distance(slot, transform.position);
                    if (distance > 1.5f) mover.MoveTo(slot, distance > 6f || player.IsSprinting);
                    else if (mover.HasArrived) mover.Stop();
                    Status = "Following";
                    break;

                case SquadOrder.Regroup:
                case SquadOrder.ReturnToPlayer:
                    if (player == null) break;
                    if (Vector3.Distance(player.Position, transform.position) < 2.5f)
                    {
                        if (Order == SquadOrder.Regroup) Command(SquadOrder.Follow, player.Position, null, DoorAction.None);
                        else Command(SquadOrder.Wait, player.Position + player.AimDirection * 4f, null, DoorAction.None);
                        orderReadyAt = 0f;
                    }
                    else mover.MoveTo(player.Position, Order == SquadOrder.Regroup);
                    break;

                case SquadOrder.Hold:
                case SquadOrder.Cover:
                case SquadOrder.StayBehind:
                case SquadOrder.Wait:
                    if (Vector3.Distance(holdPoint, transform.position) > 1f) mover.MoveTo(holdPoint, false);
                    crouched = Order == SquadOrder.Cover || Order == SquadOrder.StayBehind;
                    body.Animator.SetCrouch(crouched);
                    break;

                case SquadOrder.MoveTo:
                    if (mover.HasArrived)
                    {
                        if (waypoints.Count > 0) mover.MoveTo(waypoints.Dequeue(), false);
                        else
                        {
                            Order = SquadOrder.Hold;
                            holdPoint = transform.position;
                            facePoint = transform.position + transform.forward * 3f;
                            Status = "Holding";
                        }
                    }
                    break;

                case SquadOrder.Stack:
                    UpdateStack();
                    break;

                case SquadOrder.Clear:
                    if (mover.HasArrived)
                    {
                        var room = GameManager.Instance.Level.RoomAt(transform.position);
                        if (room != null && !AIManager.Instance.AnyThreatIn(room) && Status != "Room clear")
                        {
                            Status = "Room clear";
                            SquadCommandManager.Instance.Radio(this, "Room clear!");
                        }
                    }
                    break;

                case SquadOrder.AssistCivilians:
                    UpdateAssist();
                    break;
            }

            if (mover.IsStuck)
            {
                // Can't get there: give up on the move and report.
                mover.Stop();
                if (Order == SquadOrder.MoveTo) waypoints.Clear();
                SquadCommandManager.Instance.Radio(this, "Can't get there, holding here.");
                Order = SquadOrder.Hold;
                holdPoint = transform.position;
                Status = "Holding";
            }
        }

        void Perceive()
        {
            if (Time.time < nextScan) return;
            nextScan = Time.time + 0.2f;
            if (Time.time < dazzledUntil) { target = null; return; }

            Vector3 eye = transform.position + Vector3.up * 1.6f;
            EnemyAI best = null;
            float bestScore = float.MaxValue;
            foreach (var enemy in AIManager.Instance.Enemies)
            {
                if (enemy.IsNeutralized || enemy.Area != Area) continue;
                float distance = Vector3.Distance(enemy.Position, transform.position);
                if (distance > Data.perceptionRange * 1.3f) continue;
                float visibility = AIVisibility.VisibilityOf(enemy.Position, enemy.Body.IsCrouched, false);
                if (!AIVisibility.CanSee(eye, transform.forward, 240f, Data.perceptionRange, enemy.Head, visibility)) continue;

                bool hostile = enemy.State == EnemyState.Attacking || enemy.State == EnemyState.TakingCover || enemy.State == EnemyState.Chasing || enemy.State == EnemyState.Alert;
                if (!enemy.IsArmedThreat || !hostile)
                {
                    // Not attacking (or unarmed): shout compliance instead of shooting.
                    if (enemy.State != EnemyState.Surrendering && Time.time > nextShout && distance < 12f)
                    {
                        nextShout = Time.time + 2f;
                        enemy.HearShout(transform.position, false);
                        SquadCommandManager.Instance.Radio(this, enemy.Data.armed ? "Police! Drop the weapon!" : "Police! Get on the ground!", false);
                    }
                    continue;
                }
                float score = distance - (enemy.State == EnemyState.Attacking ? 5f : 0f);
                if (score < bestScore)
                {
                    best = enemy;
                    bestScore = score;
                }
            }

            if (best != null)
            {
                if (best != target)
                {
                    target = best;
                    targetSince = Time.time + Data.reactionTime / SquadCommandManager.Instance.ResponseBoost;
                    if (Time.time - lastTargetSeen > 4f) SquadCommandManager.Instance.Radio(this, "Contact!");
                }
                lastTargetSeen = Time.time;
            }
            else if (target != null && (target.IsNeutralized || Time.time - lastTargetSeen > 1.5f))
            {
                target = null;
            }
        }

        void TryFire()
        {
            var weapon = Inventory.Current;
            if (Time.time < nextShot) return;
            if (weapon.Magazine <= 0)
            {
                if (weapon.CanReload) StartReload();
                else if (Inventory.CurrentIndex == 0) SwitchToSidearm();
                return;
            }
            Vector3 origin = ChestPosition;
            Vector3 aim = target.Position + Vector3.up * 1.1f;
            if (!WeaponEffects.ClearShot(origin, aim, transform)) return;

            var data = weapon.Data;
            weapon.Magazine--;
            nextShot = Time.time + Mathf.Max(1f / Mathf.Max(0.1f, data.fireRate), 0.12f) * (weapon.Automatic ? 1f : 1.6f);
            float distance = Vector3.Distance(origin, aim);
            float chance = Data.accuracy * SquadCommandManager.Instance.AccuracyBoost * Mathf.Clamp01(1.3f - distance / Mathf.Max(1f, data.range));
            if (target.Body.IsCrouched) chance -= 0.1f;
            bool onTarget = Random.value < Mathf.Clamp(chance, 0.1f, 0.95f);
            Vector3 direction = (aim - origin).normalized;
            if (!onTarget) direction = Quaternion.Euler(0f, Random.Range(3f, 8f) * (Random.value < 0.5f ? -1f : 1f), 0f) * direction;

            var damage = new DamageInfo { amount = data.damage, attacker = Team.Police, lessLethal = data.lessLethal, stun = data.stunDuration, weapon = data };
            Vector3 muzzle = body.Parts.muzzle.position;
            for (int i = 0; i < Mathf.Max(1, data.pellets); i++)
                WeaponEffects.Shoot(origin, i == 0 ? direction : WeaponEffects.Scatter(direction, data.spread), data.range, damage, muzzle, data.tracerColor);
            body.Animator.Fire(Mathf.Clamp(0.45f + data.kick * 0.45f, 0.4f, 1.5f));
            WeaponEffects.Fired(body.Parts.muzzle, data, 0.7f, weapon.NoiseRadius, NoiseKind.Gunshot);
            if (data.ejectsShells) WeaponEffects.EjectShell(body.Parts.gunRoot.position, transform.right, data.category == WeaponCategory.Shotgun || data.category == WeaponCategory.AutoShotgun);
        }

        bool IsReloading { get { return reloadEnd > 0f; } }

        void StartReload()
        {
            reloadEnd = Time.time + Inventory.Current.ReloadTime;
            AudioManager.Play(Sound.Reload, transform.position, 0.6f, 1f, SoundCategory.Weapons);
        }

        void FinishReload()
        {
            reloadEnd = 0f;
            Inventory.Current.FinishReload();
        }

        void SwitchToSidearm()
        {
            Inventory.CurrentIndex = 1;
            CharacterFactory.SetWeapon(body.Parts, Inventory.Current.Data, null);
        }

        // Role behaviours that run whenever the officer isn't fighting.
        void RoleUpkeep()
        {
            if (Data.role == OfficerRole.Shield)
                Health.SetBracing(Order == SquadOrder.Hold || Order == SquadOrder.Cover || IsStacked || Order == SquadOrder.Clear && mover.HasArrived);
            if (Data.role == OfficerRole.Recon && Time.time > nextRecon)
            {
                nextRecon = Time.time + 20f;
                TacticalIntel.Instance.RevealAround(transform.position, 8f, 4f);
            }
            bool dark = !AIVisibility.IsLit(transform.position + transform.forward * 3f);
            if (dark != Flashlight.On) Flashlight.Set(dark);
        }

        // Restrain nearby surrendered suspects; medics revive and heal teammates.
        bool DoSideTasks()
        {
            bool free = Order == SquadOrder.Follow || Order == SquadOrder.Hold || Order == SquadOrder.Wait || Order == SquadOrder.Cover || Order == SquadOrder.AssistCivilians || Order == SquadOrder.Clear;
            if (!free) return false;

            if (taskTimer > 0f)
            {
                taskTimer -= NextThinkInterval;
                if (taskTimer > 0f) return true;
                CompleteTask();
                return true;
            }

            if (Data.role == OfficerRole.Medic && reviveTarget == null)
            {
                foreach (var officer in AIManager.Instance.Officers)
                    if (officer != this && !officer.IsAlive && officer.Area == Area && Vector3.Distance(officer.Position, transform.position) < 15f) { reviveTarget = officer; break; }
                if (reviveTarget == null && player != null && player.Health.IsAlive && player.Health.Fraction < 0.5f
                    && Inventory.CountOf(EquipmentKind.MedicalKit) > 0 && Vector3.Distance(player.Position, transform.position) < 12f && Time.time - lastHeal > 20f)
                {
                    mover.MoveTo(player.Position, true);
                    if (Vector3.Distance(player.Position, transform.position) < 2f)
                    {
                        Inventory.Consume(EquipmentKind.MedicalKit);
                        player.Health.HealOverTime(GameData.Equipment("medkit").healAmount * 1.5f, 3f);
                        lastHeal = Time.time;
                        SquadCommandManager.Instance.Radio(this, "Hold still, patching you up.");
                    }
                    Status = "Treating";
                    return true;
                }
            }
            if (reviveTarget != null)
            {
                if (reviveTarget.IsAlive) { reviveTarget = null; return false; }
                if (Vector3.Distance(reviveTarget.Position, transform.position) > 1.6f)
                {
                    mover.MoveTo(reviveTarget.Position, true);
                    Status = "Reviving " + reviveTarget.Data.callsign;
                }
                else
                {
                    mover.Stop();
                    taskTimer = 3f;
                }
                return true;
            }

            if (restrainTarget == null)
            {
                foreach (var enemy in AIManager.Instance.Enemies)
                {
                    if (enemy.State != EnemyState.Surrendering || enemy.Area != Area || SquadCommandManager.Instance.IsClaimed(enemy, this)) continue;
                    if (Vector3.Distance(enemy.Position, transform.position) > 9f) continue;
                    restrainTarget = enemy;
                    break;
                }
            }
            if (restrainTarget != null)
            {
                if (restrainTarget.State != EnemyState.Surrendering) { restrainTarget = null; return false; }
                if (Vector3.Distance(restrainTarget.Position, transform.position) > 1.4f)
                {
                    mover.MoveTo(restrainTarget.Position, false);
                    Status = "Restraining suspect";
                }
                else
                {
                    mover.Stop();
                    taskTimer = 1.2f;
                }
                return true;
            }
            return false;
        }

        float lastHeal = -100f;
        float gassedUntil, gasRadioAt, pace = 1f;

        // Slower while choking on gas or limping on a wounded leg.
        void UpdatePace()
        {
            float wanted = (Time.time < gassedUntil ? 0.65f : 1f) * (Health.LegInjured ? 0.8f : 1f);
            if (Mathf.Approximately(wanted, pace)) return;
            pace = wanted;
            mover.SetSpeedMultiplier(pace);
        }

        // CS gas: without a gas mask they choke and slow down (and say so).
        public void Gassed()
        {
            if (Loadout != null && Loadout.faceIndex == (int)GearCatalog.Face.GasMask) return;
            gassedUntil = Time.time + 2f;
            UpdatePace();
            if (Time.time >= gasRadioAt)
            {
                gasRadioAt = Time.time + 12f;
                SquadCommandManager.Instance.Radio(this, "Gas! I've got no mask!");
            }
        }
        float NextThinkInterval { get { return QualityManager.Current.aiThinkInterval * ThinkScale; } }

        void CompleteTask()
        {
            if (reviveTarget != null)
            {
                bool kit = Inventory.Consume(EquipmentKind.MedicalKit);
                reviveTarget.Health.Revive(kit ? 45f : 25f);
                SquadCommandManager.Instance.Radio(this, "You're good, get back up.");
                reviveTarget = null;
            }
            else if (restrainTarget != null)
            {
                restrainTarget.Restrain(false);
                // Calls it in to TOC and picks up the suspect's gun if it's lying close by.
                TocReports.Report(restrainTarget, this);
                var gun = DroppedWeapon.ForOwner(restrainTarget);
                if (gun != null && (gun.transform.position - transform.position).sqrMagnitude < 9f) gun.Secure(this);
                restrainTarget = null;
            }
            else if (escort != null && escort.State == CivilianState.Injured)
            {
                escort.Treat();
            }
            else if (escort != null && escort.State == CivilianState.Captive)
            {
                escort.Free();
            }
        }

        void UpdateStack()
        {
            if (stackDoor == null) { Order = SquadOrder.Wait; return; }
            if (stackDoor.IsPassable && stacked)
            {
                SquadCommandManager.Instance.DoorOpened(stackDoor);
                return;
            }
            Vector3 slot = SquadFormation.StackSlot(stackDoor, stackSide, SquadCommandManager.Instance.StackIndex(this));
            if (Vector3.Distance(slot, transform.position) > 0.6f && !stacked)
            {
                mover.MoveTo(slot, true);
                Status = "Stacking up";
                return;
            }
            if (!stacked)
            {
                stacked = true;
                mover.Stop();
                Status = "Stacked";
                if (SquadCommandManager.Instance.StackIndex(this) == 0) SquadCommandManager.Instance.Radio(this, "In position.");
            }
            if (doorAction == DoorAction.None || SquadCommandManager.Instance.StackIndex(this) != 0 || !SquadCommandManager.Instance.StackReady(stackDoor)) return;

            if (doorActionAt < 0f)
            {
                doorActionAt = Time.time + 0.8f;
                return;
            }
            if (Time.time < doorActionAt) return;
            PerformDoorAction();
        }

        void PerformDoorAction()
        {
            var door = stackDoor;
            var action = doorAction;
            doorAction = DoorAction.None;
            // A trap someone has spotted gets disarmed first; that takes a moment.
            if (door.TrapKnown && action != DoorAction.Breach && action != DoorAction.Mirror)
            {
                door.DisarmTrap();
                SquadCommandManager.Instance.Radio(this, "Trap disarmed.");
                doorAction = action;
                doorActionAt = Time.time + 2f;
                return;
            }
            switch (action)
            {
                case DoorAction.Mirror:
                {
                    // A look under the door: who's inside and whether the door is rigged. Then hold.
                    var lines = ReconCamera.Look(door, transform.position);
                    UIManager.ShowRecon(lines);
                    SquadCommandManager.Instance.Radio(this, MirrorReport(lines, door));
                    break;
                }
                case DoorAction.Shotgun:
                {
                    var gunner = SquadCommandManager.Instance.ShotgunCarrier(door) ?? this;
                    if (!gunner.FireBreachingRound(door)) SquadCommandManager.Instance.Radio(this, door.State == DoorState.Wedged ? "It's wedged, the lock won't do it." : "Can't shotgun this one.");
                    break;
                }
                case DoorAction.Open:
                    if (door.State == DoorState.Closed) door.Open(transform.position);
                    else if (door.State == DoorState.Locked && door.Pickable) door.PickLock(transform.position);
                    else if (door.State == DoorState.Locked) SquadCommandManager.Instance.Radio(this, "It's locked. We need to breach.");
                    else if (door.State == DoorState.Wedged) door.RemoveWedge();
                    break;
                case DoorAction.Breach:
                    var carrier = SquadCommandManager.Instance.ChargeCarrier(door);
                    if (!door.Breachable) SquadCommandManager.Instance.Radio(this, "This door can't be breached.");
                    else if (carrier == null) SquadCommandManager.Instance.Radio(this, "Nobody has a breaching charge.");
                    else
                    {
                        carrier.Inventory.Consume(EquipmentKind.BreachingCharge);
                        door.PlaceCharge(transform.position, false);
                        SquadCommandManager.Instance.Radio(carrier, "Charge set! Stand by...");
                        MissionManager.Instance.ReportEquipment(EquipmentKind.BreachingCharge);
                    }
                    break;
                case DoorAction.Flash:
                    var thrower = SquadCommandManager.Instance.FlashCarrier(door);
                    if (thrower == null)
                    {
                        SquadCommandManager.Instance.Radio(this, "We're out of flashbangs.");
                        if (door.State == DoorState.Closed) door.Open(transform.position);
                        break;
                    }
                    if (door.State == DoorState.Closed) door.Open(transform.position);
                    else if (door.State == DoorState.Locked && door.Pickable) door.PickLock(transform.position);
                    if (!door.IsPassable)
                    {
                        SquadCommandManager.Instance.Radio(this, "Door's locked, can't get a flash in.");
                        break;
                    }
                    thrower.Inventory.Consume(EquipmentKind.Flashbang);
                    var room = door.FarRoom(transform.position);
                    Vector3 into = room != null ? room.Bounds.center : door.transform.position - door.transform.forward * stackSide * 3f;
                    into.y = 0.1f;
                    ThrownGrenade.Throw(GameData.Equipment("flashbang"), thrower.ChestPosition, into, false);
                    SquadCommandManager.Instance.Radio(thrower, "Flashbang out!");
                    SquadCommandManager.Instance.DelayClear(door, 1.8f);
                    MissionManager.Instance.ReportEquipment(EquipmentKind.Flashbang);
                    break;
                case DoorAction.Gas:
                {
                    // Like a flash, but CS gas, and a longer wait for it to spread before going in.
                    var gasser = SquadCommandManager.Instance.GasCarrier(door);
                    if (gasser == null)
                    {
                        SquadCommandManager.Instance.Radio(this, "We're out of gas.");
                        if (door.State == DoorState.Closed) door.Open(transform.position);
                        break;
                    }
                    if (door.State == DoorState.Closed) door.Open(transform.position);
                    else if (door.State == DoorState.Locked && door.Pickable) door.PickLock(transform.position);
                    if (!door.IsPassable)
                    {
                        SquadCommandManager.Instance.Radio(this, "Door's locked, can't get gas in.");
                        break;
                    }
                    gasser.Inventory.Consume(EquipmentKind.CSGas);
                    var gasRoom = door.FarRoom(transform.position);
                    Vector3 gasInto = gasRoom != null ? gasRoom.Bounds.center : door.transform.position - door.transform.forward * stackSide * 3f;
                    gasInto.y = 0.1f;
                    ThrownGrenade.Throw(GameData.Equipment("cs_gas"), gasser.ChestPosition, gasInto, false);
                    SquadCommandManager.Instance.Radio(gasser, "Gas out!");
                    SquadCommandManager.Instance.DelayClear(door, 3f);
                    MissionManager.Instance.ReportEquipment(EquipmentKind.CSGas);
                    break;
                }
            }
        }

        // The mirror report in a sentence: counts from the look, plus a warning for a trap.
        static string MirrorReport(System.Collections.Generic.List<string> lines, DoorController door)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 1; i < lines.Count; i++)
            {
                string line = lines[i];
                if (line.StartsWith("Coverage") || line.StartsWith("DOOR IS TRAPPED")) continue;
                parts.Add(line.TrimEnd('.'));
            }
            string report = "Mirror: " + (parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "nothing visible") + ".";
            if (door.TrapKnown) report += " The door's trapped!";
            return report;
        }

        // Fires a breaching round into the door's lock with the shotgun this officer carries.
        public bool FireBreachingRound(DoorController door)
        {
            var data = Inventory.Primary != null ? Inventory.Primary.Data : null;
            if (!door.ShotgunBreach(transform.position, false)) return false;
            body.Animator.Fire(1.2f);
            if (data != null) WeaponEffects.Fired(body.Parts.muzzle, data, 0.7f, 20f, NoiseKind.Gunshot);
            SquadCommandManager.Instance.Radio(this, "Breaching!");
            return true;
        }

        void UpdateAssist()
        {
            var level = GameManager.Instance.Level;
            if (escort == null || !escort.IsAlive || escort.IsEvacuated)
            {
                escort = AIManager.Instance.NearestCivilianNeedingHelp(transform.position, 25f, Area);
                if (escort == null)
                {
                    SquadCommandManager.Instance.Radio(this, "No more civilians nearby. Returning.");
                    Command(SquadOrder.Regroup, transform.position, null, DoorAction.None);
                    return;
                }
                SquadCommandManager.Instance.Radio(this, "I'll get that civilian out.");
            }

            if (escort.State == CivilianState.Following && escort.Leader == transform)
            {
                // Lead them to the nearest safe zone (via the stairs if it's on another floor).
                Vector3 goal = level.extraction != null ? level.extraction.Bounds.center : player.Position;
                foreach (var stairs in level.stairs)
                {
                    if (level.AreaAt(stairs.transform.position) != Area || stairs.DestinationArea >= Area) continue;
                    goal = stairs.transform.position;
                    if (Vector3.Distance(goal, transform.position) < 2f) GameManager.Instance.MoveThroughStairs(stairs, this, escort);
                    break;
                }
                goal.y = 0f;
                mover.MoveTo(goal, false);
                Status = "Escorting civilian";
                return;
            }

            if (Vector3.Distance(escort.Position, transform.position) > 1.6f)
            {
                mover.MoveTo(escort.Position, true);
                Status = "Assisting civilians";
                return;
            }
            mover.Stop();
            if (escort.State == CivilianState.Injured)
            {
                taskTimer = Data.role == OfficerRole.Medic ? 1.5f : 3f;
                Status = "Treating civilian";
            }
            else if (escort.State == CivilianState.Captive)
            {
                taskTimer = 1.5f;
                Status = "Freeing hostage";
            }
            else
            {
                escort.FollowLeader(transform);
            }
        }

        // ---- Events ----

        public void Dazzle(float seconds)
        {
            dazzledUntil = Mathf.Max(dazzledUntil, Time.time + seconds);
        }

        public void OnDowned()
        {
            mover.Disable();
            body.Animator.SetDown(true);
            body.ShowSelected(false, true);
            Flashlight.Set(false);
            Status = "DOWN";
            target = null;
            SquadCommandManager.Instance.Radio(this, "Officer down! " + Data.callsign + " is down!");
            AudioManager.Play2D(Sound.Hurt, 0.6f, 0.8f, SoundCategory.Voice);
            MissionManager.Instance.OnOfficerDown(this);
        }

        public void OnRevived()
        {
            body.Animator.SetDown(false);
            body.Parts.model.localRotation = Quaternion.identity;
            body.Parts.model.localPosition = Vector3.zero;
            body.Parts.ring.enabled = true;
            body.Parts.ShowWeapon(true);
            mover.Enable();
            mover.Warp(transform.position);
            Command(SquadOrder.Follow, transform.position, null, DoorAction.None);
            Status = "Following";
        }

        public void TeleportTo(Vector3 position, int area)
        {
            mover.Warp(position);
            Area = area;
            holdPoint = position;
        }

        public float InteractDuration(PlayerController p) { return p.Officer.role == OfficerRole.Medic ? 2f : 4f; }
        public bool CanInteract(PlayerController p) { return !IsAlive; }

        public void Interact(PlayerController p)
        {
            bool kit = p.Weapons.Inventory.Consume(EquipmentKind.MedicalKit);
            if (kit) MissionManager.Instance.ReportEquipment(EquipmentKind.MedicalKit);
            Health.Revive(kit ? 50f : 25f);
            UIManager.Notify(Data.callsign + " is back on their feet");
        }

        public void SetVisible(bool visible)
        {
            body.Parts.SetVisible(visible);
        }

        public void NotifyHurt(Vector3 direction)
        {
            lastHurt = Time.time;
            body.Animator.Hit(direction);
        }
    }
}
