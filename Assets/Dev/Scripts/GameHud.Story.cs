using UnityEngine;
using UI = UnityEngine.UI;

namespace NaraTycoon
{
    internal sealed partial class GameHud
    {
        static readonly string[] EndingTitles = { "첫 납품", "일상이 달라지다", "행복한 스마트시티" };
        static readonly string[] EndingCaptions =
        {
            "첫 책상과 의자가 도착한 날, 솔빛시의 변화가 시작됐습니다.",
            "거리와 공원에 사람들이 모이고, 시민의 하루가 조금 더 편해졌습니다.",
            "작은 조달이 모여 도시를 바꿨습니다. 솔빛시의 이야기는 계속됩니다."
        };
        UI.Text filmTitle, filmCaption;
        UI.Image filmFade;

        RectTransform tutorialCard;
        UI.Text tutorialTitle, tutorialBody;
        UI.Outline primaryOutline, secondaryOutline;
        float tutorialRefresh;

        void BuildEndingFilm()
        {
            var dark = new Color(.025f, .055f, .086f);
            var upper = Flat(overlay, dark).rectTransform;
            upper.anchorMin = new Vector2(0, 1);
            upper.anchorMax = Vector2.one;
            upper.pivot = new Vector2(.5f, 1);
            upper.anchoredPosition = Vector2.zero;
            upper.sizeDelta = new Vector2(0, 82);
            Short(upper, "솔빛시의 변화", 23, Gold, true, 42, 17, 395, 46);
            TopRight(Button(upper, "Skip ending", "건너뛰기", Ink, White, 16, game.CompleteEnding)
                .GetComponent<RectTransform>(), 35, 17, 139, 48);

            var lower = Flat(overlay, dark).rectTransform;
            BottomStretch(lower, 0, 0, 0, 134);
            filmTitle = Label(lower, "", 27, Gold, true, TextAnchor.MiddleCenter);
            filmCaption = Label(lower, "", 19, White, false, TextAnchor.MiddleCenter);
            Stretch(filmTitle.rectTransform, 30, 30, 11, 70);
            Stretch(filmCaption.rectTransform, 24, 24, 56, 14);

            filmFade = Flat(overlay, dark);
            filmFade.transform.SetSiblingIndex(0);
            Stretch(filmFade.rectTransform);
            filmFade.raycastTarget = false;
        }

        internal void UpdateEndingFilm(int shot, float time)
        {
            filmTitle.text = EndingTitles[shot];
            filmCaption.text = EndingCaptions[shot];
            float duration = shot == 2 ? 6f : 5f;
            float fadeIn = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(time / .42f));
            float fadeOut = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(duration - .34f, duration, time));
            var color = filmFade.color;
            color.a = Mathf.Max(fadeIn, fadeOut);
            filmFade.color = color;
        }

        internal void ShowEndingSummary()
        {
            ResetChildren(overlay);
            var panel = ScreenCard("행복한 스마트시티", "솔빛시의 다음 조달을 계속할 수 있습니다.", 615, 388);
            Short(panel, $"도시 행복도 {Model.Data.Happiness:N0}", 27, Teal, true, 30, 125, 545, 52);
            Short(panel, $"납품 {Model.Completed:N0}건  ·  소요 시간 {Minutes(Model.Data.Clock)}", 18, Ink, false, 31, 184, 543, 42);
            Short(panel, "조달로 연결하고, 행복으로 성장하는 도시.", 17, Muted, false, 31, 234, 543, 36);
            TopLeft(Button(panel, "Continue operating city", "계속 운영", Teal, White, 20, game.ContinueAfterEnding)
                .GetComponent<RectTransform>(), 30, 296, 352, 56);
            TopLeft(Button(panel, "Back to title", "타이틀로", Pale, Teal, 17, game.ReturnToTitle)
                .GetComponent<RectTransform>(), 394, 296, 191, 56);
        }

        void BuildTutorialHud()
        {
            tutorialCard = Panel(canvas, "Tutorial instructions", Paper);
            tutorialCard.GetComponent<UI.Image>().raycastTarget = false;
            var accent = Flat(tutorialCard, Gold);
            TopLeft(accent.rectTransform, 0, 0, 7, 113);
            accent.raycastTarget = false;
            tutorialTitle = Short(tutorialCard, "", 20, Teal, true, 19, 9, 390, 32);
            tutorialBody = Short(tutorialCard, "", 16, Ink, false, 19, 45, 386, 55);
            tutorialTitle.raycastTarget = false;
            tutorialBody.raycastTarget = false;
            tutorialCard.gameObject.SetActive(false);
        }

        void ClearTutorial()
        {
            tutorialCard.gameObject.SetActive(false);
            SetTutorialOutline(ref primaryOutline, null);
            SetTutorialOutline(ref secondaryOutline, null);
            tutorialRefresh = 0;
        }

        void UpdateTutorialHud()
        {
            if (game.Mode != NaraGame.ScreenState.Playing) return;
            if (Time.unscaledTime >= tutorialRefresh)
            {
                tutorialRefresh = Time.unscaledTime + .12f;
                Step supply = Model.Data.Orders[1].Step;
                if (!Model.IsActive(0) && Model.Data.Orders[0].Step != Step.Locked)
                    TeachFirstOrder(Model.Data.Orders[0].Step);
                else if (!Model.IsActive(1) && supply is Step.Materials or Step.SupplyTrip)
                    TeachSupply(supply);
                else
                {
                    ClearTutorial();
                    tutorialRefresh = Time.unscaledTime + .12f;
                }
            }

            float intensity = .74f + .24f * Mathf.Sin(Time.unscaledTime * 5.8f);
            if (primaryOutline != null) primaryOutline.effectColor = new Color(1, .76f, .25f, intensity);
            if (secondaryOutline != null) secondaryOutline.effectColor = new Color(1, .76f, .25f, intensity);
        }

        void TeachFirstOrder(Step step)
        {
            const string first = "첫 조달";
            if (step == Step.Bidding)
            {
                Tip(first + " · 입찰 접수", "접수 시간이 끝나면 기업 제안서를 비교할 수 있습니다.");
                return;
            }
            if (step == Step.Production)
            {
                Tip(first + " · 생산", "기업이 책상과 의자를 만드는 중입니다. 생산이 끝나면 협력기업에서 품질을 검사합니다.");
                return;
            }
            if (step == Step.DeliveryTrip)
            {
                Tip(first + " · 납품", game.DrivingOrder(0)
                    ? "도로의 청록색 화살표를 따라 학교로 가세요. 노란 선 안에 들어가면 하역합니다."
                    : "배송 차량이 학교에 도착하면 납품이 완료됩니다.");
                return;
            }

            Site site = game.WorkSite(0);
            if (!game.CanUse(site) || contextSite != site)
            {
                Tip(first, $"{CityWorld.Names[(int)site]} 건물 가까이 가면 업무 메뉴가 열립니다.", "Site " + site);
                return;
            }
            if (modalId == 0 && step == Step.Request)
            {
                Tip(first + " · 공고", "학교가 요청한 물품과 기준을 확인한 뒤 ‘입찰공고 게시’를 누르세요.", "Publish notice");
            }
            else if (modalId == 0 && step == Step.Evaluate)
            {
                string button = null;
                foreach (var bid in Model.Offers(0))
                    if (Model.Ineligible(0, bid).Length == 0)
                    {
                        button = "Select " + bid.Company;
                        break;
                    }
                Tip(first + " · 기업 선정", "가격, 품질, 납기, 참가자격을 모두 충족하는 기업의 ‘계약 체결’을 누르세요.", button);
            }
            else if (modalId == 0 && step == Step.Inspect)
            {
                ClearTutorial();
            }
            else if (active != Tab.Contracts)
            {
                Tip(first, "‘업무 현황’으로 돌아가 학교 책상·의자 요청을 선택하세요.", "Local building tab");
            }
            else
            {
                string action = step switch
                {
                    Step.Request => "학교 책상·의자 요청에서 ‘요청 확인 및 공고 게시’를 누르세요.",
                    Step.Evaluate => "학교 요청의 ‘제안서 비교 및 선정’을 눌러 참여 기업을 확인하세요.",
                    Step.Inspect => "‘품질검사 시작’을 눌러 책상 표본을 검사하세요.",
                    _ => "‘트럭에 싣고 직접 납품’을 눌러 학교로 출발하세요."
                };
                Tip(first, action, selected == 0 ? "Task action" : "Order 0");
            }
        }

        void TeachSupply(Step step)
        {
            const int job = 1;
            const string title = "원자재 공급";
            if (step == Step.SupplyTrip)
            {
                Tip(title + " · 전달", game.DrivingOrder(job)
                    ? "청록색 화살표를 따라 협력기업에 도착하면 원자재를 하역합니다."
                    : "운송 차량이 기업에 원자재를 전달하면 생산이 시작됩니다.");
                return;
            }
            if (!game.CanUse(Site.Depot) || contextSite != Site.Depot)
            {
                Tip(title + " · 비축기지", "기업에 필요한 원자재를 공급해야 합니다. 비축기지에 가까이 가세요.", "Site Depot");
                return;
            }

            Resource material = Model.RequiredMaterial(job);
            int missing = Mathf.Max(0, Model.RequiredUnits(job) - Model.Stock(material));
            string name = Procurement.MaterialName(material);
            if (modalId == job)
            {
                if (missing > 0)
                    Tip(title + " · 입고", $"‘부족분 구매’를 눌러 {name} {missing}개를 창고에 채우세요.", "Buy shortage");
                else
                    Tip(title + " · 출발", "‘상차 후 직접 운송’을 눌러 차량에 원자재를 실으세요.", "Load and drive");
            }
            else if (active == Tab.Staff)
                Tip(title, "‘창고 관리’에서 원자재 재고를 확인하세요.", "Local building tab");
            else if (active == Tab.Stock && missing > 0 && Model.Stored < Model.Capacity)
                Tip(title + " · 입고", $"창고에서 {name} {missing}개를 더 구매하세요. 구매한 원자재는 비축기지에 보관됩니다.",
                    "Buy several " + material);
            else if (active == Tab.Stock)
                Tip(title, "‘공급 업무 보기’를 눌러 기업의 요청과 필요한 양을 확인하세요.", "Local supply orders");
            else
                Tip(title + " · 공급 요청", "가로등 요청에서 ‘비축 및 원자재 운송’을 누르세요.", selected == job ? "Task action" : "Order 1");
        }

        void Tip(string title, string text, string target = null, string other = null)
        {
            tutorialCard.gameObject.SetActive(true);
            tutorialCard.SetAsLastSibling();
            if (HasModal) BottomLeft(tutorialCard, 24, 23, 425, 113);
            else TopLeft(tutorialCard, 24, 194, 425, 113);
            tutorialTitle.text = title;
            tutorialBody.text = text;
            var scope = HasModal ? modal : target != null && target.StartsWith("Site ", System.StringComparison.Ordinal) ? map : desk;
            SetTutorialOutline(ref primaryOutline, FindGuideButton(scope, target));
            SetTutorialOutline(ref secondaryOutline, FindGuideButton(scope, other));
        }

        static UI.Button FindGuideButton(RectTransform scope, string name)
        {
            if (scope == null || name == null) return null;
            foreach (var button in scope.GetComponentsInChildren<UI.Button>(false))
                if (button.name == name && button.IsInteractable()) return button;
            return null;
        }

        static void SetTutorialOutline(ref UI.Outline current, UI.Button button)
        {
            if (current != null && button != null && current.gameObject == button.gameObject) return;
            if (current != null) current.enabled = false;
            current = null;
            if (button == null) return;
            current = button.GetComponent<UI.Outline>() ?? button.gameObject.AddComponent<UI.Outline>();
            current.effectDistance = new Vector2(5, -5);
            current.useGraphicAlpha = false;
            current.enabled = true;
        }
    }
}
