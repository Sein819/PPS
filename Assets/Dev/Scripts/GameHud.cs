using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UI = UnityEngine.UI;

namespace NaraTycoon
{
    internal sealed class GameHud
    {
        internal enum Tab { Contracts, Stock, Staff }
        static readonly Color Ink = CityWorld.ColorOf("213D49");
        static readonly Color Muted = CityWorld.ColorOf("537177");
        static readonly Color Teal = CityWorld.ColorOf("23776E");
        static readonly Color Pale = CityWorld.ColorOf("DDEDE0");
        static readonly Color Paper = CityWorld.ColorOf("F9F3E3");
        static readonly Color White = CityWorld.ColorOf("FFFAEE");
        static readonly Color Line = CityWorld.ColorOf("E6DFCA");
        static readonly Color Gold = CityWorld.ColorOf("EEC56D");
        static readonly Color Rose = CityWorld.ColorOf("BE5969");
        static readonly Color Shade = new(.068f, .15f, .18f, .63f);
        static readonly string[] Tabs = { "나라장터", "비축기지", "자동화" };
        static readonly string[] StepText =
        {
            "공개 예정", "새 요청", "입찰 접수 중", "기업 선정", "원자재 부족", "원자재 운송 중",
            "생산 중", "품질검사 대기", "납품 대기", "납품 운송 중", "납품 완료"
        };

        readonly NaraGame game;
        readonly Font normal, bold;
        readonly Sprite round;
        readonly RectTransform canvas, overlay, main, map, top, desk, nav, toast, driveHUD, walkHint;
        readonly UI.Text notice, budget, hearts, happiness, day, stage, next, panelTitle, panelCount, driveText, driveValue, walkText;
        readonly UI.Image progress, noticeBag, clockDot;
        readonly RectTransform detail, list;
        readonly UI.Button arrivalButton, endingButton, expansionButton;
        readonly UI.Text speedText, expansionText;
        readonly UI.Button[] tabs = new UI.Button[3];
        readonly UI.Text[] tabText = new UI.Text[3];
        readonly List<(int id, UI.Text text)> orderTimers = new();
        readonly List<(Upgrade upgrade, UI.Text text)> staffTimers = new();
        readonly RectTransform[] markers = new RectTransform[CityWorld.Names.Length];
        readonly UI.Image[] markerBg = new UI.Image[CityWorld.Names.Length];
        readonly UI.Text[] markerText = new UI.Text[CityWorld.Names.Length];
        Site? contextSite;
        int modalId = -1;
        Order modalOrder;
        Step modalStep;
        UI.Text inspectionStatus;
        RectTransform modal;
        UI.Text detailTimer;
        float toastUntil;
        int selected = -1;
        int lastWave = -1, lastTab = -1;
        bool viewHidden, chooseManually;
        Tab active;
        internal Transform HeartWidget => hearts.transform.parent;
        internal bool HasModal => modal != null;
        Procurement Model => game.Model;

        internal GameHud(NaraGame owner, Font regularFont, Font boldFont)
        {
            game = owner;
            normal = regularFont;
            bold = boldFont;
            round = RoundedSprite();
            var go = new GameObject("Solbit municipal UI", typeof(RectTransform), typeof(Canvas),
                typeof(UI.CanvasScaler), typeof(UI.GraphicRaycaster));
            go.transform.SetParent(owner.transform, false);
            var screen = go.GetComponent<Canvas>();
            screen.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<UI.CanvasScaler>();
            scaler.uiScaleMode = UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = UI.CanvasScaler.ScreenMatchMode.Expand;
            canvas = (RectTransform)go.transform;
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Input System UI", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(owner.transform, false);
            }

            main = Make("Operations HUD", canvas);
            Stretch(main);
            map = Make("City markers", main);
            Stretch(map);
            for (int i = 0; i < markers.Length; i++)
            {
                var site = (Site)i;
                markers[i] = Button(map, "Site " + site, CityWorld.Names[i], Paper, Ink, 15, () =>
                {
                    game.Focus(site);
                    if (!game.Driving && game.AtSite && game.Nearby == site) Interact(site);
                }, out markerText[i]).GetComponent<RectTransform>();
                markers[i].sizeDelta = new Vector2(i == 4 || i == 9 ? 108 : 93, 31);
                markerBg[i] = markers[i].GetComponent<UI.Image>();
            }

            top = Panel(main, "Resource bar", Paper);
            TopLeft(top, 24, 22, 655, 75);
            var seal = Panel(top, "Brand icon", Teal);
            TopLeft(seal, 14, 13, 47, 48);
            var sealType = Label(seal, "市", 24, White, true, TextAnchor.MiddleCenter);
            Stretch(sealType.rectTransform);
            Short(top, "나라장터 타이쿤", 20, Ink, true, 73, 10, 160, 29);
            Short(top, "솔빛시 조달 상황실", 13, Muted, false, 74, 39, 146, 25);
            Sep(top, 227, 15, 1, 45);
            budget = Stat(top, "조달 예산", 246, 117, Teal);
            Sep(top, 369, 15, 1, 45);
            hearts = Stat(top, "행복 하트", 389, 98, Rose);
            Sep(top, 500, 15, 1, 45);
            happiness = Stat(top, "도시 행복도", 520, 121, Teal);
            progress = Flat(top, Teal);
            TopLeft(progress.rectTransform, 389, 62, 98, 3);

            var clock = Panel(main, "City time", Paper);
            TopRight(clock, 24, 22, 402, 75);
            clockDot = Panel(clock, "Day status", Gold).GetComponent<UI.Image>();
            TopLeft(clockDot.rectTransform, 16, 23, 29, 29);
            day = Short(clock, "", 17, Ink, true, 57, 10, 145, 52);
            var speed = Button(clock, "Game speed", "1배속", Pale, Teal, 16, ShowSpeed, out speedText);
            TopRight(speed.GetComponent<RectTransform>(), 122, 13, 75, 49);
            Right(Button(clock, "Pause", "일시정지", Pale, Teal, 15, game.TogglePause).GetComponent<RectTransform>(), 13, 13, 101, 49);

            desk = Panel(main, "Operations panel", Paper);
            StretchRight(desk, 24, 110, 123, 402);
            panelTitle = Short(desk, "나라장터", 26, Ink, true, 22, 13, 311, 38);
            panelCount = Short(desk, "", 14, Muted, false, 23, 53, 355, 26);
            expansionButton = Button(desk, "Expansion settings", "", Pale, Teal, 15,
                () => ShowExpansion(active == Tab.Stock), out expansionText);
            TopRight(expansionButton.GetComponent<RectTransform>(), 19, 16, 115, 34);
            Sep(desk, 23, 87, 356, 1);
            var scroll = Scroll(desk, out list);
            Stretch(scroll, 17, 17, 97, 155);
            detail = Make("Selected task", desk);
            BottomStretch(detail, 18, 18, 9, 141);

            nav = Panel(main, "Navigation", Paper);
            BottomLeft(nav, 24, 23, 655, 82);
            for (int i = 0; i < tabs.Length; i++)
            {
                var value = (Tab)i;
                tabs[i] = Button(nav, Tabs[i], Tabs[i], Pale, Teal, 19, () => OpenTab(value), out tabText[i]);
                TopLeft(tabs[i].GetComponent<RectTransform>(), 12 + i * 142, 14, 132, 54);
            }
            noticeBag = Panel(nav, "New request count", Rose).GetComponent<UI.Image>();
            TopLeft(noticeBag.rectTransform, 114, 10, 25, 25);
            noticeBag.raycastTarget = false;
            var badge = Label(noticeBag.rectTransform, "!", 16, White, true, TextAnchor.MiddleCenter);
            Stretch(badge.rectTransform);
            Sep(nav, 445, 16, 1, 49);
            TopLeft(Button(nav, "City overview", "도시 전경", Paper, Teal, 15, () => game.Focus(null)).GetComponent<RectTransform>(), 456, 14, 91, 54);
            TopLeft(Button(nav, "Follow camera", "나르미  V", Paper, Teal, 15, game.ChangeView).GetComponent<RectTransform>(), 550, 14, 94, 54);

            var milestone = Panel(main, "Stage progress", Ink);
            BottomRight(milestone, 24, 23, 402, 82);
            stage = Short(milestone, "", 19, White, true, 17, 9, 365, 31);
            next = Short(milestone, "", 14, Color.Lerp(White, Pale, .45f), false, 18, 42, 367, 27);
            endingButton = Button(milestone, "Ending unlock", "엔딩 보기", Gold, Ink, 17, ShowEnding);
            TopRight(endingButton.GetComponent<RectTransform>(), 13, 15, 122, 52);
            endingButton.gameObject.SetActive(false);

            walkHint = Panel(main, "Available interaction", Paper);
            BottomLeft(walkHint, 24, 115, 345, 49);
            walkText = Label(walkHint, "", 16, Ink, true);
            Stretch(walkText.rectTransform, 15, 15, 0, 0);

            driveHUD = Panel(main, "Driving information", Ink);
            BottomLeft(driveHUD, 24, 113, 655, 111);
            driveText = Short(driveHUD, "", 21, Gold, true, 17, 8, 446, 37);
            driveValue = Short(driveHUD, "", 15, White, false, 18, 46, 476, 31);
            Short(driveHUD, "WASD / 방향키 이동   ·   Space 제동", 13, Color.Lerp(White, Pale, .45f), false, 18, 78, 462, 23);
            arrivalButton = Button(driveHUD, "Unload", "하역하기  E", Gold, Ink, 17, game.FinishDriving);
            TopRight(arrivalButton.GetComponent<RectTransform>(), 12, 20, 141, 70);

            toast = Panel(canvas, "Event toast", Teal);
            AnchorTopCenter(toast, 0, 108, 460, 52);
            toast.GetComponent<UI.Image>().raycastTarget = false;
            notice = Label(toast, "", 17, White, true, TextAnchor.MiddleCenter);
            Stretch(notice.rectTransform, 15, 15, 0, 0);
            toast.gameObject.SetActive(false);
            overlay = Make("Game screens", canvas);
            Stretch(overlay);
        }

        internal void SetMode(NaraGame.ScreenState state)
        {
            CloseModal();
            if (state is NaraGame.ScreenState.Title or NaraGame.ScreenState.Ended) toast.gameObject.SetActive(false);
            ResetChildren(overlay);
            main.gameObject.SetActive(state == NaraGame.ScreenState.Playing || state == NaraGame.ScreenState.Paused);
            overlay.gameObject.SetActive(state != NaraGame.ScreenState.Playing);
            if (state == NaraGame.ScreenState.Title) BuildTitle();
            else if (state == NaraGame.ScreenState.Paused) BuildPause();
            else if (state == NaraGame.ScreenState.Ended) BuildEnd();
        }

        void BuildTitle()
        {
            var plaque = Panel(overlay, "Title card", Paper);
            At(plaque, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(59, 2), 483, 540);
            var mark = Panel(plaque, "Municipal seal", Teal);
            TopLeft(mark, 37, 34, 53, 53);
            Stretch(Label(mark, "市", 29, White, true, TextAnchor.MiddleCenter).rectTransform);
            Short(plaque, "솔빛시 조달 상황실", 17, Teal, true, 107, 37, 326, 42);
            Short(plaque, "나라장터", 69, Ink, true, 36, 99, 400, 96);
            Short(plaque, "타이쿤", 69, Teal, true, 36, 179, 400, 96);
            Sep(plaque, 39, 288, 405, 1);
            Short(plaque, "조달로 연결하고, 행복으로 성장하는 도시.", 18, Muted, false, 39, 305, 407, 49);
            bool saved = game.CanContinue;
            var start = Button(plaque, "Start game", saved ? "이어하기" : "새 게임 시작", Teal, White, 22, () => game.Begin(saved));
            TopLeft(start.GetComponent<RectTransform>(), 38, 372, 407, 62);
            if (saved)
                TopLeft(Button(plaque, "New city", "새 도시 시작", Pale, Teal, 17, AskReset).GetComponent<RectTransform>(), 38, 447, 407, 42);
            Short(plaque, "WASD 이동  ·  E 상호작용  ·  Tab 나라장터", 13, Muted, false, 39, 500, 412, 28);
        }

        void BuildPause()
        {
            var panel = ScreenCard("일시정지", "솔빛시 조달 상황실", 490, 443);
            VolumeSlider(panel, "배경 음악", game.MusicVolume, 120, game.SetMusic);
            VolumeSlider(panel, "효과음", game.EffectsVolume, 194, game.SetEffects);
            TopLeft(Button(panel, "Resume", "계속하기", Teal, White, 20, game.TogglePause).GetComponent<RectTransform>(), 30, 269, 430, 57);
            TopLeft(Button(panel, "Title", "타이틀로", Pale, Teal, 17, game.ReturnToTitle).GetComponent<RectTransform>(), 30, 341, 208, 52);
            TopLeft(Button(panel, "Quit", "게임 종료", Pale, Teal, 17, game.Quit).GetComponent<RectTransform>(), 252, 341, 208, 52);
        }

        void BuildEnd()
        {
            var panel = ScreenCard("행복한 스마트시티", "행복 하트 5,000개를 모아 솔빛시의 목표를 달성했습니다.", 594, 405);
            Short(panel, $"도시 행복도 {Model.Data.Happiness:N0}   ·   하트 {Model.Data.Hearts:N0}", 25, Teal, true, 30, 125, 527, 54);
            Short(panel, $"납품 {Model.Completed}건  ·  소요 시간 {Minutes(Model.Data.Clock)}", 20, Ink, false, 31, 193, 530, 45);
            Short(panel, "조달로 연결하고, 행복으로 성장하는 도시.", 17, Muted, false, 31, 243, 530, 39);
            TopLeft(Button(panel,"Back to title","타이틀로",Teal,White,20,game.ReturnToTitle).GetComponent<RectTransform>(),30,305,534,58);
        }

        void AskReset()
        {
            var panel = OpenModal("새 도시 시작", "현재 저장된 진행 상황을 새 도시로 교체합니다.", 540, 221);
            TopLeft(Button(panel,"Cancel","취소",Pale,Teal,17,CloseModal).GetComponent<RectTransform>(),29,135,224,48);
            TopLeft(Button(panel,"Confirm reset","새 게임 시작",Teal,White,17,() => game.Begin(false)).GetComponent<RectTransform>(),270,135,241,48);
        }

        internal void ShowSpeed()
        {
            if (game.Mode != NaraGame.ScreenState.Playing) return;
            var panel = OpenModal("게임 배속", "구매한 배속은 언제든 다시 선택할 수 있습니다.", 590, 360);
            for (int tier = 0; tier <= 2; tier++)
            {
                int value = tier;
                string speed = tier == 0 ? "1배속" : tier == 1 ? "1.5배속" : "2배속";
                string availability = Model.Data.SpeedTier == tier ? "적용 중" :
                    Model.HasSpeed(tier) ? "전환" : $"하트 {Procurement.SpeedPrice(tier):N0}개";
                var pick = Button(panel, $"Speed {tier}", $"{speed}   ·   {availability}", tier == Model.Data.SpeedTier ? Teal : Pale,
                    tier == Model.Data.SpeedTier ? White : Ink, 20, () => game.SelectSpeed(value));
                pick.interactable = tier != Model.Data.SpeedTier;
                TopLeft(pick.GetComponent<RectTransform>(), 29, 132 + tier * 67, 532, 54);
            }
        }

        void ShowEnding()
        {
            if (!Model.CanEnd || game.Driving) return;
            var panel = OpenModal("엔딩 보기", "행복 하트 목표를 달성했습니다. 솔빛시의 엔딩을 보시겠습니까?", 630, 221);
            TopLeft(Button(panel, "Keep playing", "계속 운영", Pale, Teal, 17, CloseModal).GetComponent<RectTransform>(), 29, 135, 272, 48);
            TopLeft(Button(panel, "Finish city", "엔딩 시작", Teal, White, 17, game.OpenEnding).GetComponent<RectTransform>(), 317, 135, 284, 48);
        }

        void ShowExpansion(bool warehouse)
        {
            Site site = warehouse ? Site.Depot : Site.Office;
            Upgrade remote = warehouse ? Upgrade.Reserve : Upgrade.Clerk;
            if (!game.RequireVisit(site, remote)) return;

            string name = warehouse ? "비축 창고" : "업무 슬롯";
            bool available = warehouse ? Model.CanExpandWarehouse : Model.CanExpandOrders;
            int current = warehouse ? Model.Capacity : Model.OrderLimit;
            int nextCapacity = warehouse ? Model.NextWarehouseCapacity : Model.OrderLimit + 1;
            string unit = warehouse ? "개" : "건";
            var panel = OpenModal(name + " 확장", warehouse ? "조달 예산으로 비축기지 용량을 늘립니다." :
                "행복 하트로 동시에 받을 수 있는 조달 업무를 늘립니다.", 590, 290);
            Short(panel, available ? $"{current}{unit}에서 {nextCapacity}{unit}로 확장" : $"최대 {current}{unit} 운영 중",
                25, Ink, true, 29, 126, 532, 43);
            if (!available) return;

            int price = warehouse ? Model.WarehouseExpansionPrice : Model.OrderExpansionPrice;
            string cost = warehouse ? $"{price:N0}만원" : $"행복 하트 {price:N0}개";
            var buy = Button(panel, "Buy expansion", $"{cost}  ·  확장", Teal, White, 19, () =>
            {
                if (!game.RequireVisit(site, remote)) return;
                if (warehouse ? Model.ExpandWarehouse() : Model.ExpandOrders()) ShowExpansion(warehouse);
                else Toast($"확장 비용 부족 · {cost}", false);
            });
            TopLeft(buy.GetComponent<RectTransform>(), 29, 205, 532, 55);
        }

        RectTransform ScreenCard(string title, string subtitle, float width, float height)
        {
            Stretch(Flat(overlay, Shade).rectTransform);
            return Card(overlay, title, subtitle, width, height);
        }

        RectTransform OpenModal(string title, string subtitle, float width, float height)
        {
            if (modal != null) HideAndDestroy(modal.gameObject);
            modalId = -1;
            modalOrder = null;
            modal = Make("Modal · " + title, canvas);
            Stretch(modal);
            Stretch(Flat(modal, Shade).rectTransform);
            var modalPanel = Card(modal, title, subtitle, width, height);
            TopRight(Button(modalPanel, "Close", "닫기", Pale, Teal, 15, CloseModal).GetComponent<RectTransform>(), 23, 25, 69, 39);
            return modalPanel;
        }

        void BindOrderModal(int id)
        {
            modalId = id;
            modalOrder = Model.Data.Orders[id];
            modalStep = modalOrder.Step;
        }

        RectTransform Card(RectTransform parent, string title, string subtitle, float width, float height)
        {
            var panel = Panel(parent, title, Paper);
            At(panel, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, width, height);
            Short(panel, title, 31, Ink, true, 29, 19, width - 130, 48);
            Short(panel, subtitle, 16, Muted, false, 30, 72, width - 57, 32);
            Sep(panel, 29, 111, width - 58, 1);
            return panel;
        }

        internal void CloseModal()
        {
            if (modal != null) HideAndDestroy(modal.gameObject);
            modal = null;
            modalOrder = null;
            modalId = -1;
            inspectionStatus = null;
            game.CloseInspection();
        }

        internal void OpenTab(Tab tab)
        {
            active = tab;
            contextSite = null;
            chooseManually = false;
            lastTab = -1;
            if (Model != null) Refresh(true);
        }

        internal void Interact(Site site)
        {
            contextSite = site;
            active = site == Site.Depot ? Tab.Stock : Tab.Contracts;
            chooseManually = false;
            lastTab = -1;
            selected = FindActive();
            Refresh(true);
        }

        internal void LeaveSite()
        {
            if (contextSite.HasValue) OpenTab(Tab.Contracts);
            else Refresh(true);
        }

        internal void Refresh(bool force)
        {
            if (!main.gameObject.activeSelf) return;
            var d = Model.Data;
            budget.text = $"{d.Budget:N0} 만원";
            hearts.text = $"{d.Hearts:N0}";
            happiness.text = $"{d.Happiness:N0}";
            progress.rectTransform.sizeDelta = new Vector2(Mathf.Max(2, 98 * Mathf.Clamp01((float)d.Hearts / Procurement.Goal)), 3);
            speedText.text = $"{Model.TimeMultiplier:0.#}배속";
            bool night = CityWorld.Night(d.Clock);
            int hour = (7 + Mathf.FloorToInt(d.Clock % 174 / 174 * 24)) % 24;
            int minutes = Mathf.FloorToInt(d.Clock % 174 / 174 * 24 * 60) % 60;
            day.text = $"솔빛시   {hour:00}:{minutes:00}  {(night ? "밤" : "낮")}";
            clockDot.color = night ? CityWorld.ColorOf("879ABF") : Gold;
            noticeBag.gameObject.SetActive(Model.NewRequests > 0);
            stage.text = $"{Model.Stage + 1:D2} / 03   {Procurement.Stages[Model.Stage]}";
            endingButton.gameObject.SetActive(Model.CanEnd && !game.Driving);
            next.text = Model.CanEnd ? $"납품 {Model.Completed:N0}건   ·   {Minutes(d.Clock)}" :
                $"엔딩 하트 {d.Hearts:N0} / {Procurement.Goal:N0}    ·    납품 {Model.Completed:N0}건";
            if (toast.gameObject.activeSelf && Time.unscaledTime >= toastUntil) toast.gameObject.SetActive(false);
            bool showDrive = game.Driving;
            if (showDrive != viewHidden)
            {
                viewHidden = showDrive;
                desk.gameObject.SetActive(!showDrive);
            }
            driveHUD.gameObject.SetActive(showDrive);
            walkHint.gameObject.SetActive(!showDrive && game.AtSite);
            if (showDrive)
            {
                driveText.text = game.Parked ? $"도착 · {CityWorld.Names[(int)game.Destination]}" : $"운송 목적지 · {CityWorld.Names[(int)game.Destination]}";
                driveValue.text = $"{game.Speed:0} km/h       남은 거리 {game.Distance:0} m";
                arrivalButton.interactable = game.Parked;
            }
            else if (game.AtSite) walkText.text = $"E   {CityWorld.Names[(int)game.Nearby]} 업무 확인";
            if (lastWave != d.Wave || lastTab != (int)active || force)
            {
                if (!chooseManually || selected < 0 || !MatchesContext(selected)) selected = FindActive();
                if (modalId >= 0 && (d.Orders[modalId] != modalOrder || modalOrder.Step != modalStep)) CloseModal();
                lastWave = d.Wave;
                lastTab = (int)active;
                RebuildDesk();
            }
            foreach (var (id, text) in orderTimers)
                text.text = Status(id);
            foreach (var (up, text) in staffTimers)
                text.text = WorkerStatus(up);
            if (detailTimer != null && active == Tab.Contracts && selected >= 0)
                detailTimer.text = Status(selected);
        }

        int FindActive()
        {
            for (int i = 0; i < Model.Data.Orders.Length; i++)
                if (MatchesContext(i) && Model.Data.Orders[i].Step is Step.Evaluate or Step.Inspect or Step.Delivery or Step.Materials) return i;
            for (int i = 0; i < Model.Data.Orders.Length; i++)
                if (MatchesContext(i)) return i;
            return -1;
        }

        bool MatchesContext(int id)
        {
            Step step = Model.Data.Orders[id].Step;
            if (step is Step.Locked or Step.Complete) return false;
            if (!contextSite.HasValue || contextSite == Site.Office) return true;
            if (Procurement.IsSupplier(contextSite.Value))
                return step is >= Step.SupplyTrip and <= Step.DeliveryTrip && Model.Supplier(id) == contextSite;
            return Procurement.Catalog[id].Site == contextSite;
        }

        internal void UpdateMarkers()
        {
            if (!main.gameObject.activeSelf) return;
            var worldCamera = game.City.Camera;
            for (int i = 0; i < markers.Length; i++)
            {
                Vector3 point = worldCamera.WorldToScreenPoint(game.City.Label((Site)i));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, point, null, out Vector2 p);
                Rect c = canvas.rect;
                float fromRight = c.xMax - p.x;
                bool visible = point.z > 0 && p.x > c.xMin + 49 && fromRight > (game.Driving ? 45 : 441) &&
                    p.y > c.yMin + 133 && p.y < c.yMax - 113 && (!game.Driving || i == (int)game.Destination);
                markers[i].gameObject.SetActive(visible);
                if (!visible) continue;
                markers[i].anchoredPosition = p;
                bool powered = false;
                for (int j = 0; j < Procurement.Catalog.Length; j++)
                    if ((int)Procurement.Catalog[j].Site == i) powered = Model.IsActive(j);
                bool called = i == 0 && Model.NewRequests > 0;
                markerBg[i].color = powered ? Pale : called ? Gold : Paper;
                markerText[i].color = Teal;
            }
        }

        void Select(int id)
        {
            selected = id;
            chooseManually = true;
            if (!contextSite.HasValue || contextSite.Value != game.Nearby) game.CenterOrder(id);
            RebuildDesk();
        }

        void RebuildDesk()
        {
            ResetChildren(list);
            ResetChildren(detail);
            orderTimers.Clear();
            staffTimers.Clear();
            detailTimer = null;
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].GetComponent<UI.Image>().color = i == (int)active ? Teal : Pale;
                tabText[i].color = i == (int)active ? White : Teal;
            }
            panelTitle.text = contextSite.HasValue && contextSite != Site.Office
                ? CityWorld.Names[(int)contextSite.Value] : Tabs[(int)active];
            bool orderDesk = active == Tab.Contracts && (!contextSite.HasValue || contextSite == Site.Office);
            expansionButton.gameObject.SetActive(orderDesk || active == Tab.Stock);
            expansionText.text = active == Tab.Stock ? "창고 확장" : "업무 슬롯";
            switch (active)
            {
                case Tab.Contracts: BuildOrders(); break;
                case Tab.Stock: BuildStock(); break;
                case Tab.Staff: BuildStaff(); break;
            }
        }

        void BuildOrders()
        {
            panelCount.text = contextSite.HasValue && contextSite != Site.Office
                ? "이 시설에 연결된 조달 업무"
                : $"업무 {Model.ActiveOrders} / {Model.OrderLimit}건    ·    새 요청 {Model.NewRequests}건";
            for (int i = 0; i < Procurement.Catalog.Length; i++)
            {
                if (!MatchesContext(i)) continue;
                int id = i;
                Contract c = Procurement.Catalog[id];
                var order = Model.Data.Orders[id];
                var item = Button(list, "Order " + i, "", id == selected ? Pale : White, Ink, 17, () => Select(id));
                var row = item.GetComponent<RectTransform>();
                Height(row, 89);
                var accent = Flat(row, order.Step == Step.Request ? Rose : Gold);
                TopLeft(accent.rectTransform, 11, 13, 4, 61);
                string owner = order.Bid < 0 ? c.Buyer : CityWorld.Names[(int)Model.Supplier(id)];
                Short(row, owner, 13, Muted, false, 24, 8, 212, 23);
                Short(row, c.Item, 19, Ink, true, 24, 30, 322, 29);
                Short(row, $"하트 +{order.Reward}", 13, Rose, true, 265, 62, 82, 21);
                orderTimers.Add((id, Short(row, Status(id), 13, Teal, true, 24, 62, 234, 21)));
            }
            var bg = Panel(detail, "Task action", Pale);
            Stretch(bg);
            if (selected < 0)
            {
                Short(bg, "대기 중인 업무 없음", 18, Ink, true, 12, 12, 338, 38);
                Short(bg, "새 조달요청이 도착하면 나라장터에 표시됩니다.", 15, Muted, false, 12, 51, 335, 52);
                return;
            }
            Contract chosen = Procurement.Catalog[selected];
            Short(bg, chosen.Item, 17, Ink, true, 11, 5, 340, 29);
            detailTimer = Short(bg, Status(selected), 14, Muted, false, 11, 36, 340, 24);
            var stepNow = Model.Data.Orders[selected].Step;
            string label = stepNow switch
            {
                Step.Request => "요청 확인 및 공고 게시", Step.Evaluate => "제안서 비교 및 선정",
                Step.Materials => "비축 및 원자재 운송", Step.Inspect => "품질검사 시작",
                Step.Delivery => game.CanUse(game.WorkSite(selected)) ? "트럭에 싣고 직접 납품" : "자동화 차량 보내기",
                _ => "진행 위치 보기"
            };
            if (NeedsAction(stepNow) && !game.CanUse(game.WorkSite(selected), game.RemoteFor(selected)))
                label = $"{CityWorld.Names[(int)game.WorkSite(selected)]} 방문";
            int idSelected = selected;
            BottomStretch(Button(bg, "Task action", label, Teal, White, 17, () => TakeAction(idSelected)).GetComponent<RectTransform>(), 11, 11, 11, 55);
        }

        void TakeAction(int id)
        {
            if (NeedsAction(Model.Data.Orders[id].Step) && !game.RequireVisit(game.WorkSite(id), game.RemoteFor(id))) return;
            switch (Model.Data.Orders[id].Step)
            {
                case Step.Request: ShowRequest(id); break;
                case Step.Evaluate: ShowBids(id); break;
                case Step.Materials: ShowSupply(id); break;
                case Step.Inspect: game.OpenInspection(id); break;
                case Step.Delivery: game.StartTrip(id, false); break;
                default: game.CenterOrder(id); break;
            }
        }

        static bool NeedsAction(Step step) => step is Step.Request or Step.Evaluate or Step.Materials or Step.Inspect or Step.Delivery;

        void ShowRequest(int id)
        {
            Contract c = Procurement.Catalog[id];
            var panel = OpenModal("조달요청 확인", $"{c.Buyer}   /   {c.Item}", 700, 358);
            BindOrderModal(id);
            Short(panel, $"수량 {c.Quantity}개    ·    예산 상한 {c.Limit}만원", 22, Ink, true, 31, 128, 632, 48);
            Short(panel, $"품질 {c.Quality}점 이상    ·    납기 {c.Deadline}일 이내    ·    참가자격 필수", 18, Teal, false, 32, 181, 630, 38);
            Short(panel, c.Benefit, 17, Muted, false, 32, 225, 634, 33);
            TopLeft(Button(panel,"Publish notice",$"입찰공고 게시  ·  접수 {Procurement.BidSeconds}초",Teal,White,19,() =>
            {
                if (game.Publish(id)) CloseModal();
            }).GetComponent<RectTransform>(),29,276,642,55);
        }

        void ShowBids(int id)
        {
            Contract c = Procurement.Catalog[id];
            var panel = OpenModal("입찰 제안서", $"{c.Item}   ·   {c.Limit}만원 이하  /  {c.Quality}점 이상  /  {c.Deadline}일 이내  /  참가자격 필수", 946, 514);
            BindOrderModal(id);
            string[] columns = { "기업", "가격", "품질", "납기", "참가자격" };
            int[] x = { 52, 242, 349, 442, 529 };
            for (int i = 0; i < columns.Length; i++) Short(panel, columns[i], 16, Muted, true, x[i], 126, 113, 35);
            Bid[] bids = Procurement.Offers(id);
            for (int i = 0; i < bids.Length; i++)
            {
                Bid bid = bids[i];
                string failure = Model.Ineligible(id, bid);
                bool valid = failure.Length == 0;
                var row = Panel(panel, bid.Company, White);
                TopLeft(row, 29, 167 + i * 75, 888, 65);
                Short(row, bid.Company, 21, Ink, true, x[0]-29, 7, 198, 48);
                Short(row, $"{bid.Price}만원", 18, bid.Price > c.Limit ? Rose : Ink, false, x[1]-29, 7, 108, 48);
                Short(row, $"{bid.Quality}점", 18, bid.Quality < c.Quality ? Rose : Ink, false, x[2]-29, 7, 100, 48);
                Short(row, $"{bid.Days}일", 18, bid.Days > c.Deadline ? Rose : Ink, false, x[3]-29, 7, 90, 48);
                Short(row, bid.Licensed ? "충족" : "미충족", 18, bid.Licensed ? Teal : Rose, true, x[4]-29, 7, 102, 48);
                TopRight(Button(row, "Select " + bid.Company, valid ? "계약 체결" : "기준 확인", valid ? Teal : Line,
                    valid ? White : Rose, 16, () => game.Review(id,bid)).GetComponent<RectTransform>(), 13, 11, 137, 43);
            }
        }

        void ShowSupply(int id)
        {
            Contract c = Procurement.Catalog[id];
            var panel = OpenModal("기업 공급 요청", $"{CityWorld.Names[(int)Model.Supplier(id)]}   /   {c.Item}", 672, 357);
            BindOrderModal(id);
            string name = Procurement.MaterialName(c.Material);
            Short(panel, $"{name} {c.MaterialUnits}개 필요", 26, Ink, true, 29, 127, 610, 43);
            int available = Model.Stock(c.Material);
            int missing = Mathf.Max(0, c.MaterialUnits - available);
            Short(panel, $"보유 {available}개    ·    비축 용량 {Model.Stored} / {Model.Capacity}개", 18, Muted, false, 30, 181, 607, 38);
            if (missing > 0)
                TopLeft(Button(panel, "Buy shortage", $"부족분 {missing}개 구매  ·  {missing * Procurement.UnitPrice(c.Material)}만원",
                    Pale, Teal, 17, () =>
                    {
                        if (!game.RequireVisit(Site.Depot, Upgrade.Reserve)) return;
                        if (Model.Buy(c.Material, missing)) ShowSupply(id);
                        else Toast("비축 용량 또는 조달 예산을 확인하세요.", false);
                    }).GetComponent<RectTransform>(), 29, 231, 614, 44);
            else
                Short(panel, Model.Has(Upgrade.Reserve) ? "비축 차량이 운송을 준비하고 있습니다." : "상차를 마치면 협력기업까지 직접 운전하세요.",
                    17, Muted, false, 31, 230, 607, 42);
            var dispatch = Button(panel,"Load and drive",game.CanUse(Site.Depot) ? "상차 후 직접 운송" : "비축 차량 보내기",Teal,White,18,() => game.StartTrip(id,true));
            dispatch.interactable = missing == 0;
            TopLeft(dispatch.GetComponent<RectTransform>(),29,283,614,52);
        }

        internal void ShowInspection(int id)
        {
            Contract c = Procurement.Catalog[id];
            Order order = Model.Data.Orders[id];
            bool defect = Model.HasDefect(id);
            bool scratch = (id + order.Sample) % 2 == 0;
            var panel = OpenModal("품질검사", $"{c.Item}  ·  표본 {order.Sample + 1} / {Model.SampleCount(id)}", 854, 500);
            BindOrderModal(id);
            var specimen = Panel(panel, "Product specimen", CityWorld.ColorOf("E4E6CE"));
            TopLeft(specimen, 29, 133, 352, 238);
            DrawSpecimen(specimen, id, defect && scratch);
            Short(panel,"검사 기준",20,Teal,true,406,132,412,42);
            Short(panel,"표면 손상 없음 · 측정값 95~105 mm",17,Muted,false,406,177,410,35);
            string surface = defect && scratch ? "긁힘 확인" : "이상 없음";
            int measure = defect && !scratch ? 109 + order.Sample : 98 + (order.Sample * 3 + id) % 7;
            Short(panel,$"표면 관찰     {surface}",21,Ink,true,406,226,412,37);
            Short(panel,$"규격 측정     {measure} mm",21,Ink,true,406,269,412,37);
            inspectionStatus = Short(panel,"두 기준을 모두 충족하면 합격을 선택하세요.",15,Muted,false,406,319,410,48);
            TopLeft(Button(panel,"Accept sample","합격",Teal,White,20,() => game.Judge(id,false)).GetComponent<RectTransform>(),29,399,389,64);
            TopLeft(Button(panel,"Request correction","불량 · 보완 요청",Gold,Ink,20,() => game.Judge(id,true)).GetComponent<RectTransform>(),436,399,389,64);
        }

        static string SampleColor(int id) => id == 0 ? "C49060" : id == 1 ? "9ABAB3" : id == 2 ? "D9A45F" : "78A6A2";

        void DrawSpecimen(RectTransform specimen, int id, bool scratched)
        {
            Color paint = CityWorld.ColorOf(SampleColor(id));
            var deskTop = Panel(specimen,"Specimen main surface",paint);
            TopLeft(deskTop,55,72,242,70);
            if (id == 0)
            {
                TopLeft(Flat(specimen,Ink).rectTransform, 77,137,13,56);
                TopLeft(Flat(specimen,Ink).rectTransform, 260,137,13,56);
                TopLeft(Flat(specimen,CityWorld.ColorOf("E8BB82")).rectTransform,69,86,214,4);
            }
            else
            {
                TopLeft(Flat(specimen,Ink).rectTransform,65,134,221,12);
                TopLeft(Panel(specimen,"Gauge",Pale),198,94,69,30);
                for (int i = 0; i < 4; i++) TopLeft(Flat(specimen,Muted).rectTransform,84+i*14,93,5,27);
            }
            if (scratched)
                for (int i = 0; i < 3; i++)
                {
                    var scratch = Flat(specimen, Rose).rectTransform;
                    TopLeft(scratch,133+i*11,104+i*2,36-i*5,3);
                    scratch.localRotation = Quaternion.Euler(0,0,32);
                }
        }

        internal void FlashInspection(string message, bool error)
        {
            if (inspectionStatus == null) return;
            inspectionStatus.text = message;
            inspectionStatus.color = error ? Rose : Teal;
        }

        void BuildStock()
        {
            panelCount.text = $"보관 중 {Model.Stored} / {Model.Capacity}개";
            bool haveAccess = game.CanUse(Site.Depot, Upgrade.Reserve);
            foreach (var material in new[] { Resource.Aluminum, Resource.Copper })
            {
                var card = Panel(list, Procurement.MaterialName(material), White);
                Height(card, haveAccess ? 153 : 92);
                Short(card, Procurement.MaterialName(material), 20, Ink, true, 14, 8, 218, 38);
                Short(card, $"{Model.Stock(material)}개 보유 · 단가 {Procurement.UnitPrice(material)}만원",
                    15, Muted, false, 15, 51, 336, 28);
                if (!haveAccess) continue;
                int add = Mathf.Min(4, Model.Capacity - Model.Stored);
                TopLeft(Button(card, "Buy one", "+1 구매", Pale, Teal, 16, () => Purchase(material, 1)).GetComponent<RectTransform>(), 13, 92, 99, 43);
                TopLeft(Button(card, "Buy several", $"+{Mathf.Max(1,add)} 구매", Pale, Teal, 16,
                    () => Purchase(material, Mathf.Max(1,add))).GetComponent<RectTransform>(), 120, 92, 105, 43);
                TopLeft(Button(card, "Sell one", "−1 매각", Line, Ink, 16, () =>
                {
                    if (!game.RequireVisit(Site.Depot, Upgrade.Reserve)) return;
                    if (!Model.Sell(material)) Toast("매각할 원자재가 없습니다.",false);
                }).GetComponent<RectTransform>(), 234, 92, 104, 43);
            }
            var note = Panel(detail, "Stock instructions", Pale);
            Stretch(note);
            Short(note, "기업 원자재 공급", 17, Ink, true, 13, 8, 341, 32);
            Short(note, haveAccess ? "나라장터에서 필요한 품목과 수량을 확인하세요." : "구매와 매각은 비축기지 업무 구역에서 가능합니다.",
                14, Muted, false, 13, 41, 337, 35);
            BottomStretch(Button(note, "Back to orders", haveAccess ? "조달요청 보기" : "비축기지 위치 보기", Teal, White, 17,
                () => { if (haveAccess) OpenTab(Tab.Contracts); else game.Focus(Site.Depot); }).GetComponent<RectTransform>(),11,11,11,49);
        }

        void Purchase(Resource material, int quantity)
        {
            if (!game.RequireVisit(Site.Depot, Upgrade.Reserve)) return;
            if (!Model.Buy(material, quantity)) Toast("비축 용량 또는 조달 예산을 확인하세요.",false);
        }

        void BuildStaff()
        {
            int hired = 0;
            for (int i = 0; i < Procurement.UpgradeNames.Length; i++)
            {
                var up = (Upgrade)i;
                int level = Model.Level(up);
                bool running = level > 0;
                bool max = level == Procurement.MaxLevel;
                bool unlocked = Model.Unlocked(up);
                if (running) hired++;
                var card = Panel(list, Procurement.UpgradeNames[i], running ? Pale : White);
                Height(card, 168);
                Short(card, Procurement.UpgradeNames[i], 19, Ink, true, 14, 8, 247, 36);
                if (running) Short(card, $"Lv.{level} / {Procurement.MaxLevel}", 15, Teal, true, 264, 8, 80, 36);
                Short(card, Procurement.UpgradeDetails[i], 14, Muted, false, 15, 47, 333, 50);
                if (running) staffTimers.Add((up, Short(card, WorkerStatus(up), 13, Teal, true, 14, 92, 336, 27)));
                int cost = Model.UpgradePrice(up);
                int budgetCost = Model.UpgradeBudgetPrice(up);
                string costLabel = budgetCost > 0 ? $"하트 {cost:N0} + {budgetCost:N0}만원" : $"하트 {cost:N0}";
                bool canHireHere = game.CanUse(Site.Office, Upgrade.Clerk);
                var hire = Button(card, "Activate " + up,
                    max ? "최고 등급" : !unlocked ? "해금 대기" : !canHireHere ? $"조달청 방문  ·  {costLabel}" :
                        $"{costLabel}  ·  {(running ? $"Lv.{level + 1} 강화" : "운영 시작")}",
                    unlocked && !max ? Teal : Line, unlocked && !max ? White : Muted, 15, () =>
                    {
                        if (!game.RequireVisit(Site.Office, Upgrade.Clerk)) return;
                        if (!Model.Hire(up)) Toast($"{costLabel} 비용을 확인하세요.",false);
                    });
                hire.interactable = unlocked && !max;
                TopLeft(hire.GetComponent<RectTransform>(),13,122,335,34);
            }
            panelCount.text = $"운영 중 {hired} / 5";
            var note = Panel(detail, "Automation instructions", Pale);
            Stretch(note);
            var title = Short(note, "행복 하트 사용", 17, Ink, true, 13, 8, 338, 37);
            title.raycastTarget = false;
            Short(note, "조달청에서 직원을 고용하고 최대 3단계까지 강화하세요. 등급이 오르면 업무가 빨라지고 대기시간이 줄어듭니다.",
                15, Muted, false, 13, 49, 339, 71);
        }

        string WorkerStatus(Upgrade up)
        {
            if (up == Upgrade.Fleet) return $"차량 속도 {Model.FleetMultiplier:0.##}배   ·   자동 구매";
            float remaining = Model.RemainingCooldown(up);
            return $"업무 속도 {Model.WorkerMultiplier(up):0.##}배   ·   " +
                (remaining > 0 ? $"대기 {Mathf.CeilToInt(remaining)}초" : $"업무 후 대기 {Model.Cooldown(up):0.#}초");
        }

        string Status(int id)
        {
            var order = Model.Data.Orders[id];
            string state = StepText[(int)order.Step];
            if (order.Step == Step.Bidding || order.Step == Step.Production)
            {
                if (order.Step == Step.Production && order.Sample > 0) state = "보완·재검 중";
                return $"{state}   {Mathf.CeilToInt(Mathf.Max(0, order.Timer))}초";
            }
            if (order.Step == Step.Materials)
            {
                var c = Procurement.Catalog[id];
                return $"{Procurement.MaterialName(c.Material)} {c.MaterialUnits}개 필요";
            }
            return state;
        }

        internal void Toast(string message, bool reward)
        {
            if (string.IsNullOrEmpty(message)) return;
            notice.text = message;
            notice.color = reward ? Ink : White;
            toast.GetComponent<UI.Image>().color = reward ? Gold : Teal;
            toast.SetAsLastSibling();
            toast.gameObject.SetActive(true);
            toastUntil = Time.unscaledTime + 4.5f;
        }

        void VolumeSlider(RectTransform parent, string name, float value, float y, Action<float> update)
        {
            Short(parent, name, 16, Ink, true, 31, y, 130, 44);
            var container = Make(name + " slider", parent);
            TopLeft(container, 163, y, 283, 43);
            var track = Panel(container, "Track", Pale);
            Stretch(track, 0, 0, 16, 16);
            var span = Make("Handle area",container);
            Stretch(span,10,10,0,0);
            var handle = Panel(span,"Knob",Teal);
            handle.sizeDelta = new Vector2(23,28);
            var slider = container.gameObject.AddComponent<UI.Slider>();
            slider.targetGraphic = handle.GetComponent<UI.Image>();
            slider.handleRect = handle;
            slider.value = value;
            slider.onValueChanged.AddListener(v => update(v));
        }

        RectTransform Scroll(RectTransform parent, out RectTransform content)
        {
            var scrollRoot = Make("Scroll",parent);
            var viewport = Make("Viewport",scrollRoot);
            Stretch(viewport);
            viewport.gameObject.AddComponent<UI.Image>().color = new Color(1,1,1,0);
            viewport.gameObject.AddComponent<UI.RectMask2D>();
            content = Make("List",viewport);
            content.anchorMin = new Vector2(0,1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f,1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<UI.VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(2, 2, 0, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<UI.ContentSizeFitter>();
            fit.verticalFit = UI.ContentSizeFitter.FitMode.PreferredSize;
            var scroll = scrollRoot.gameObject.AddComponent<UI.ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            return scrollRoot;
        }

        UI.Text Stat(RectTransform parent, string label, float x, float w, Color color)
        {
            var widget = Make(label,parent);
            TopLeft(widget,x,0,w,74);
            Short(widget,label,14,Muted,true,0,7,w,25);
            return Short(widget,"",24,color,true,0,32,w,29);
        }

        static string Minutes(float t) => $"{(int)t / 60:00}:{(int)t % 60:00}";
        static void Height(RectTransform target, float value) => target.gameObject.AddComponent<UI.LayoutElement>().preferredHeight = value;
        void Sep(RectTransform parent, float x, float y, float w, float h) => TopLeft(Flat(parent, Line).rectTransform,x,y,w,h);
        static void HideAndDestroy(GameObject obj) { obj.SetActive(false); UnityEngine.Object.Destroy(obj); }
        static void ResetChildren(RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                child.SetParent(null, false);
                HideAndDestroy(child.gameObject);
            }
        }

        UI.Text Short(RectTransform parent, string value, int font, Color color, bool heavy, float x, float y, float w, float h)
        {
            var label = Label(parent,value,font,color,heavy);
            TopLeft(label.rectTransform,x,y,w,h);
            return label;
        }

        UI.Text Label(RectTransform parent, string value, int size, Color color, bool heavy, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var rt = Make("Text",parent);
            var label = rt.gameObject.AddComponent<UI.Text>();
            label.font = heavy ? bold : normal;
            label.fontSize = size;
            label.color = color;
            label.text = value;
            label.alignment = anchor;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.lineSpacing = 1.08f;
            return label;
        }

        UI.Button Button(RectTransform parent, string name, string value, Color bg, Color fg, int size, Action action) =>
            Button(parent,name,value,bg,fg,size,action,out _);

        UI.Button Button(RectTransform parent, string name, string value, Color bg, Color fg, int size, Action action, out UI.Text text)
        {
            var node = Panel(parent,name,bg);
            var button = node.gameObject.AddComponent<UI.Button>();
            button.targetGraphic = node.GetComponent<UI.Image>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.91f,.96f,.91f);
            colors.pressedColor = new Color(.77f,.86f,.81f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.85f,.85f,.85f,.76f);
            colors.fadeDuration = .1f;
            button.colors = colors;
            var navigation = button.navigation;
            navigation.mode = UI.Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(() => { game.Click(); action(); });
            text = Label(node,value,size,fg,true,TextAnchor.MiddleCenter);
            Stretch(text.rectTransform,4,4,0,0);
            return button;
        }

        RectTransform Panel(RectTransform parent, string name, Color color)
        {
            var rt = Make(name,parent);
            var image = rt.gameObject.AddComponent<UI.Image>();
            image.sprite = round;
            image.type = UI.Image.Type.Sliced;
            image.color = color;
            return rt;
        }

        static UI.Image Flat(RectTransform parent, Color color)
        {
            var rt = Make("Fill",parent);
            var image = rt.gameObject.AddComponent<UI.Image>();
            image.color = color;
            image.raycastTarget = color == Shade;
            return image;
        }

        static RectTransform Make(string name, RectTransform parent)
        {
            var obj = new GameObject(name,typeof(RectTransform));
            var rt = (RectTransform)obj.transform;
            rt.SetParent(parent,false);
            return rt;
        }

        static Sprite RoundedSprite()
        {
            const int n = 40, radius = 13;
            var tex = new Texture2D(n,n,TextureFormat.RGBA32,false) {name="Solbit rounded UI",filterMode=FilterMode.Bilinear};
            var colors = new Color[n*n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x+.5f-n*.5f)-(n*.5f-radius));
                    float dy = Mathf.Max(0, Mathf.Abs(y+.5f-n*.5f)-(n*.5f-radius));
                    colors[y*n+x] = new Color(1,1,1,Mathf.Clamp01(radius+.5f - Mathf.Sqrt(dx*dx+dy*dy)));
                }
            tex.SetPixels(colors);
            tex.Apply(false,true);
            return Sprite.Create(tex,new Rect(0,0,n,n),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,
                new Vector4(radius,radius,radius,radius));
        }

        static void At(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(width,height);
        }

        static void Stretch(RectTransform rt, float left=0, float right=0, float top=0, float bottom=0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left,bottom); rt.offsetMax = new Vector2(-right,-top);
        }

        static void TopLeft(RectTransform rt, float x, float y, float w, float h) => At(rt,Vector2.up,Vector2.up,new Vector2(x,-y),w,h);
        static void TopRight(RectTransform rt, float x, float y, float w, float h) => At(rt,Vector2.one,Vector2.one,new Vector2(-x,-y),w,h);
        static void Right(RectTransform rt, float x, float y, float w, float h) => TopRight(rt,x,y,w,h);
        static void BottomLeft(RectTransform rt, float x, float y, float w, float h) => At(rt,Vector2.zero,Vector2.zero,new Vector2(x,y),w,h);
        static void BottomRight(RectTransform rt, float x, float y, float w, float h) => At(rt,Vector2.right,Vector2.right,new Vector2(-x,y),w,h);
        static void AnchorTopCenter(RectTransform rt, float x, float y, float w, float h) =>
            At(rt,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(x,-y),w,h);

        static void BottomStretch(RectTransform rt, float left, float right, float bottom, float height)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.right;
            rt.offsetMin = new Vector2(left,bottom); rt.offsetMax = new Vector2(-right,bottom+height);
        }

        static void StretchRight(RectTransform rt, float right, float top, float bottom, float width)
        {
            rt.anchorMin = Vector2.right; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-right-width,bottom); rt.offsetMax = new Vector2(-right,-top);
        }
    }
}
