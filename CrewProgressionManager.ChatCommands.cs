using Eleon.Modding;
using EmpyrionAPIMessageBroker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChatCommandHandler = System.Action<Eleon.Modding.ChatInfo, Eleon.Modding.PString>;


namespace CrewProgressionMod
{
    partial class CrewProgressionManager
    {

        public Dictionary<string, ChatCommandHandler> generateInitialChatCommands()
        {
            var chatCommands = new Dictionary<string, ChatCommandHandler>()
                {
                    { @"crew balance",  checkCrewBalance},
                    { @"crew withdraw (.*)",  withdrawUpgradePoints},
                    { @"crew convert (.*)",  convertCredits}
                };

            return chatCommands;
        }

        private void checkCrewBalance(ChatInfo data, PString subcommand)
        {
            var playerId = new Id(data.playerId);
            var cmd = new APICmd(CmdId.Request_Player_Info, playerId);
            broker.HandleCall<PlayerInfo>(cmd, (cmdId, playerInfo) =>
            {
                var crewAccount = SafeGetAccount(AccountType.Crew, playerInfo.factionId, playerInfo);
                var crewBalance = crewAccount.balances[ResourceType.Points];
                var personalAccount = SafeGetAccount(playerInfo);
                var personalBalance = personalAccount.balances[ResourceType.Points];
                var balanceMessage = $"your crew has a balance of {crewBalance} points;\nyour personal balance is {personalBalance} points";
                var msg = new IdMsgPrio()
                {
                    id = playerInfo.entityId,
                    msg = balanceMessage,
                    prio = 1
                };
                var outmsg = new APICmd(CmdId.Request_InGameMessage_SinglePlayer, msg);
                broker.HandleCall(outmsg);
            });
        }

        private void withdrawUpgradePoints(ChatInfo data, PString subcommand)
        {
            int value;
            value = int.TryParse(subcommand.pstr, out value) ? value : -1;
            if (value < 0) return;
            log(()=>$"*** beginning transfer of {value}");
            this.transferBalance(data.playerId, value, TransactionType.UpgradePoints, x => {
                log(() => "***** transfer response");

                string balanceMessage;
                string factionMessage;
                if (x.succeeded)
                {
                    balanceMessage = $"withdrawal successful\nyour crew has a balance of {x.crewBalance} points;\nyour personal balance is {x.playerBalance} points";
                    factionMessage = $"{x.playerName} has withdrawn {value} points from the crew bank";

                    var facmsg = new IdMsgPrio()
                    {
                        id = x.crewAccount,
                        msg = factionMessage,
                        prio = (byte)(x.succeeded ? 1 : 0)
                    };

                    var cmd = new APICmd(CmdId.Request_InGameMessage_Faction, facmsg);
                    broker.HandleCall(cmd);
                }
                else
                {
                    balanceMessage = $"withdrawal failed: {x.reason}";
                }

                var msg = new IdMsgPrio()
                {
                    id = data.playerId,
                    msg = balanceMessage,
                    prio = (byte) (x.succeeded ? 1:0)
                };
                var outmsg = new APICmd(CmdId.Request_InGameMessage_SinglePlayer, msg);
                broker.HandleCall(outmsg);
            });
        }

        private void convertCredits(ChatInfo data, PString subcommand)
        {
            int value;
            value = int.TryParse(subcommand.pstr, out value) ? value : -1;
            if (value < 0) return;
            var result = convertPointsToCredits(data.playerId, value);
            var msg = new IdMsgPrio()
            {
                id = data.playerId,
                msg = $"you converted {result.amountRequested} points to credits",
                prio = 1
            };
            var outmsg = new APICmd(CmdId.Request_InGameMessage_SinglePlayer, msg);
            broker.HandleCall(outmsg);
        }
    }
}
