using UnityEngine;
using UnityEngine.UI;

namespace GetYourPhD
{
    /// <summary>
    /// Level 3 additions to the HUD (the deadline clock and Paper meter come from <see cref="HUD"/>):
    /// country + fuel + damage panel top-right, postcards bottom-left, the fail flash,
    /// the "deadline missed" screen and the arrival screen with the collected postcards.
    /// </summary>
    public class RoadHUD : MonoBehaviour
    {
        public static RoadHUD Instance { get; private set; }

        public Font font;
        public Sprite camperIcon, jerryIcon, postcardBlank;

        RoadTrip trip;
        Text country, fuelText, kmh;
        Image fuelFill, flagA, flagB, flagC;
        Image[] hpIcons;
        Image[] cards;
        GameObject failPanel, endPanel;
        Text failTitle, failSub, endTitle, endInfo, endHelp;
        Image[] endCards; Text[] endCardText;

        static readonly Color Ink = new Color(0.06f, 0.08f, 0.14f, 0.82f);
        static readonly Color Caption = new Color(0.7f, 0.8f, 1f);

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            trip = RoadTrip.Instance;
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        Text Label(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fs, TextAnchor align, Color c, string text = "")
        {
            var t = Rect("Text", parent, anchor, pivot, pos, size).gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = fs; t.alignment = align; t.color = c; t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.8f); sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        Image Panel(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color c, Sprite s = null)
        {
            var img = Rect("Panel", parent, anchor, pivot, pos, size).gameObject.AddComponent<Image>();
            img.color = c; img.sprite = s; img.raycastTarget = false;
            if (s) img.preserveAspect = true;
            return img;
        }

        void Build()
        {
            var cgo = new GameObject("RoadCanvas", typeof(Canvas), typeof(CanvasScaler));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.GetComponent<Canvas>();
            roadCanvas = canvas;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;
            var sc = cgo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1280, 720);
            sc.matchWidthOrHeight = 0.5f;
            var root = cgo.transform;

            // Top-right: country, fuel, damage
            var p = Panel(root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(270, 112), Ink).transform;
            flagA = Panel(p, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -10), new Vector2(8, 22), Color.black);
            flagB = Panel(p, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -10), new Vector2(8, 22), Color.red);
            flagC = Panel(p, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -10), new Vector2(8, 22), Color.yellow);
            flagC.transform.SetAsLastSibling();
            country = Label(p, new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -8), new Vector2(220, 26), 20, TextAnchor.UpperLeft, Color.white);

            Panel(p, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -40), new Vector2(26, 26), Color.white, jerryIcon);
            var back = Panel(p, new Vector2(0, 1), new Vector2(0, 1), new Vector2(42, -46), new Vector2(160, 16), new Color(0.15f, 0.18f, 0.25f));
            fuelFill = Panel(back.transform, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(160, 16), new Color(0.4f, 0.9f, 0.4f));
            fuelText = Label(p, new Vector2(0, 1), new Vector2(0, 1), new Vector2(208, -42), new Vector2(60, 22), 16, TextAnchor.UpperLeft, Caption);

            Label(p, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -78), new Vector2(90, 22), 16, TextAnchor.UpperLeft, Caption, "CAMPER");
            hpIcons = new Image[3];
            for (int i = 0; i < 3; i++)
                hpIcons[i] = Panel(p, new Vector2(0, 1), new Vector2(0, 1), new Vector2(96 + i * 30, -70), new Vector2(18, 34), Color.white, camperIcon);
            kmh = Label(p, new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, -78), new Vector2(76, 22), 16, TextAnchor.UpperRight, Color.white);

            // Bottom-left: postcards
            var pc = Panel(root, new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 16), new Vector2(214, 62), Ink).transform;
            Label(pc, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -4), new Vector2(200, 18), 14, TextAnchor.UpperLeft, Caption, "POSTCARDS");
            int n = trip ? trip.legs.Length : 3;
            cards = new Image[n];
            for (int i = 0; i < n; i++)
                cards[i] = Panel(pc, new Vector2(0, 0), new Vector2(0, 0), new Vector2(10 + i * 66, 6), new Vector2(60, 34), new Color(1, 1, 1, 0.25f), postcardBlank);

            // Fail flash
            var fp = Panel(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000), new Color(0.25f, 0.02f, 0.02f, 0.6f));
            failPanel = fp.gameObject;
            failTitle = Label(fp.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1000, 60), 48, TextAnchor.MiddleCenter, Color.white);
            failSub = Label(fp.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1000, 40), 24, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.6f));
            failPanel.SetActive(false);

            // End screen (arrival or deadline missed)
            var ep = Panel(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000), new Color(0.02f, 0.03f, 0.08f, 0.85f));
            endPanel = ep.gameObject;
            var box = Panel(ep.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 560), new Color(0.95f, 0.94f, 0.9f)).transform;
            endTitle = Label(box, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(860, 60), 44, TextAnchor.UpperCenter, new Color(0.15f, 0.55f, 0.25f));
            endInfo = Label(box, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(860, 60), 20, TextAnchor.UpperCenter, new Color(0.2f, 0.2f, 0.25f));
            endCards = new Image[n]; endCardText = new Text[n];
            for (int i = 0; i < n; i++)
            {
                float x = (i - (n - 1) * 0.5f) * 280f;
                var frame = Panel(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -10), new Vector2(250, 180), Color.white);
                frame.transform.localRotation = Quaternion.Euler(0, 0, (i - 1) * -4f);
                endCards[i] = Panel(frame.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(234, 150), Color.white);
                endCards[i].preserveAspect = true;
                endCardText[i] = Label(frame.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(260, 40), 16, TextAnchor.UpperCenter, new Color(0.25f, 0.25f, 0.3f));
            }
            endHelp = Label(box, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(860, 30), 20, TextAnchor.LowerCenter, new Color(0.2f, 0.2f, 0.25f));
            foreach (var s in box.GetComponentsInChildren<Shadow>()) s.effectColor = new Color(0, 0, 0, 0.12f);
            endPanel.SetActive(false);
        }

        void Update()
        {
            if (!trip || !country) return;
            var c = CamperController.Instance;
            var leg = trip.Leg;
            bool alt = leg.altFromY >= 0f && c && c.Y >= leg.altFromY;
            country.text = (alt ? leg.altName : string.IsNullOrEmpty(leg.hudName) ? leg.name : leg.hudName).ToUpperInvariant();
            if (alt) SetFlag(leg.altFlagStyle, leg.altFlagA, leg.altFlagB, leg.altFlagC);
            else SetFlag(leg.flagStyle, leg.flagA, leg.flagB, leg.flagC);
            if (c)
            {
                float f = c.Fuel / c.fuelMax;
                fuelFill.rectTransform.sizeDelta = new Vector2(160f * f, 16f);
                bool low = f < 0.25f;
                fuelFill.color = low ? (Mathf.Repeat(Time.time * 2f, 1f) > 0.5f ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.7f, 0.3f))
                                     : Color.Lerp(new Color(1f, 0.75f, 0.3f), new Color(0.4f, 0.9f, 0.4f), (f - 0.25f) / 0.5f);
                fuelText.text = Mathf.CeilToInt(f * 100f) + "%";
                for (int i = 0; i < hpIcons.Length; i++)
                    hpIcons[i].color = i < c.Hp ? Color.white : new Color(0.3f, 0.3f, 0.35f, 0.6f);
                kmh.text = Mathf.RoundToInt(c.Kmh) + " km/h";
                kmh.color = c.Speeding ? new Color(1f, 0.55f, 0.4f) : Color.white;
            }
            for (int i = 0; i < cards.Length; i++)
            {
                bool got = trip.Postcards[i];
                cards[i].sprite = got && trip.legs[i].postcard ? trip.legs[i].postcard : postcardBlank;
                cards[i].color = got ? Color.white : new Color(1, 1, 1, 0.25f);
            }
        }

        /// <summary>The flag box is 24 x 22 px at (12, -10) in the panel.</summary>
        void SetFlag(int style, Color a, Color b, Color c)
        {
            const float x0 = 12f, y0 = -10f, w = 24f, h = 22f;
            flagA.color = a; flagB.color = b; flagC.color = c;
            if (style == 1) // horizontal stripes, top to bottom a, b, c
            {
                float sh = h / 3f;
                Place(flagA, x0, y0, w, sh); Place(flagB, x0, y0 - sh, w, sh); Place(flagC, x0, y0 - 2f * sh, w, h - 2f * sh);
            }
            else if (style == 2) // Swiss: square red field with a white cross
            {
                float s = h, xs = x0 + (w - s) * 0.5f;
                Place(flagA, xs, y0, s, s);
                Place(flagB, xs + s * 0.2f, y0 - s * 0.4f, s * 0.6f, s * 0.2f);
                Place(flagC, xs + s * 0.4f, y0 - s * 0.2f, s * 0.2f, s * 0.6f);
            }
            else // vertical stripes, left to right a, b, c
            {
                float sw = w / 3f;
                Place(flagA, x0, y0, sw, h); Place(flagB, x0 + sw, y0, sw, h); Place(flagC, x0 + 2f * sw, y0, w - 2f * sw, h);
            }
        }

        static void Place(Image img, float x, float y, float w, float h)
        {
            img.rectTransform.anchoredPosition = new Vector2(x, y);
            img.rectTransform.sizeDelta = new Vector2(w, h);
        }

        public void ShowFail(string title, string sub)
        {
            failPanel.SetActive(true);
            failTitle.text = title;
            failSub.text = sub;
        }

        public void HideFail() { if (failPanel) failPanel.SetActive(false); }

        public void ShowLost()
        {
            endPanel.SetActive(true);
            endTitle.text = "Deadline missed!";
            endTitle.color = new Color(0.75f, 0.2f, 0.15f);
            float pct = trip ? trip.Progress : 0f;
            endInfo.text = $"The committee waited, but the camper only got {Mathf.RoundToInt(pct * 100)}% of the way.\nPostcards you collected on the way:";
            FillCards();
            endHelp.text = "R / Enter: try the road trip again";
        }

        public void ShowArrival(RoadTrip t, float fill, string spare)
        {
            endPanel.SetActive(true);
            endTitle.text = "Arrived at the Paulinum!";
            endTitle.color = new Color(0.15f, 0.55f, 0.25f);
            int n = 0; foreach (var b in t.Postcards) if (b) n++;
            endInfo.text = $"{spare} to spare.   Paper {Mathf.RoundToInt(fill * 100)}% complete.   Postcards: {n} of {t.Postcards.Length}\nYour travel memories:";
            FillCards();
            endHelp.text = "Enter / A: go to the Paulinum entrance      R / X: drive again";
        }

        Canvas roadCanvas;

        /// <summary>Hides the end screen and the whole road HUD while the Paulinum keypad is open.</summary>
        public void SetEndVisible(bool visible)
        {
            if (endPanel) endPanel.SetActive(visible);
            if (roadCanvas) roadCanvas.enabled = visible;
        }

        public void ShowSubmitted(RoadTrip t, float fill)
        {
            endPanel.SetActive(true);
            endTitle.text = "Paper submitted as is";
            endTitle.color = new Color(0.75f, 0.45f, 0.05f);
            int n = 0; foreach (var b in t.Postcards) if (b) n++;
            endInfo.text = $"The camper broke down {Mathf.RoundToInt(t.Progress * 100)}% of the way to the Paulinum.\nPaper {Mathf.RoundToInt(fill * 100)}% complete.   Postcards: {n} of {t.Postcards.Length}";
            FillCards();
            endHelp.text = "Enter / A: walk to the Paulinum entrance      R / X: drive again";
        }

        void FillCards()
        {
            for (int i = 0; i < endCards.Length; i++)
            {
                bool got = trip.Postcards[i];
                var leg = trip.legs[i];
                endCards[i].sprite = got ? leg.postcard : postcardBlank;
                endCards[i].color = got ? Color.white : new Color(0.75f, 0.75f, 0.78f);
                endCardText[i].text = got ? leg.postcardTitle : "(missed: " + leg.name + ")";
            }
        }
    }
}
