using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eleon.Modding;
using DebugMod;
using EmpyrionAPIMessageBroker;
using System.IO;
using DeJson;

namespace CrewProgressionMod
{
    public class CrewProgressionMod : ModInterface
    {
        private CrewProgressionManager cpm;
        private ChatCommandManager ccm;

        private Random rand = new Random();

        private CrewProgressionManagerSettings settings;

        MessageBroker broker;

        public static ModGameAPI GameAPI;
        public void Game_Event(CmdId eventId, ushort seqNr, object data)
        {
            broker.HandleMessage(eventId, seqNr, data);
            ccm.HandleMessage(eventId, data);
            
            
        }

        public void Game_Exit()
        {
            Account.SaveAccountListToFile(cpm.accounts.Values.ToList(), "Content/Mods/CPM/accounts.json");
        }

        public void Game_Start(ModGameAPI dediAPI)
        {
            GameAPI = dediAPI;
            dediAPI.Console_Write("!!CREW PROGRESSION MOD LOADED!!");
            broker = new MessageBroker(dediAPI);
            var accounts = Account.AccountsFromFile("Content/Mods/CPM/accounts.json");
            settings = CrewProgressionManagerSettings.SettingsFromFile("Content/Mods/CPM/settings.json");
            GameAPI.Console_Write($"SETTINGS****: {Serializer.Serialize(settings)}");
            
            cpm = new CrewProgressionManager(broker, accounts, settings);
            
            ccm = new ChatCommandManager(cpm.generateInitialChatCommands());

        }

        public void Game_Update()
        {
            // Random tick then checking
            if (rand.NextDouble() < settings.checkinProbability)
            {
                cpm.log("crew progression mod checkin");
                cpm.ExecuteTickedEvents(GameAPI.Game_GetTickTime());
            }
        }
    }

}
