using System;
using System.Collections.Generic;
using UnityEngine;

namespace NaraTycoon
{
    public enum Step { Locked, Request, Bidding, Evaluate, Materials, SupplyTrip, Production, Inspect, Delivery, DeliveryTrip, Complete }
    public enum Resource { None, Aluminum, Copper }
    public enum Upgrade { Driver, Lab, Reserve, Clerk, Fleet }
    public enum Site { Office, Depot, Factory, School, Boulevard, Hospital, Park, Transit, Library, Market, Center, Plaza, FactoryTech, FactoryExpress }

    public sealed class Contract
    {
        public readonly string Item, Buyer, Benefit;
        public readonly Site Site;
        public readonly int Limit, Quality, Deadline, Quantity, MaterialUnits, Reward;
        public readonly Resource Material;
        public bool Critical => Site == Site.Hospital || Site == Site.Center;

        public Contract(string item, string buyer, string benefit, Site site, int limit,
            int quality, int deadline, int quantity, Resource material, int units, int reward)
        {
            Item = item; Buyer = buyer; Benefit = benefit; Site = site; Limit = limit;
            Quality = quality; Deadline = deadline; Quantity = quantity; Material = material;
            MaterialUnits = units; Reward = reward;
        }
    }

    public readonly struct Bid
    {
        public readonly string Company;
        public readonly int Price, Quality, Days, Slot;
        public readonly bool Licensed;

        public Bid(string company, int price, int quality, int days, bool licensed, int slot)
        {
            Company = company; Price = price; Quality = quality; Days = days; Licensed = licensed; Slot = slot;
        }
    }

    [Serializable]
    public sealed class Order
    {
        public Step Step;
        public float Timer, AutoWait;
        public int Bid = -1, Sample, Defects, Reward, Funding, Batch, TruckSlot;
        public bool Manual, Special;
    }

    public sealed class Procurement
    {
        public const int Goal = 5000;
        public const int BidSeconds = 30;
        public const int ProductionSeconds = 45;
        public const int MaxLevel = 3;
        const int EconomyVersion = 3;
        const int CurrentVersion = 4;
        const int DefaultOrderLimit = 2;
        const int MinRequestSeconds = 24;
        const int MaxRequestSeconds = 42;
        const float IncomePeriod = 26f;
        public static readonly Contract[] Catalog =
        {
            new("학교 책상·의자", "솔빛초등학교", "새 교실에서 학생들이 공부를 시작합니다.", Site.School, 210, 80, 5, 30, Resource.None, 0, 30),
            new("공공 LED 가로등", "도로관리과", "밤길이 밝아지고 시민들이 산책을 나옵니다.", Site.Boulevard, 290, 84, 5, 16, Resource.Aluminum, 4, 40),
            new("비상 발전기", "솔빛시립병원", "정전에도 응급실이 안전하게 운영됩니다.", Site.Hospital, 480, 91, 6, 2, Resource.Copper, 6, 53),
            new("스마트 벤치", "푸른숲공원", "시민들이 정원에서 쉬고 기기를 충전합니다.", Site.Park, 250, 78, 5, 12, Resource.Aluminum, 4, 35),
            new("스마트 버스 쉼터", "솔빛교통센터", "시민들이 버스 도착 시간을 확인합니다.", Site.Transit, 380, 85, 6, 8, Resource.Aluminum, 5, 43),
            new("디지털 열람대", "솔빛도서관", "도서관에 열린 디지털 자료실이 생깁니다.", Site.Library, 280, 82, 5, 20, Resource.None, 0, 36),
            new("공공 냉장 진열대", "늘봄전통시장", "상인들이 신선한 식재료를 안전하게 보관합니다.", Site.Market, 390, 85, 6, 10, Resource.Copper, 5, 44),
            new("재난 통합 센서", "도시안전센터", "도시의 재난 감지망이 실시간으로 연결됩니다.", Site.Center, 520, 92, 6, 24, Resource.Copper, 7, 57),
            new("태양광 충전 파고라", "시민광장", "광장이 밤에도 머물고 싶은 공간이 됩니다.", Site.Plaza, 455, 88, 6, 6, Resource.Aluminum, 7, 49)
        };

        public static readonly string[] Stages = { "첫 번째 조달", "원자재 공급 위기", "행복한 스마트시티" };
        public static readonly string[] UpgradeNames = { "배송 직원", "품질검사팀", "원자재 운송팀", "입찰 보조 직원", "물류 운영 시스템" };
        static readonly Upgrade[] OfficeAutomation = { Upgrade.Clerk };
        static readonly Upgrade[] DepotAutomation = { Upgrade.Reserve, Upgrade.Fleet };
        static readonly Upgrade[] SupplierAutomation = { Upgrade.Lab, Upgrade.Driver };
        public static readonly string[] UpgradeDetails =
        {
            "검사를 마친 물품을 직원이 트럭으로 납품합니다.",
            "검사팀이 제품을 확인하고 기업에 보완을 요청합니다.",
            "비축 차량이 기업에 원자재를 운송합니다.",
            "공고를 올리고 적격 기업을 선정합니다. 주요 계약의 기업 선정은 나르미가 맡습니다.",
            "납품·비축 차량이 빨라지고, 부족한 물량을 예산으로 자동 구매합니다."
        };
        static readonly int[,] Prices =
        {
            { 65, 120, 215 }, { 85, 155, 245 }, { 85, 140, 235 }, { 130, 230, 340 }, { 155, 255, 410 }
        };
        static readonly int[,] BudgetPrices =
        {
            { 0, 0, 0 }, { 0, 0, 0 }, { 140, 235, 360 }, { 0, 0, 0 }, { 240, 380, 600 }
        };
        static readonly int[] GrowthThresholds = { 0, 100, 500, 1000, 2000, 3000, Goal };
        static readonly int[] FacilityHearts = { 3, 4, 6, 9, 12, 15, 18 };
        const int StandardSpeedCost = 160;
        const int FullSpeedCost = 460;

        internal static int GrowthMilestones => GrowthThresholds.Length - 1;
        internal static int GrowthThreshold(int level) => GrowthThresholds[level];

        internal static int GrowthLevel(int earnedHearts)
        {
            int level = GrowthMilestones;
            while (level > 0 && earnedHearts < GrowthThreshold(level)) level--;
            return level;
        }
        static readonly float[] Cooldowns = { 16, 19, 18, 10, 0 };
        static readonly int[] OrderExpansionPrices = { 100, 250, 500 };
        static readonly int[] WarehouseCapacities = { 9, 18, 28, 42 };
        static readonly int[] WarehouseExpansionPrices = { 180, 320, 520 };
        const int SpecialChancePercent = 8;
        const int SpecialFundingPremium = 550;
        readonly System.Random random;

        [Serializable]
        public sealed class Progress
        {
            public int Version = CurrentVersion, Budget = 840, Hearts, Happiness, Aluminum, Copper, Wave, Upgrades;
            public int OrderSlotLevel, WarehouseLevel;
            public int TotalDelivered, SpeedTier, SpeedUnlocks;
            public float Clock, IncomeClock = IncomePeriod, NextRequestAt;
            public bool Ended, HasPlayerLocation;
            public Vector3 PlayerLocation;
            public float PlayerYaw;
            public bool[] Commissioned;
            public float[] ReorderAt, ReadyAt, ReadyAtSecond;
            public int[] Levels;
            public Order[] Orders;
        }

        public Progress Data { get; }
        public int Stage => Mathf.Min(2, Data.Wave);
        public int OrderLimit => DefaultOrderLimit + Data.OrderSlotLevel;
        public bool CanExpandOrders => Data.OrderSlotLevel < OrderExpansionPrices.Length;
        public int OrderExpansionPrice => CanExpandOrders ? OrderExpansionPrices[Data.OrderSlotLevel] : 0;
        public int Capacity => WarehouseCapacities[Data.WarehouseLevel];
        public bool CanExpandWarehouse => Data.WarehouseLevel < WarehouseExpansionPrices.Length;
        public int WarehouseExpansionPrice => CanExpandWarehouse ? WarehouseExpansionPrices[Data.WarehouseLevel] : 0;
        public int NextWarehouseCapacity => CanExpandWarehouse ? WarehouseCapacities[Data.WarehouseLevel + 1] : Capacity;
        public int Stored => Data.Aluminum + Data.Copper;
        public bool CanEnd => Data.Happiness >= Goal;
        public int Completed => Data.TotalDelivered;
        public int ActiveFacilities { get { int count = 0; foreach (bool active in Data.Commissioned) if (active) count++; return count; } }
        public int ActiveOrders { get { int count = 0; foreach (var order in Data.Orders) if (order.Step != Step.Locked) count++; return count; } }
        public int NewRequests { get { int count = 0; foreach (var order in Data.Orders) if (order.Step == Step.Request) count++; return count; } }
        public float TimeMultiplier => Data.SpeedTier switch { 1 => 1.5f, 2 => 2f, _ => 1f };
        public Action<int> Changed;
        public Action<int, string> Notice;
        public Action<int, bool, bool> Dispatch;
        public Action<int, string> Delivered;

        public Procurement(Progress data = null, int? seed = null)
        {
            random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            Data = data ?? new Progress();
            if (data == null)
            {
                Data.Orders = new Order[Catalog.Length];
                for (int i = 0; i < Data.Orders.Length; i++) Data.Orders[i] = new Order();
            }
            Data.Commissioned ??= new bool[Catalog.Length];
            Data.ReorderAt ??= new float[Catalog.Length];
            Data.Levels ??= new int[UpgradeNames.Length];
            Data.ReadyAt ??= new float[UpgradeNames.Length];
            Data.ReadyAtSecond ??= new float[UpgradeNames.Length];
            if (Data.Version == 1) MigrateLegacyOrders();
            if (Data.Version < EconomyVersion) MigrateEconomy();
            if (Data.Version < CurrentVersion)
            {
                for (int i = 0; i < Data.Orders.Length; i++)
                {
                    var order = Data.Orders[i];
                    if (order.Step <= Step.Locked || order.Step >= Step.Complete) continue;
                    order.Reward = RollReward(BaseHearts(i, order.Special));
                    order.Funding = RollReward(BaseFunding(i, order.Special));
                }
                Data.Version = CurrentVersion;
            }
            if (data == null)
            {
                Receive(0);
                ScheduleRequest();
            }
        }

        void MigrateLegacyOrders()
        {
            for (int i = 0; i < Catalog.Length; i++)
            {
                if (Data.Orders[i].Step == Step.Complete)
                {
                    Data.Commissioned[i] = true;
                    Data.TotalDelivered++;
                    Data.Orders[i] = new Order();
                }
                else if (Data.Orders[i].Step != Step.Locked)
                {
                    Data.Orders[i].Timer = Mathf.Min(Data.Orders[i].Timer,
                        Data.Orders[i].Step == Step.Bidding ? BidSeconds : ProductionSeconds);
                }
            }
            for (int i = 0; i < Data.Levels.Length; i++)
                Data.Levels[i] = (Data.Upgrades & (1 << i)) == 0 ? 0 : 1;
            Data.Wave = Data.Commissioned[1] ? 2 : Data.Commissioned[0] ? 1 : 0;
        }

        void MigrateEconomy()
        {
            // Preserve purchased storage and stock while removing storage from worker upgrades.
            int oldCapacity = 9 + (Has(Upgrade.Reserve) ? 9 + 4 * (Level(Upgrade.Reserve) - 1) : 0)
                + (Has(Upgrade.Fleet) ? 10 + 3 * (Level(Upgrade.Fleet) - 1) : 0);
            int needed = Mathf.Max(oldCapacity, Stored);
            while (CanExpandWarehouse && Capacity < needed) Data.WarehouseLevel++;

            if (Data.Version == 1 || Data.NextRequestAt == 0) ScheduleRequest();
            else Data.NextRequestAt = Data.Clock + Mathf.Clamp(Data.NextRequestAt - Data.Clock, 0, MaxRequestSeconds);
            foreach (var order in Data.Orders)
                if (order.Step == Step.Production && order.Sample == 0 && !order.Special)
                    order.Timer = Mathf.Min(order.Timer, ProductionSeconds + 5);
            Data.Version = EconomyVersion;
        }

        public static bool Valid(Progress data) => data != null && data.Version is >= 1 and <= CurrentVersion &&
            data.Orders != null && data.Orders.Length == Catalog.Length &&
            Array.TrueForAll(data.Orders, order => order != null && Enum.IsDefined(typeof(Step), order.Step)) &&
            (data.Version == 1 || data.Commissioned?.Length == Catalog.Length && data.ReorderAt?.Length == Catalog.Length &&
                data.Levels?.Length == UpgradeNames.Length && data.ReadyAt?.Length == UpgradeNames.Length) &&
            (data.Version < EconomyVersion || data.OrderSlotLevel >= 0 && data.OrderSlotLevel <= OrderExpansionPrices.Length &&
                data.WarehouseLevel >= 0 && data.WarehouseLevel < WarehouseCapacities.Length);

        public int Level(Upgrade upgrade) => Data.Levels[(int)upgrade];
        public bool Has(Upgrade upgrade) => Level(upgrade) > 0;
        public bool IsActive(int id) => Data.Commissioned[id];
        public float WorkerMultiplier(Upgrade upgrade) => Level(upgrade) switch { 2 => 1.3f, 3 => 1.65f, _ => 1f };
        public float FleetMultiplier => Has(Upgrade.Fleet) ? 1.18f + .14f * (Level(Upgrade.Fleet) - 1) : 1;
        public float Cooldown(Upgrade upgrade) => Cooldowns[(int)upgrade] * (Level(upgrade) switch { 2 => .7f, 3 => .45f, _ => 1f });
        public float RemainingCooldown(Upgrade upgrade, int slot = 0) =>
            Mathf.Max(0, (slot == 0 ? Data.ReadyAt : Data.ReadyAtSecond)[(int)upgrade] - Data.Clock);
        public int VehicleCount(Upgrade upgrade) => Level(upgrade) == MaxLevel ? 2 : 1;
        public int UpgradePrice(Upgrade upgrade) => Prices[(int)upgrade, Mathf.Min(Level(upgrade), MaxLevel - 1)];
        public int UpgradeBudgetPrice(Upgrade upgrade) => BudgetPrices[(int)upgrade, Mathf.Min(Level(upgrade), MaxLevel - 1)];
        public Site Supplier(int id) => Data.Orders[id].Bid switch { 1 => Site.FactoryTech, 2 => Site.FactoryExpress, _ => Site.Factory };
        public static bool IsSupplier(Site site) => site is Site.Factory or Site.FactoryTech or Site.FactoryExpress;
        internal static ReadOnlySpan<Upgrade> AutomationAt(Site site) => site switch
        {
            Site.Office => OfficeAutomation,
            Site.Depot => DepotAutomation,
            Site.Factory or Site.FactoryTech or Site.FactoryExpress => SupplierAutomation,
            _ => ReadOnlySpan<Upgrade>.Empty
        };

        public Resource RequiredMaterial(int id) => RequiredMaterial(id, Data.Orders[id].Special);
        public int RequiredUnits(int id) => RequiredUnits(id, Data.Orders[id].Special);

        static Resource RequiredMaterial(int id, bool special)
        {
            Resource material = Catalog[id].Material;
            return special && material == Resource.None ? Resource.Copper : material;
        }

        static int RequiredUnits(int id, bool special) =>
            special ? Mathf.Max(10, Catalog[id].MaterialUnits + 7) : Catalog[id].MaterialUnits;

        public int BudgetReward(int id) => Data.Orders[id].Funding;
        static int BaseHearts(int id, bool special) => Catalog[id].Reward * (special ? 3 : 1);
        static int BaseFunding(int id, bool special)
        {
            int materials = RequiredUnits(id, special) * UnitPrice(RequiredMaterial(id, special));
            return Catalog[id].Limit + 38 + materials + (special ? SpecialFundingPremium : 0);
        }

        public int RequiredQuality(int id) => Catalog[id].Quality + (Data.Orders[id].Special ? 4 : 0);
        public int Stock(Resource resource) => resource == Resource.Aluminum ? Data.Aluminum : Data.Copper;
        public static string MaterialName(Resource material) => material == Resource.Aluminum ? "알루미늄" : "구리";
        public static int UnitPrice(Resource material) => material == Resource.Aluminum ? 28 : 40;

        public bool Unlocked(Upgrade upgrade)
        {
            if (Has(upgrade)) return true;
            if (upgrade == Upgrade.Driver) return IsActive(0);
            if (upgrade == Upgrade.Reserve) return IsActive(1) || Data.Orders[1].Step >= Step.Production;
            if (upgrade == Upgrade.Lab) return IsActive(1);
            return Stage == 2 && (upgrade != Upgrade.Fleet || Has(Upgrade.Reserve));
        }

        public bool Hire(Upgrade upgrade)
        {
            int price = UpgradePrice(upgrade);
            int budgetPrice = UpgradeBudgetPrice(upgrade);
            int level = Level(upgrade);
            if (level >= MaxLevel || !Unlocked(upgrade) || Data.Hearts < price || Data.Budget < budgetPrice) return false;
            float remaining = RemainingCooldown(upgrade);
            float previous = Cooldown(upgrade);
            Data.Levels[(int)upgrade]++;
            if (remaining > 0 && previous > 0) Data.ReadyAt[(int)upgrade] = Data.Clock + remaining * Cooldown(upgrade) / previous;
            Data.Hearts -= price;
            Data.Budget -= budgetPrice;
            Signal(-1, $"{UpgradeNames[(int)upgrade]} Lv.{level + 1} 운영 시작");
            return true;
        }

        public bool ExpandOrders()
        {
            if (!CanExpandOrders || Data.Hearts < OrderExpansionPrice) return false;
            Data.Hearts -= OrderExpansionPrice;
            Data.OrderSlotLevel++;
            Signal(-1, $"업무 슬롯 확장 · 최대 {OrderLimit}건");
            return true;
        }

        public bool ExpandWarehouse()
        {
            if (!CanExpandWarehouse || Data.Budget < WarehouseExpansionPrice) return false;
            Data.Budget -= WarehouseExpansionPrice;
            Data.WarehouseLevel++;
            Signal(-1, $"비축 창고 확장 · 최대 {Capacity}개");
            return true;
        }

        public int SpeedPrice(int tier) => HasSpeed(tier) ? 0 : tier == 1 ? StandardSpeedCost :
            FullSpeedCost - (HasSpeed(1) ? StandardSpeedCost : 0);

        public bool HasSpeed(int tier) => tier == 0 || (Data.SpeedUnlocks & (1 << tier)) != 0 ||
            (tier == 1 && (Data.SpeedUnlocks & (1 << 2)) != 0);

        public bool SelectSpeed(int tier)
        {
            if (tier is < 0 or > 2) return false;
            if (!HasSpeed(tier))
            {
                int price = SpeedPrice(tier);
                if (Data.Hearts < price) return false;
                Data.Hearts -= price;
                Data.SpeedUnlocks |= 1 << tier;
            }
            Data.SpeedTier = tier;
            Signal(-1, $"{TimeMultiplier:0.#}배속 적용");
            return true;
        }

        public bool Buy(Resource resource, int quantity)
        {
            int price = UnitPrice(resource) * quantity;
            if (resource == Resource.None || quantity <= 0 || quantity > Capacity - Stored || price > Data.Budget) return false;
            Data.Budget -= price;
            AdjustStock(resource, quantity);
            Signal(-1, $"{MaterialName(resource)} {quantity}개 입고");
            return true;
        }

        public bool Sell(Resource resource)
        {
            if (resource == Resource.None || Stock(resource) == 0) return false;
            AdjustStock(resource, -1);
            Data.Budget += UnitPrice(resource) * 3 / 4;
            Signal(-1, $"{MaterialName(resource)} 1개 출고");
            return true;
        }

        public static Bid[] Offers(int id)
        {
            Contract c = Catalog[id];
            Bid[] offers =
            {
                new("한결산업", c.Limit - 34, c.Quality + 4, c.Deadline - 1, true, 0),
                new("솔빛테크", c.Limit - 11, Mathf.Min(99, c.Quality + 7), c.Deadline - 2, true, 1),
                new("바로상사", c.Limit - 64, id % 2 == 0 ? c.Quality - 7 : c.Quality, c.Deadline, true, 2),
                new("미래물산", c.Limit - 49, c.Quality + 2, c.Deadline, false, 3)
            };
            int rotate = id % 4;
            return new[] { offers[rotate], offers[(rotate + 1) % 4], offers[(rotate + 2) % 4], offers[(rotate + 3) % 4] };
        }

        public string Ineligible(int id, Bid bid)
        {
            Contract c = Catalog[id];
            var problems = new List<string>(4);
            if (!bid.Licensed) problems.Add("참가자격 미충족");
            if (bid.Quality < RequiredQuality(id)) problems.Add("품질 기준 미달");
            if (bid.Days > c.Deadline) problems.Add("납기 초과");
            if (bid.Price > c.Limit) problems.Add("공고 상한 초과");
            if (bid.Price > Data.Budget) problems.Add("보유 예산 부족");
            return string.Join(" · ", problems);
        }

        public bool Publish(int id)
        {
            if (Data.Orders[id].Step != Step.Request) return false;
            Set(id, Step.Bidding, BidSeconds, $"입찰공고 게시 · {BidSeconds}초 후 마감");
            return true;
        }

        public bool Award(int id, Bid bid)
        {
            if (Data.Orders[id].Step != Step.Evaluate || Ineligible(id, bid).Length > 0) return false;
            Data.Budget -= bid.Price;
            Data.Orders[id].Bid = bid.Slot;
            if (RequiredMaterial(id) != Resource.None)
                Set(id, Step.Materials, 0, $"{bid.Company} 계약 · 원자재 공급 요청");
            else
                Produce(id, $"{bid.Company} 계약 · 생산 시작");
            return true;
        }

        public bool Send(int id, bool supply, bool manual, int truckSlot = 0)
        {
            Order order = Data.Orders[id];
            if (order.Step != (supply ? Step.Materials : Step.Delivery)) return false;
            if (supply)
            {
                Resource material = RequiredMaterial(id);
                int units = RequiredUnits(id);
                if (Stock(material) < units) return false;
                AdjustStock(material, -units);
            }
            order.Manual = manual;
            order.TruckSlot = manual ? 0 : truckSlot;
            Set(id, supply ? Step.SupplyTrip : Step.DeliveryTrip, 0, supply ? "원자재 상차 완료" : "납품 물품 상차 완료");
            Dispatch?.Invoke(id, supply, manual);
            return true;
        }

        public void Arrive(int id)
        {
            var order = Data.Orders[id];
            if (order.Step == Step.SupplyTrip)
            {
                if (!order.Manual) Rest(Upgrade.Reserve, order.TruckSlot);
                order.Manual = false;
                Produce(id, "원자재 도착 · 기업 생산 재개");
                return;
            }
            if (order.Step != Step.DeliveryTrip) return;
            if (!order.Manual) Rest(Upgrade.Driver, order.TruckSlot);
            Contract c = Catalog[id];
            bool first = !IsActive(id);
            int reward = order.Reward;
            int funding = BudgetReward(id);
            Data.Budget += funding;
            Data.Hearts += reward;
            Data.Happiness += reward;
            Data.TotalDelivered++;
            Data.Commissioned[id] = true;
            Data.ReorderAt[id] = Data.Clock + random.Next(48, 97);
            Data.Orders[id] = new Order();
            if (first && id == Stage && Stage < 2) Data.Wave++;
            Changed?.Invoke(id);
            string outcome = order.Special ? "특별 조달 완료" : first ? $"{c.Buyer} 활성화" : $"{c.Buyer} 납품 완료";
            Delivered?.Invoke(id, $"{outcome} · 하트 +{reward} · 예산 +{funding:N0}만원");
        }

        public int SampleCount(int id) => Data.Orders[id].Special ? 7 : id == 0 ? 5 : 4;
        public bool HasDefect(int id)
        {
            Order order = Data.Orders[id];
            int sample = (order.Sample + order.Batch) % SampleCount(id);
            return sample == (id + 1) % SampleCount(id) ||
                (id == 0 && sample == 3) || ((order.Special || order.Bid == 0 && id > 0) && sample == (id + 3) % SampleCount(id));
        }

        public bool Inspect(int id, bool reject)
        {
            Order order = Data.Orders[id];
            if (order.Step != Step.Inspect || reject != HasDefect(id)) return false;
            if (reject) order.Defects++;
            order.Sample++;
            if (order.Sample >= SampleCount(id))
                Set(id, Step.Production, 14 + 4 * order.Defects, $"보완 요청 {order.Defects}건 · 교체품 재검 대기");
            else
                Changed?.Invoke(id);
            return true;
        }

        public void Tick(float dt, int inspecting, int driverFree, int supplyFree)
        {
            Data.Clock += dt;
            Data.IncomeClock -= dt;
            if (Data.IncomeClock <= 0)
            {
                Data.IncomeClock += IncomePeriod;
                int active = ActiveFacilities;
                int growth = GrowthLevel(Data.Happiness);
                Data.Budget += RollReward(26 + active * (7 + growth * 2));
                if (active > 0)
                {
                    int reward = RollReward(active * FacilityHearts[growth]);
                    Data.Hearts += reward;
                    Data.Happiness += reward;
                }
            }

            for (int i = 0; i < Data.Orders.Length; i++)
            {
                Order order = Data.Orders[i];
                if (order.Step == Step.Locked) continue;
                Contract c = Catalog[i];
                order.AutoWait += dt;
                switch (order.Step)
                {
                    case Step.Request when Ready(Upgrade.Clerk, order, 6):
                        if (Publish(i)) Rest(Upgrade.Clerk);
                        break;
                    case Step.Bidding:
                        if ((order.Timer -= dt) <= 0) Set(i, Step.Evaluate, 0, "입찰 마감 · 제안서 도착");
                        break;
                    case Step.Evaluate when (!c.Critical || IsActive(i)) && Ready(Upgrade.Clerk, order, 8):
                        foreach (var bid in Offers(i))
                            if (bid.Slot == 1 && Award(i, bid)) { Rest(Upgrade.Clerk); break; }
                        break;
                    case Step.Materials:
                        int reserveSlot = AvailableTruck(Upgrade.Reserve, order, 5, supplyFree);
                        if (reserveSlot < 0) break;
                        Resource material = RequiredMaterial(i);
                        int missing = RequiredUnits(i) - Stock(material);
                        if (missing > 0 && Has(Upgrade.Fleet)) Buy(material, missing);
                        if (Send(i, true, false, reserveSlot)) supplyFree &= ~(1 << reserveSlot);
                        break;
                    case Step.Production:
                        if ((order.Timer -= dt) <= 0)
                        {
                            if (order.Sample > 0) Set(i, Step.Delivery, 0, "교체품 적합 · 납품 가능");
                            else Set(i, Step.Inspect, 0, "생산 완료 · 품질검사 대기");
                        }
                        break;
                    case Step.Inspect when inspecting != i && Ready(Upgrade.Lab, order, 14):
                        while (order.Step == Step.Inspect) Inspect(i, HasDefect(i));
                        Rest(Upgrade.Lab);
                        break;
                    case Step.Delivery:
                        int driverSlot = AvailableTruck(Upgrade.Driver, order, 6, driverFree);
                        if (driverSlot >= 0 && Send(i, false, false, driverSlot)) driverFree &= ~(1 << driverSlot);
                        break;
                }
            }
            TryReceive();
        }

        bool Ready(Upgrade upgrade, Order order, float preparation) =>
            Has(upgrade) && RemainingCooldown(upgrade) <= 0 && order.AutoWait >= preparation / WorkerMultiplier(upgrade);

        int AvailableTruck(Upgrade upgrade, Order order, float preparation, int available)
        {
            if (!Has(upgrade) || order.AutoWait < preparation / WorkerMultiplier(upgrade)) return -1;
            for (int slot = 0; slot < VehicleCount(upgrade); slot++)
                if ((available & (1 << slot)) != 0 && RemainingCooldown(upgrade, slot) <= 0) return slot;
            return -1;
        }

        void Rest(Upgrade upgrade, int slot = 0) =>
            (slot == 0 ? Data.ReadyAt : Data.ReadyAtSecond)[(int)upgrade] = Data.Clock + Cooldown(upgrade);

        void TryReceive()
        {
            if (Data.Clock < Data.NextRequestAt) return;
            ScheduleRequest();
            if (ActiveOrders >= OrderLimit) return;
            if (Stage == 1 && !IsActive(1) && Data.Orders[1].Step == Step.Locked)
            {
                Receive(1);
                return;
            }
            int selected = -1, count = 0;
            for (int i = 0; i < Catalog.Length; i++)
            {
                if (Stage < 2 && i > Stage && Catalog[i].Site != Site.Library) continue;
                if (Data.Orders[i].Step == Step.Locked && Data.Clock >= Data.ReorderAt[i] && random.Next(++count) == 0)
                    selected = i;
            }
            if (selected >= 0) Receive(selected);
        }

        void ScheduleRequest() => Data.NextRequestAt = Data.Clock + random.Next(MinRequestSeconds, MaxRequestSeconds + 1);

        void Receive(int id)
        {
            bool special = Stage == 2 && Capacity >= 18 && random.Next(100) < SpecialChancePercent;
            Data.Orders[id] = new Order
            {
                Special = special,
                Reward = RollReward(BaseHearts(id, special)),
                Funding = RollReward(BaseFunding(id, special))
            };
            if (IsActive(id) || special) Data.Orders[id].Batch = random.Next(SampleCount(id));
            Set(id, Step.Request, 0, $"{(special ? "특별 조달요청" : "새 조달요청")} · {Catalog[id].Item}");
        }

        int RollReward(int amount)
        {
            int spread = amount / 10;
            return amount + random.Next(-spread, spread + 1);
        }

        void Produce(int id, string message) => Set(id, Step.Production,
            Data.Orders[id].Special ? random.Next(120, 161) : random.Next(ProductionSeconds - 5, ProductionSeconds + 6), message);

        void AdjustStock(Resource resource, int change)
        {
            if (resource == Resource.Aluminum) Data.Aluminum += change;
            else Data.Copper += change;
        }

        void Set(int id, Step step, float timer, string message)
        {
            var order = Data.Orders[id];
            order.Step = step;
            order.Timer = timer;
            order.AutoWait = 0;
            Signal(id, message);
        }

        void Signal(int id, string message)
        {
            Changed?.Invoke(id);
            Notice?.Invoke(id, message);
        }
    }
}
