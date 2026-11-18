using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NaraTycoon
{
    public sealed class NaraGame : MonoBehaviour
    {
        public enum ScreenState { Title, Playing, Paused, Ended }
        const string SaveKey = "NaraTycoon.Solbit.1";

        [SerializeField] CityWorld city;
        [SerializeField] Font regular, bold;
        readonly List<Trip> trips = new();
        GameHud hud;
        GameAudio audio;
        Trip manual;
        Procurement model;
        float refresh, saveClock, dustClock;
        bool track, changed;
        Site? focusedSite;
        Site? cameraZone;
        const float EnterDistance = 5.1f;
        const float ExitDistance = 6.8f;
        const float CameraEnterDistance = 3.2f;
        const float CameraExitDistance = 4.4f;
        int examining = -1;
        MMF_Player statusPulse;
        public ScreenState Mode { get; private set; }
        public Procurement Model => model;
        internal CityWorld City => city;
        internal int Examining => examining;
        internal bool Driving => manual != null;
        internal float Speed => manual == null ? 0 : Mathf.Abs(manual.Speed) * 3.6f;
        internal float Distance => manual == null ? 0 : Vector3.Distance(manual.Truck.position, manual.Path[^1]);
        internal bool Parked => manual != null && Distance < 3.45f && Mathf.Abs(manual.Speed) < 2.65f;
        internal Site Destination => manual == null ? Site.Office : manual.Target;
        internal Site Nearby { get; private set; }
        internal bool AtSite { get; private set; }
        internal bool CanContinue => Procurement.Valid(ReadSave());
        internal float MusicVolume => audio.Music;
        internal float EffectsVolume => audio.Sfx;

        sealed class Trip
        {
            public int Id, Index;
            public bool Supply, Manual, Returning;
            public Site Target;
            public Transform Truck;
            public Vector3[] Path;
            public Vector3 Direction;
            public float Speed;
        }

        public void Configure(CityWorld world, Font normal, Font heavy)
        {
            city = world;
            regular = normal;
            bold = heavy;
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            audio = new GameAudio(transform);
            hud = new GameHud(this, regular, bold);
            statusPulse = gameObject.AddComponent<MMF_Player>();
            statusPulse.AddFeedback(new MMF_Scale
            {
                AnimateScaleTarget = hud.HeartWidget,
                AnimateScaleDuration = .36f,
                UniformScaling = true,
                RemapCurveOne = 1.115f
            });
            statusPulse.Initialization();
            model = new Procurement();
            SetMode(ScreenState.Title);
        }

        public void Begin(bool resume)
        {
            model = new Procurement(resume ? ReadSave() : null);
            model.Changed = _ => changed = true;
            model.Notice = OnNotice;
            model.Dispatch = Dispatch;
            model.Delivered = OnDelivery;
            ClearTrips();
            city.Hero.position = city.Dock(Site.Office) + CityWorld.V(-2, .9f);
            city.Hero.rotation = Quaternion.Euler(0,195,0);
            if (model.Data.HasPlayerLocation && city.Free(model.Data.PlayerLocation, .24f))
            {
                city.Hero.position = model.Data.PlayerLocation;
                city.Hero.rotation = Quaternion.Euler(0, model.Data.PlayerYaw, 0);
            }
            AtSite = false;
            cameraZone = null;
            Focus(null);
            examining = -1;
            for (int i = 0; i < model.Data.Orders.Length; i++)
            {
                var order = model.Data.Orders[i];
                if (order.Step == Step.SupplyTrip || order.Step == Step.DeliveryTrip)
                {
                    Dispatch(i, order.Step == Step.SupplyTrip, order.Manual);
                    if (order.Manual && model.Data.HasPlayerLocation && city.Free(model.Data.PlayerLocation, .94f))
                    {
                        manual.Truck.position = model.Data.PlayerLocation;
                        manual.Truck.rotation = Quaternion.Euler(0, model.Data.PlayerYaw, 0);
                    }
                }
            }
            SetMode(model.Data.Ended ? ScreenState.Ended : ScreenState.Playing);
            city.UpdateActivity(model, model.Data.Clock);
            hud.OpenTab(GameHud.Tab.Contracts);
            if (Driving) FollowPlayer();
            else if (Mode == ScreenState.Playing && !UpdateSite(city.Hero.position, false)) FollowPlayer();
            else if (Mode == ScreenState.Ended) Focus(Site.Plaza);
            Save();
        }

        void OnNotice(int id, string message)
        {
            hud.Toast(message, false);
            if (id >= 0 && model.Data.Orders[id].Step is Step.Request or Step.Bidding or Step.Production or Step.Inspect)
                audio.Confirm();
        }

        void OnDelivery(int id, string message)
        {
            hud.Toast(message, true);
            city.Burst(city.Dock(Procurement.Catalog[id].Site) + Vector3.up, 46);
            statusPulse.PlayFeedbacks();
            audio.Reward();
        }

        void SetMode(ScreenState mode)
        {
            Mode = mode;
            ApplyTimeScale();
            audio.Duck(mode == ScreenState.Paused);
            hud.SetMode(mode);
        }

        void ApplyTimeScale() =>
            Time.timeScale = Mode == ScreenState.Playing ? model.TimeMultiplier : Mode == ScreenState.Paused ? 0 : 1;

        internal void SelectSpeed(int tier)
        {
            if (Mode != ScreenState.Playing) return;
            if (!model.SelectSpeed(tier))
            {
                hud.Toast($"행복 하트 {Procurement.SpeedPrice(tier):N0}개가 필요합니다.", false);
                return;
            }
            ApplyTimeScale();
            hud.ShowSpeed();
            Save();
        }

        internal void OpenEnding()
        {
            if (Mode != ScreenState.Playing || !model.CanEnd || Driving) return;
            model.Data.Ended = true;
            if (!Driving) Focus(Site.Plaza);
            city.Burst(city.Center(Site.Plaza) + Vector3.up * 2, 68);
            audio.Reward();
            SetMode(ScreenState.Ended);
            Save();
        }

        internal void TogglePause()
        {
            if (Mode == ScreenState.Paused) SetMode(ScreenState.Playing);
            else if (Mode == ScreenState.Playing)
            {
                Save();
                SetMode(ScreenState.Paused);
            }
        }

        internal void ReturnToTitle()
        {
            Save();
            ClearTrips();
            AtSite = false;
            cameraZone = null;
            Focus(null);
            SetMode(ScreenState.Title);
        }

        internal void Quit()
        {
            if (Mode != ScreenState.Title) Save();
            Application.Quit();
        }

        internal void Click() => audio.Click();
        internal void SetMusic(float value) => audio.SetMusic(value);
        internal void SetEffects(float value) => audio.SetEffects(value);
        internal void Focus(Site? site)
        {
            if (Driving) return;
            track = false;
            focusedSite = site;
            city.TrackPlayer(false);
            city.Focus(site);
        }

        internal void ChangeView()
        {
            if (Driving) return;
            if (track) Focus(null);
            else FollowPlayer();
        }

        void FollowPlayer()
        {
            track = true;
            focusedSite = null;
            city.Focus(null);
            city.TrackPlayer(true);
        }

        internal Site WorkSite(int id)
        {
            Step step = model.Data.Orders[id].Step;
            return step switch
            {
                Step.Request or Step.Bidding or Step.Evaluate => Site.Office,
                Step.Materials => Site.Depot,
                Step.SupplyTrip or Step.Production or Step.Inspect or Step.Delivery => model.Supplier(id),
                _ => Procurement.Catalog[id].Site
            };
        }

        internal void CenterOrder(int id) => Focus(WorkSite(id));

        internal bool CanUse(Site site, Upgrade? remote = null) =>
            Mode == ScreenState.Playing && !Driving && ((AtSite && Nearby == site) || (remote.HasValue && model.Has(remote.Value)));

        internal bool RequireVisit(Site site, Upgrade? remote = null)
        {
            if (CanUse(site, remote)) return true;
            if (!Driving) Focus(site);
            hud.Toast($"{CityWorld.Names[(int)site]} 업무 구역까지 이동하세요.", false);
            return false;
        }

        internal Upgrade? RemoteFor(int id) => model.Data.Orders[id].Step switch
        {
            Step.Request => Upgrade.Clerk,
            Step.Evaluate => !Procurement.Catalog[id].Critical || model.IsActive(id) ? Upgrade.Clerk : null,
            Step.Materials => Upgrade.Reserve,
            Step.Inspect => Upgrade.Lab,
            Step.Delivery => Upgrade.Driver,
            _ => null
        };

        internal bool Publish(int id) => RequireVisit(Site.Office, RemoteFor(id)) && model.Publish(id);

        internal void Review(int id, Bid bid)
        {
            if (!RequireVisit(Site.Office, RemoteFor(id))) return;
            if (model.Award(id, bid)) { hud.CloseModal(); Save(); }
            else hud.Toast(model.Ineligible(id, bid), false);
        }

        internal void OpenInspection(int id)
        {
            if (model.Data.Orders[id].Step != Step.Inspect || !RequireVisit(model.Supplier(id), Upgrade.Lab)) return;
            examining = id;
            hud.ShowInspection(id);
        }

        internal void CloseInspection() => examining = -1;

        internal void Judge(int id, bool reject)
        {
            if (!RequireVisit(model.Supplier(id), Upgrade.Lab)) return;
            if (!model.Inspect(id, reject))
            {
                hud.FlashInspection("판정을 다시 확인하세요. 제품의 상태와 측정값을 살펴보세요.", true);
                audio.Error();
                return;
            }
            city.Burst(city.Dock(model.Supplier(id)) + Vector3.up, 11);
            audio.Confirm();
            if (model.Data.Orders[id].Step == Step.Inspect)
            {
                hud.ShowInspection(id);
                hud.FlashInspection(reject ? "불량 표시를 기록했습니다." : "합격을 기록했습니다.", false);
            }
            else
                hud.CloseModal();
            Save();
        }

        internal void StartTrip(int id, bool supply)
        {
            if (Driving)
            {
                hud.Toast("현재 운송을 마친 뒤 새 업무에 출발하세요.", false);
                return;
            }
            Site origin = supply ? Site.Depot : model.Supplier(id);
            Upgrade worker = supply ? Upgrade.Reserve : Upgrade.Driver;
            if (!RequireVisit(origin, worker)) return;
            bool local = CanUse(origin);
            if (!local && (trips.Exists(t => !t.Manual && t.Supply == supply) || model.RemainingCooldown(worker) > 0))
            {
                hud.Toast("자동화 차량이 이동 중이거나 다음 업무를 준비하고 있습니다.", false);
                return;
            }
            if (!model.Send(id, supply, local))
            {
                hud.Toast("비축기지에 필요한 수량을 먼저 입고하세요.", false);
                return;
            }
            hud.CloseModal();
            Save();
        }

        void Dispatch(int id, bool supply, bool isManual)
        {
            Site supplier = model.Supplier(id);
            Site origin = supply ? Site.Depot : supplier;
            Site destination = supply ? supplier : Procurement.Catalog[id].Site;
            var path = city.Route(origin, destination);
            Transform truck = isManual ? city.ManualTruck : supply ? city.SupplyTruck : city.DeliveryTruck;
            var direction = (path[1] - path[0]).normalized;
            var trip = new Trip { Id = id, Index = 1, Supply = supply, Manual = isManual, Truck = truck, Path = path, Target = destination, Direction = direction };
            truck.SetPositionAndRotation(path[0], Quaternion.LookRotation(direction));
            truck.gameObject.SetActive(true);
            trips.Add(trip);
            if (!isManual) return;
            manual = trip;
            AtSite = false;
            cameraZone = null;
            city.Hero.gameObject.SetActive(false);
            city.ShowRoute(path);
            FollowPlayer();
        }

        void ClearTrips()
        {
            trips.Clear();
            manual = null;
            city.ManualTruck.gameObject.SetActive(false);
            city.DeliveryTruck.gameObject.SetActive(false);
            city.SupplyTruck.gameObject.SetActive(false);
            city.Hero.gameObject.SetActive(true);
            city.ShowRoute(null);
        }

        public void FinishDriving()
        {
            if (!Parked) return;
            var trip = manual;
            trip.Truck.gameObject.SetActive(false);
            city.Hero.position = trip.Path[^1] + CityWorld.V(-1.3f, .9f);
            city.Hero.gameObject.SetActive(true);
            trips.Remove(trip);
            manual = null;
            city.ShowRoute(null);
            model.Arrive(trip.Id);
            if (trip.Supply) city.Burst(trip.Path[^1] + Vector3.up, 20);
            UpdateSite(city.Hero.position, false);
            Save();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float movement = Mathf.Min(dt, .05f * model.TimeMultiplier);
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (Mode == ScreenState.Playing && hud.HasModal) hud.CloseModal();
                else TogglePause();
            }
            if (Mode == ScreenState.Playing)
            {
                if (!hud.HasModal && keyboard != null)
                {
                    if (keyboard.tabKey.wasPressedThisFrame) hud.OpenTab(GameHud.Tab.Contracts);
                    if (keyboard.vKey.wasPressedThisFrame) ChangeView();
                    if (keyboard.eKey.wasPressedThisFrame)
                    {
                        if (Driving) FinishDriving();
                        else if (AtSite) { Focus(Nearby); hud.Interact(Nearby); }
                    }
                }
                if (manual != null)
                    MoveManual(hud.HasModal ? Vector2.zero : InputVector(keyboard), keyboard != null && keyboard.spaceKey.isPressed, movement);
                else
                    MoveHero(hud.HasModal ? Vector2.zero : InputVector(keyboard), keyboard != null && keyboard.leftShiftKey.isPressed, movement);
                MoveWorkers(movement);
                int before = model.Data.Happiness;
                int deliveries = model.Completed;
                model.Tick(dt, examining,
                    trips.TrueForAll(t => t.Manual || t.Supply), trips.TrueForAll(t => t.Manual || !t.Supply));
                if (model.Data.Happiness > before && model.Completed == deliveries)
                {
                    statusPulse.PlayFeedbacks();
                    audio.Confirm();
                    for (int i = 0; i < Procurement.Catalog.Length; i++)
                        if (model.IsActive(i))
                            city.Burst(city.Dock(Procurement.Catalog[i].Site) + Vector3.up, 7);
                }
                saveClock += dt;
                if (changed || saveClock > 12) Save();
                city.UpdateActivity(model, model.Data.Clock);
            }
            float elapsed = Mode == ScreenState.Title ? 27f : model.Data.Clock;
            city.ApplyDay(elapsed);
            city.UpdateCamera(Driving ? manual.Truck : city.Hero, Mode == ScreenState.Title, Time.unscaledDeltaTime);
            refresh -= Time.unscaledDeltaTime;
            if (changed || refresh <= 0)
            {
                hud.Refresh(changed);
                changed = false;
                refresh = .19f;
            }
            hud.UpdateMarkers();
        }

        static Vector2 InputVector(Keyboard kb)
        {
            if (kb == null) return Vector2.zero;
            return new Vector2(
                (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0),
                (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0));
        }

        void MoveHero(Vector2 input, bool sprint, float dt)
        {
            Vector3 direction = WorldDirection(input);
            var pos = city.Hero.position;
            var previous = pos;
            Vector3 shift = direction * (sprint ? 11 : 7) * dt;
            Slide(ref pos, shift, .24f);
            pos.y = .10f;
            city.Hero.position = pos;
            if (direction.sqrMagnitude > .01f)
                city.Hero.rotation = Quaternion.Slerp(city.Hero.rotation, Quaternion.LookRotation(direction), dt * 13);
            bool moving = Mathf.Abs(pos.x - previous.x) + Mathf.Abs(pos.z - previous.z) > .001f;
            city.AnimateHero(moving);
            UpdateSite(pos, moving);
        }

        static Vector3 WorldDirection(Vector2 input) => Vector3.ClampMagnitude(CityWorld.V(input.x, input.y), 1);

        bool UpdateSite(Vector3 position, bool moving)
        {
            Site? previousSite = AtSite ? Nearby : null;
            float best = EnterDistance;
            for (int i = 0; i < CityWorld.Names.Length; i++)
            {
                Site site = (Site)i;
                float distance = Vector3.Distance(city.Dock(site), position);
                if (distance >= best) continue;
                best = distance;
                Nearby = site;
            }
            AtSite = best < EnterDistance || (previousSite.HasValue &&
                Vector3.Distance(city.Dock(previousSite.Value), position) <= ExitDistance);

            bool leftCameraZone = cameraZone.HasValue &&
                Vector3.Distance(city.Dock(cameraZone.Value), position) > CameraExitDistance;
            if (leftCameraZone) cameraZone = null;
            if (!cameraZone.HasValue && best <= CameraEnterDistance)
            {
                cameraZone = Nearby;
                Focus(Nearby);
                hud.Interact(Nearby);
                return true;
            }
            if (leftCameraZone || (previousSite.HasValue && !AtSite)) hud.LeaveSite();
            if (!track && (leftCameraZone || (moving && (!focusedSite.HasValue ||
                Vector3.Distance(city.Dock(focusedSite.Value), position) > CameraExitDistance))))
                FollowPlayer();
            return cameraZone.HasValue;
        }

        void MoveManual(Vector2 input, bool brake, float dt)
        {
            var truck = manual.Truck;
            var pos = truck.position;
            bool road = city.IsRoad(pos);
            float top = road ? 13.5f : 6.8f;
            Vector3 direction = brake ? Vector3.zero : WorldDirection(input);
            if (direction.sqrMagnitude > .01f)
            {
                manual.Direction = direction;
                truck.rotation = Quaternion.RotateTowards(truck.rotation, Quaternion.LookRotation(direction), dt * 540);
            }
            float target = direction.magnitude * top;
            manual.Speed = Mathf.MoveTowards(manual.Speed, target, (brake ? 34 : target == 0 ? 17 : 12) * dt);
            Vector3 shift = manual.Direction * manual.Speed * dt;
            Vector3 start = pos;
            Slide(ref pos, shift, .94f);
            if (shift.sqrMagnitude > .003f && (pos - start).sqrMagnitude < shift.sqrMagnitude * .08f)
                manual.Speed = 0;
            truck.position = pos;
            dustClock -= dt;
            if (!road && Mathf.Abs(manual.Speed) > 3 && dustClock < 0)
            {
                city.Burst(pos - truck.forward * 1.25f + Vector3.up * .22f, 2, true);
                dustClock = .18f;
            }
        }

        void Slide(ref Vector3 p, Vector3 delta, float margin)
        {
            Vector3 candidate = p + delta;
            if (city.Free(candidate, margin)) { p = candidate; return; }
            candidate = p + new Vector3(delta.x, 0, 0);
            if (city.Free(candidate, margin)) p = candidate;
            candidate = p + new Vector3(0, 0, delta.z);
            if (city.Free(candidate, margin)) p = candidate;
        }

        void MoveWorkers(float dt)
        {
            for (int i = trips.Count - 1; i >= 0; i--)
            {
                Trip trip = trips[i];
                if (trip.Manual) continue;
                float speed = 6.2f * model.FleetMultiplier * model.WorkerMultiplier(trip.Supply ? Upgrade.Reserve : Upgrade.Driver);
                Transform truck = trip.Truck;
                Vector3 to = trip.Path[trip.Index], dir = to - truck.position;
                if (dir.sqrMagnitude > .01f)
                    truck.rotation = Quaternion.RotateTowards(truck.rotation, Quaternion.LookRotation(dir), 224 * dt);
                truck.position = Vector3.MoveTowards(truck.position, to, speed * dt);
                if (Vector3.Distance(truck.position, to) > .06f || ++trip.Index < trip.Path.Length) continue;
                if (trip.Returning)
                {
                    truck.gameObject.SetActive(false);
                    trips.RemoveAt(i);
                    continue;
                }
                model.Arrive(trip.Id);
                if (trip.Supply) city.Burst(truck.position + Vector3.up, 19);
                Array.Reverse(trip.Path);
                trip.Index = 1;
                trip.Returning = true;
            }
        }

        static Procurement.Progress ReadSave()
        {
            try
            {
                var data = JsonUtility.FromJson<Procurement.Progress>(PlayerPrefs.GetString(SaveKey, ""));
                return Procurement.Valid(data) ? data : null;
            }
            catch (ArgumentException) { return null; }
        }

        void Save()
        {
            if (model == null || Mode == ScreenState.Title) return;
            Transform actor = Driving ? manual.Truck : city.Hero;
            model.Data.HasPlayerLocation = true;
            model.Data.PlayerLocation = actor.position;
            model.Data.PlayerYaw = actor.eulerAngles.y;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(model.Data));
            PlayerPrefs.Save();
            saveClock = 0;
        }

        void OnApplicationQuit() => Save();
        void OnApplicationFocus(bool focused) { if (!focused) Save(); }
        void OnDestroy() => Time.timeScale = 1;
    }
}
