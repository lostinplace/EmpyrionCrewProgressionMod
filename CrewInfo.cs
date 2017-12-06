using Eleon.Modding;
using System.Collections.Generic;

namespace CrewProgressionMod
{

    public class ResearchTeam
    {
        public PlayerInfo leader;
        public List<PlayerInfo> members;

        public int teamSize
        {
            get
            {
                return this.members.Count;
            }
        }

        public ResearchTeam()
        {
            members = new List<PlayerInfo>();
        }
    }

    public class CrewInfo
    {
        public Dictionary<int,PlayerInfo> crewMembers;
        public ResearchTeam researchTeam;

        public CrewInfo()
        {
            researchTeam = new ResearchTeam();
            crewMembers = new Dictionary<int, PlayerInfo>();
        }

    }
}
