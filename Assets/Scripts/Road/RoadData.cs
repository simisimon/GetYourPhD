using UnityEngine;

namespace GetYourPhD
{
    /// <summary>One thing on the road map (Assets/Levels/Level3_RoadTrip.txt), placed by the builder.</summary>
    [System.Serializable]
    public class RoadEvent
    {
        public float y;
        public string kind;   // one map character, see RoadLevelBuilder
        public int col;       // 0..3 lanes, 4 = right shoulder (gas station / workshop)
        public string text;   // signs
        public int seed;
    }

    /// <summary>One country leg. Crossing its start border saves the checkpoint.</summary>
    [System.Serializable]
    public class RoadLeg
    {
        public string code = "DE";
        public string name = "Germany";
        public string welcome = "Willkommen in Deutschland";
        public float startY, endY;
        public int firstCol;          // 0 = four lanes, 1 = three lanes
        public float curveAmp;        // road bends (Alps)
        public float curveLen = 80f;
        public int theme;             // 0 Germany, 1 Alps, 2 Italy
        public Color flagA = Color.black, flagB = Color.red, flagC = Color.yellow;
        /// <summary>0 = vertical tricolour (Italy), 1 = horizontal tricolour (Germany, Austria), 2 = white cross on red (Switzerland).</summary>
        public int flagStyle;
        /// <summary>Optional second country inside this leg (Switzerland in the Alps leg), shown from this y on. -1 = none.</summary>
        public float altFromY = -1f;
        public string altName;
        /// <summary>Country shown in the HUD (defaults to name).</summary>
        public string hudName;
        public int altFlagStyle = 2;
        public Color altFlagA = Color.red, altFlagB = Color.white, altFlagC = Color.white;
        public Sprite postcard;
        public string postcardTitle = "Greetings from Germany";
        public int Lanes => 4 - firstCol;
    }

    /// <summary>All sprites the road needs, filled by the builder.</summary>
    [System.Serializable]
    public class RoadArt
    {
        public Sprite pixel;
        public Sprite[] camperDamage;        // 0 intact .. 2 badly dented
        public Sprite[] traffic;             // ordinary cars
        public Sprite sportsCar, reviewer2, vespa, cow, smallCar, rock, pothole, barrier;
        public Sprite jerry, paper, citation, stamp, mate, postcardIcon;
        public Sprite[] postcardIcons;  // one per leg
        public Sprite gasStation, workshop, tollBooth, venue, shadow, warn;
        public float venueScale = 2f;
        public string venueName = "PAULINUM LEIPZIG";
        public Sprite[] asphalt;   // per theme
        public Sprite[] grass;     // per theme
        public Sprite[] decorDE, decorAT, decorIT;
        public Sprite signBoard, gantry, finishLine, laneDash;
    }
}
