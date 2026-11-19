using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UI = UnityEngine.UI;

namespace NaraTycoon
{
    internal sealed partial class GameHud
    {
        sealed class NoticeEntry
        {
            internal UI.Text Text;
            internal float Created, Y = 200, Scale = 1, Opacity;
            internal Color Tint;
        }

        readonly List<NoticeEntry> notices = new();
        RectTransform townEventPanel;
        UI.Text townEventText;
        UI.Image townEventBackground;

        internal void Toast(string message, bool reward)
        {
            if (string.IsNullOrEmpty(message)) return;
            var text = Label(toast, message, 18, reward ? Gold : Color.white, true, TextAnchor.MiddleCenter);
            var outline = text.gameObject.AddComponent<UI.Outline>();
            outline.effectColor = new Color(.05f, .11f, .14f, .9f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            AnchorTopCenter(text.rectTransform, 0, 200, 680, 28);
            var tint = text.color;
            text.color = new Color(tint.r, tint.g, tint.b, 0);
            notices.Add(new NoticeEntry { Text = text, Created = Time.unscaledTime, Tint = tint });
            toast.SetAsLastSibling();
        }

        void ClearNotices()
        {
            foreach (var item in notices) HideAndDestroy(item.Text.gameObject);
            notices.Clear();
        }

        void BuildTownEvent(RectTransform parent)
        {
            townEventPanel = Panel(parent, "Town event", Teal);
            townEventBackground = townEventPanel.GetComponent<UI.Image>();
            TopLeft(townEventPanel, 24, 191, 319, 50);
            townEventText = Short(townEventPanel, "", 15, White, true, 12, 4, 295, 42);
            townEventPanel.gameObject.SetActive(false);
        }

        internal void UpdateOperations(float dt, Keyboard keyboard)
        {
            float blend = 1 - Mathf.Exp(-14 * dt);
            for (int i = notices.Count - 1; i >= 0; i--)
            {
                var item = notices[i];
                float age = Time.unscaledTime - item.Created;
                if (age >= 6.5f)
                {
                    HideAndDestroy(item.Text.gameObject);
                    notices.RemoveAt(i);
                    continue;
                }
                int rank = notices.Count - 1 - i;
                item.Y = Mathf.Lerp(item.Y, Mathf.Max(74, 180 - rank * 24), blend);
                item.Scale = Mathf.Lerp(item.Scale, Mathf.Max(.66f, 1 - rank * .085f), blend);
                item.Opacity = Mathf.Lerp(item.Opacity, rank < 5 ? 1 - rank * .14f : 0, blend);
                item.Text.rectTransform.anchoredPosition = new Vector2(0, -item.Y);
                item.Text.rectTransform.localScale = Vector3.one * item.Scale;
                var color = item.Tint;
                color.a = item.Opacity * Mathf.Clamp01((6.5f - age) / 1.15f);
                item.Text.color = color;
            }

            var townEvent = Model.ActiveEvent;
            viewButton.interactable = !game.Driving;
            viewButtonText.text = game.ViewTargetsOverview ? "도시 전경  V" : "나르미  V";
            townEventPanel.gameObject.SetActive(townEvent != TownEvent.None);
            if (townEvent != TownEvent.None)
            {
                townEventText.text = $"{Procurement.EventName(townEvent)}   {Mathf.CeilToInt(Model.EventTime)}초\n{Procurement.EventEffect(townEvent)}";
                townEventBackground.color = townEvent switch
                {
                    TownEvent.Fireworks => CityWorld.ColorOf("61447C"),
                    TownEvent.NightMarket => CityWorld.ColorOf("994F48"),
                    _ => CityWorld.ColorOf("84611F")
                };
            }

            if (inspectionChallenge != null && game.Mode == NaraGame.ScreenState.Playing)
                TickInspectionChallenge(dt, keyboard);
        }

        sealed class InspectionChallenge
        {
            internal int Id, Kind, Combo, Count, LastHitPass;
            internal float Duration, Elapsed, Stable, Value, FinishDelay;
            internal bool Running, Finishing;
            internal int[] Sequence;
            internal UI.Text Clock, Status, StartText;
            internal UI.Button Start;
            internal UI.Button[] Pads;
            internal RectTransform Needle;
        }

        InspectionChallenge inspectionChallenge;

        internal void ShowInspectionChallenge(int id)
        {
            int kind = id switch { 0 or 3 => 0, 2 or 5 or 7 => 2, _ => 1 };
            string title = kind switch { 0 => "흔들림 테스트", 1 => "전력 조절 챌린지", _ => "긴급 안정화!" };
            string instruction = kind switch
            {
                0 => "바늘이 초록 구간에 들어올 때 Space를 눌러 5번 맞히세요.",
                1 => "A/D로 출력을 조절해 초록 구간에 합계 5초간 유지하세요.",
                _ => "빛나는 버튼의 순서를 기억해 1~4 키 또는 버튼으로 입력하세요."
            };
            var panel = OpenModal(title, "품질검사", 720, 584);
            BindOrderModal(id);
            var challenge = new InspectionChallenge { Id = id, Kind = kind, Duration = 8 + 2 * kind };
            inspectionChallenge = challenge;
            var specimen = Panel(panel, "Product specimen", CityWorld.ColorOf("E5EEE4"));
            TopLeft(specimen, 30, 126, 660, 204);
            DrawInspectionProduct(specimen, id);
            var productName = Label(specimen, Procurement.Catalog[id].Item, 20, Ink, true, TextAnchor.MiddleCenter);
            TopLeft(productName.rectTransform, 0, 164, 660, 33);
            Short(panel, instruction, 18, Ink, true, 30, 338, 660, 36);
            challenge.Clock = Short(panel, $"{challenge.Duration:0}초", 17, Teal, true, 30, 376, 660, 28);
            challenge.Status = Short(panel, "", 19, Teal, true, 30, 461, 660, 39);
            challenge.Start = Button(panel, "Start measurement", "측정 시작", Teal, White, 19,
                () => StartInspectionChallenge(challenge), out challenge.StartText);
            TopLeft(challenge.Start.GetComponent<RectTransform>(), 30, 516, 660, 46);
            if (kind == 2)
            {
                challenge.Pads = new UI.Button[4];
                for (int i = 0; i < challenge.Pads.Length; i++)
                {
                    int pad = i;
                    var button = Button(panel, "Sequence pad " + i, (i + 1).ToString(), Pale, Ink, 21,
                        () => PressInspectionPad(challenge, pad));
                    challenge.Pads[i] = button;
                    TopLeft(button.GetComponent<RectTransform>(), 30 + 166 * i, 412, 158, 44);
                }
            }
            else
            {
                var track = Panel(panel, "Measurement track", Line);
                TopLeft(track, 30, 418, 660, 32);
                TopLeft(Flat(track, CityWorld.ColorOf("78AE8D")).rectTransform, 660 * Low(kind), 0,
                    660 * (High(kind) - Low(kind)), 32);
                challenge.Needle = Flat(track, Ink).rectTransform;
                TopLeft(challenge.Needle, 0, -3, 7, 38);
            }
        }

        void DrawInspectionProduct(RectTransform stage, int id)
        {
            var art = Make("Product illustration", stage);
            TopLeft(art, 185, 12, 290, 150);
            var steel = CityWorld.ColorOf("5E7C84");
            var wood = CityWorld.ColorOf("C98E60");
            var light = CityWorld.ColorOf("FFE1A1");
            void Part(float x, float y, float w, float h, Color color, bool rounded = false)
            {
                var piece = rounded ? Panel(art, "Product part", color) : Flat(art, color).rectTransform;
                TopLeft(piece, x, y, w, h);
            }
            Part(20, 134, 248, 8, Pale, true);
            switch (id)
            {
                case 0:
                    Part(54,68,122,16,wood,true); Part(61,82,8,50,steel); Part(156,82,8,50,steel);
                    Part(183,85,62,13,wood,true); Part(222,53,17,31,wood,true);
                    Part(190,98,7,34,steel); Part(232,98,7,34,steel);
                    break;
                case 1:
                    Part(78,127,67,8,steel,true); Part(106,30,9,100,steel); Part(108,23,77,9,steel,true);
                    Part(169,26,49,17,Ink,true); Part(178,41,34,5,light,true);
                    break;
                case 2:
                    Part(55,40,175,85,steel,true); Part(61,47,157,69,light,true);
                    Part(67,52,57,55,Ink,true); Part(79,62,32,19,Pale,true);
                    for (int i = 0; i < 4; i++) Part(143,59 + i * 11,63,4,steel,true);
                    Part(69,124,28,10,Ink,true); Part(190,124,28,10,Ink,true);
                    break;
                case 3:
                    Part(55,60,173,16,wood,true); Part(55,79,173,13,wood,true);
                    Part(42,99,201,12,Teal,true); Part(59,109,11,24,steel); Part(215,109,11,24,steel);
                    break;
                case 4:
                case 8:
                    Part(46,30,197,18,id == 8 ? Teal : steel,true);
                    Part(62,48,10,84,steel); Part(216,48,10,84,steel);
                    Part(75,52,138,49,id == 8 ? Pale : CityWorld.ColorOf("B9D8D8"),true);
                    Part(id == 8 ? 122 : 94,102,id == 8 ? 49 : 94,9,wood,true);
                    Part(96,111,7,21,steel); Part(180,111,7,21,steel);
                    break;
                case 5:
                    Part(80,30,127,78,steel,true); Part(88,38,111,54,Pale,true);
                    Part(135,105,17,23,Ink); Part(107,128,76,7,steel,true);
                    break;
                case 6:
                    Part(62,47,166,81,steel,true); Part(69,53,152,58,CityWorld.ColorOf("C1DDDA"),true);
                    Part(73,79,143,3,White); Part(139,55,3,52,steel); Part(58,122,175,11,Ink,true);
                    break;
                case 7:
                    Part(125,56,34,68,steel,true); Part(109,124,67,9,steel,true);
                    Part(92,32,101,45,Teal,true); Part(106,41,40,25,Ink,true);
                    Part(155,47,21,8,light,true);
                    break;
            }
        }

        static float Low(int kind) => kind == 0 ? .35f : .32f;
        static float High(int kind) => kind == 0 ? .65f : .68f;
        const int InspectionTarget = 5;
        const float SequenceBeat = .53f;
        const float SequencePreview = SequenceBeat * InspectionTarget;

        void StartInspectionChallenge(InspectionChallenge challenge)
        {
            challenge.Elapsed = challenge.Stable = 0;
            challenge.Value = .5f;
            challenge.Count = challenge.Combo = 0;
            challenge.LastHitPass = -1;
            challenge.Running = true;
            challenge.Start.interactable = false;
            challenge.Status.text = challenge.Kind == 2 ? "순서를 기억하세요" : "측정 중";
            challenge.Status.color = Teal;
            if (challenge.Kind == 2)
            {
                challenge.Sequence = new int[InspectionTarget];
                for (int i = 0; i < challenge.Sequence.Length; i++)
                {
                    int value = Random.Range(0, i == 0 ? 4 : 3);
                    if (i > 0 && value >= challenge.Sequence[i - 1]) value++;
                    challenge.Sequence[i] = value;
                }
            }
        }

        void TickInspectionChallenge(float dt, Keyboard keyboard)
        {
            var challenge = inspectionChallenge;
            if (challenge.Finishing)
            {
                challenge.FinishDelay -= dt;
                if (challenge.FinishDelay <= 0) game.FinishInspectionMeasurement(challenge.Id);
                return;
            }
            if (!challenge.Running) return;
            challenge.Elapsed += dt;
            challenge.Clock.text = $"남은 시간 {Mathf.Max(0, challenge.Duration - challenge.Elapsed):0.0}초";
            if (challenge.Elapsed >= challenge.Duration)
            {
                RetryInspection(challenge, "시간이 끝났습니다. 다시 측정하세요.");
                return;
            }
            if (challenge.Kind == 2)
            {
                int flash = Mathf.Min(challenge.Sequence.Length - 1, (int)(challenge.Elapsed / SequenceBeat));
                bool previewing = challenge.Elapsed < SequencePreview;
                int lit = previewing && challenge.Elapsed % SequenceBeat < .36f ? challenge.Sequence[flash] : -1;
                UpdateInspectionPads(challenge, lit);
                if (!previewing && challenge.Count == 0 && challenge.Status.text == "순서를 기억하세요")
                    challenge.Status.text = "기억한 순서대로 입력하세요.";
                if (!previewing && keyboard != null)
                {
                    if (keyboard.digit1Key.wasPressedThisFrame) PressInspectionPad(challenge, 0);
                    else if (keyboard.digit2Key.wasPressedThisFrame) PressInspectionPad(challenge, 1);
                    else if (keyboard.digit3Key.wasPressedThisFrame) PressInspectionPad(challenge, 2);
                    else if (keyboard.digit4Key.wasPressedThisFrame) PressInspectionPad(challenge, 3);
                }
                return;
            }

            if (challenge.Kind == 0)
                challenge.Value = Mathf.PingPong(challenge.Elapsed * 1.55f, 1);
            else
            {
                int input = (keyboard?.dKey.isPressed == true ? 1 : 0) - (keyboard?.aKey.isPressed == true ? 1 : 0);
                float drift = .08f + .54f * Mathf.Sin(challenge.Elapsed * 4.4f) + .32f * Mathf.Cos(challenge.Elapsed * 2f);
                challenge.Value = Mathf.Clamp01(challenge.Value + (input * .85f + drift) * dt);
            }
            challenge.Needle.anchoredPosition = new Vector2(challenge.Value * 653, 3);
            bool inRange = challenge.Value >= Low(challenge.Kind) && challenge.Value <= High(challenge.Kind);
            if (challenge.Kind == 0 && keyboard?.spaceKey.wasPressedThisFrame == true)
            {
                int pass = (int)(challenge.Elapsed * 1.55f);
                if (inRange && pass != challenge.LastHitPass)
                {
                    challenge.LastHitPass = pass;
                    InspectionHit(challenge, InspectionTarget);
                }
                else if (inRange) return;
                else InspectionMiss(challenge, "초록 구간을 다시 노려보세요.");
            }
            if (challenge.Kind == 1)
            {
                if (inRange) challenge.Stable += dt;
                if (challenge.Stable >= challenge.Count + 1) InspectionHit(challenge, InspectionTarget);
                challenge.Clock.text += $"   ·   정상 유지 {Mathf.Min(InspectionTarget, challenge.Stable):0.0} / {InspectionTarget}초";
            }
        }

        void UpdateInspectionPads(InspectionChallenge challenge, int lit)
        {
            if (challenge.Pads == null) return;
            for (int i = 0; i < challenge.Pads.Length; i++)
            {
                challenge.Pads[i].image.color = i == lit ? Gold : Pale;
                challenge.Pads[i].interactable = challenge.Running;
            }
        }

        void PressInspectionPad(InspectionChallenge challenge, int pad)
        {
            if (!challenge.Running || challenge.Elapsed < SequencePreview) return;
            if (pad == challenge.Sequence[challenge.Count]) InspectionHit(challenge, challenge.Sequence.Length);
            else RetryInspection(challenge, "순서가 다릅니다. 다시 측정하세요.");
        }

        void RetryInspection(InspectionChallenge challenge, string message)
        {
            challenge.Running = false;
            challenge.Start.interactable = true;
            challenge.StartText.text = "다시 측정";
            InspectionMiss(challenge, message);
            UpdateInspectionPads(challenge, -1);
        }

        void InspectionMiss(InspectionChallenge challenge, string message)
        {
            challenge.Combo = 0;
            challenge.Status.text = message;
            challenge.Status.color = Rose;
            game.InspectionCue(0);
        }

        void InspectionHit(InspectionChallenge challenge, int goal)
        {
            int combo = ++challenge.Combo;
            int progress = ++challenge.Count;
            string rank = combo == 1 ? "GOOD" : combo == 2 ? "GREAT" : "PERFECT!";
            challenge.Status.text = $"{rank}   {progress} / {goal}";
            challenge.Status.color = combo >= 3 ? CityWorld.ColorOf("94651D") : Teal;
            game.InspectionCue(combo);
            if (progress < goal) return;
            challenge.Running = false;
            challenge.Finishing = true;
            challenge.FinishDelay = .46f;
            UpdateInspectionPads(challenge, -1);
        }
    }
}
