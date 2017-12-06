using DeJson;
using System.Collections.Generic;
using System.IO;

namespace CrewProgressionMod
{
    public class ActionSpecifier
    {
        public string ActionName;
        public int interval;

        public ActionSpecifier() { }
    }

    public class CrewProgressionManagerSettings
    {
        public List<ActionSpecifier> actions;
        public bool verbose;
        public double checkinProbability;
        public double creditExchangeRate;
        public int startingUpgradePointBalance;

        public CrewProgressionManagerSettings() {
            actions = new List<ActionSpecifier>();
            verbose = true;
            checkinProbability = 0.5;
            creditExchangeRate = 1.0;
            startingUpgradePointBalance = 30;
        }

        public static CrewProgressionManagerSettings SettingsFromFile(string path)
        {
            using (var reader = new StreamReader(path))
            {
                var st = reader.ReadToEnd();
                var d = new Deserializer(true);
                var result = d.Deserialize<CrewProgressionManagerSettings>(st);
                return result;
            }

        }
    }
}
