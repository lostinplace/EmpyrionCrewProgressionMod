﻿using DebugMod;
using DeJson;
using Eleon.Modding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EmpyrionAPIMessageBroker;
using AccountEffectList = System.Collections.Generic.List<System.Func<CrewProgressionMod.Account, CrewProgressionMod.Account>>;
using LedgerEffectList = System.Collections.Generic.List<System.Func<CrewProgressionMod.PlayerLedger, CrewProgressionMod.PlayerLedger>>;

namespace CrewProgressionMod
{
    public class TimedEvent
    {
        public string name;
        public ulong interval;
        public ulong nextOccurence;
        public Action activity;

        public TimedEvent() { }
    }

    partial class CrewProgressionManager
    {

        private bool verbose;

        private CrewProgressionManagerSettings settings;

        private ulong NextCheckin { get; set; }

        public EmpyrionAPIMessageBroker.MessageBroker broker { get; set; }

        public Dictionary<string, Account> accounts;

        public Dictionary<int, CrewInfo> CrewInfoTracker;

        private ChatCommandManager ccm;

        private HashSet<int> initializedPlayerIds;

        public List<TimedEvent> EventList;

        public CrewProgressionManager(MessageBroker aBroker, List<Account> accountList, CrewProgressionManagerSettings settings)
        {
            log(() => $"***INITIALIZE CPM");

            broker = aBroker;
            this.settings = settings;
            ccm = new ChatCommandManager(generateInitialChatCommands());
            this.verbose = settings.verbose;

            log(() => $"***loading accounts: {Serializer.Serialize(accountList)}");
            log(() => $"***settings: {Serializer.Serialize(settings)}");

            this.accounts = new Dictionary<string, Account>();
            accountList.ForEach(SaveAccount);
            var playerIds = accountList
                .Where(x => x.type == AccountType.Player)
                .Select(x => int.Parse(x.id));
            initializedPlayerIds = new HashSet<int>(playerIds);

            log(() => $"***loaded with accounts: {Serializer.Serialize(this.accounts)}");
            this.NextCheckin = aBroker.GameAPI.Game_GetTickTime();
            CrewInfoTracker = new Dictionary<int, CrewInfo>();


            actionManifest = new Dictionary<string, Action>()
            {
                { "Normalize Players", SimpleUpdate },
                { "Update Points", UpdateWithPointIncrement },
                { "Evaluate Research Teams", updateCrewResearchTeams },
                { "Save Accounts",  ()=> Account.SaveAccountListToFile(this.accounts.Values.ToList(), "Content/Mods/CPM/accounts.json") }
            };

            EventList = generateEventList(settings.actions);
        }

        private Dictionary<string, Action> actionManifest;

        private List<TimedEvent> generateEventList(List<ActionSpecifier> actions)
        {
            var output = actions.Select(x => new TimedEvent() {
                name = x.ActionName,
                activity = this.actionManifest[x.ActionName],
                interval = (ulong) x.interval,
                nextOccurence = 0
            });
            return output.ToList();
        }

        public void ExecuteTickedEvents(ulong tick)
        {
            for(var i = 0; i<EventList.Count; i++)
            {
                var item = EventList[i];
                log(() => $"checking item {item.name}@{tick}  next checkin:{item.nextOccurence}");

                if (tick < item.nextOccurence) continue;

                var timeSinceCheckin = tick - item.nextOccurence;
              
                item.nextOccurence = tick+item.interval;
                log(() => $"next {item.name} : {item.nextOccurence}");
                item.activity();
                EventList[i] = item;
            }
        }

        public void UpdateWithPointIncrement()
        {
            var activePlayerAccountEffects = new LedgerEffectList()
            {
                shiftBalanceToPlayerOnPersonalFaction,
                initializeNewAccount,
            };
            var activeCrewAccountEffects = new AccountEffectList()
            {
                incrementPointOnActiveFaction
            };
            
            var handler = generateActivePlayerListHandler(activeCrewAccountEffects, activePlayerAccountEffects, true);
            
            broker.ExecuteCommand<IdList>(CmdId.Request_Player_List, handler);
        }

        private Account incrementPointOnActiveFaction(Account factionAccount)
        {
            var key = int.Parse(factionAccount.id);
            log(() => $"*** incrementing faction points for {key}");
            CrewInfo info;
            log(() => $"current crews: {Serializer.Serialize(CrewInfoTracker)}");
            info = CrewInfoTracker.TryGetValue(key, out info) ? info : new CrewInfo();
            

            var points = (info.crewMembers.Keys.Count + info.researchTeam.members.Count + (info.researchTeam.leader !=null?1:0));

            log(() => $"*** points: { points}");
            log(() => $"*** members: { info.crewMembers.Keys.Count}");
            log(() => $"*** cluster size: { info.researchTeam.members.Count}");

            factionAccount.balances[ResourceType.Points] += points;
            var message = generateFactionPointMessage(info, points);

            if (info.crewMembers.Keys.Count == 0) {
                log(() => $"crew has no active members {Serializer.Serialize(info)}");
                return factionAccount;
            }

            var singlePlayerCrew = info.crewMembers.Keys.Count == 1;

            var singlePlayerId = info.crewMembers.Values.First().entityId;
            var factionId = int.Parse(factionAccount.id);



            var msg = new IdMsgPrio()
            {
                id = singlePlayerCrew ? singlePlayerId: factionId,
                msg = message,
                prio = 1
            };
            log(() => $"factionMessage: {message}");
            log(() => $"crewInfo: {Serializer.Serialize(info)}");
            var cmdId = singlePlayerCrew ? CmdId.Request_InGameMessage_SinglePlayer : CmdId.Request_InGameMessage_Faction;
            var cmd = new APICmd(cmdId , msg);
            broker.ExecuteCommand(cmd);
            info.researchTeam = new ResearchTeam();
            CrewInfoTracker[key] = info;
            return factionAccount;
        }

        private string generateFactionPointMessage(CrewInfo info, int points)
        {
            var pointMessage = $"Your crew generated {points} points";
            string researchMessage="";
            if(info.researchTeam.leader != null && info.researchTeam.teamSize != 0)
            {
                researchMessage = $"{info.researchTeam.leader.playerName} is leading a team of {info.researchTeam.teamSize}";
            }
            return $"{pointMessage}\n{researchMessage}";
        }

        public void SimpleUpdate()
        {
            var activePlayerAccountEffects = new LedgerEffectList()
            {
                
                shiftBalanceToPlayerOnPersonalFaction,
                captureGameEffectPoints,
                initializeNewAccount,
            };
            
            var activeCrewAccountEffects = new AccountEffectList();
            var handler = generateActivePlayerListHandler(activeCrewAccountEffects, activePlayerAccountEffects);

            broker.ExecuteCommand<IdList>(CmdId.Request_Player_List, handler);
        }

        private PlayerLedger initializeNewAccount(PlayerLedger ledger)
        {
            if (!initializedPlayerIds.Contains(ledger.info.entityId))
            {
                var points = ledger.info.upgrade > settings.startingUpgradePointBalance ? ledger.info.upgrade: settings.startingUpgradePointBalance;
                ledger.PersonalAccount.balances[ResourceType.Points] = points;
                initializedPlayerIds.Add(ledger.info.entityId);
            }
            return ledger;
        }

        static Random rnd = new Random();

        private ResearchTeam GetLargestResearchTeamForDistance(List<PlayerInfo> players, double radius)
        {
            var originIndex = rnd.Next(players.Count);
            var originPlayer = players[originIndex];
            var members = new List<PlayerInfo>();

            log(() => "evaluating research team");
            log(() => $"origin player {originPlayer.playerName} @ {originIndex}/{players.Count}");
            
            for (int i = 1; i < players.Count; i++)
            {
                var index = (originIndex + i) % players.Count;
                var compared = players[index];


                var distance = getDistanceBetweenPlayers(originPlayer, compared);
                log(() => $"distance between {originPlayer.playerName} and {compared.playerName} is {distance} ");
                if ( distance < radius)
                {
                    members.Add(compared);
                }
            }
            log(() => $"research team with {members.Count} members");
            return new ResearchTeam() {
                leader = originPlayer,
                members = members
            };
        }

        private double getDistanceBetweenPlayers(PlayerInfo a, PlayerInfo b)
        {
            var xDist = Math.Pow(a.pos.x - b.pos.x, 2);
            var yDist = Math.Pow(a.pos.y - b.pos.y, 2);
            var zDist = Math.Pow(a.pos.z - b.pos.z, 2);
            return Math.Sqrt(xDist + yDist + zDist);
        }


        private void updateCrewResearchTeams()
        {
            foreach (var item in CrewInfoTracker.Keys.ToList())
            {
                log(() => $"evaluating research team for {item}");
                var crew = CrewInfoTracker[item];
                var crewList = crew.crewMembers.Values.ToList();
                log(() => $"evaluating research team from {Serializer.Serialize(crewList)}");
                var researchTeam = GetLargestResearchTeamForDistance(crewList, this.settings.researchTeamMaxDistance);
                log(() => $"largest research team for {item} was {Serializer.Serialize(researchTeam)}");
                log(() => $"existing research team for {item} was {Serializer.Serialize(crew.researchTeam)}");
                log(() => $"{researchTeam.members.Count} vs {crew.researchTeam.teamSize}");

                if (researchTeam.members.Count > crew.researchTeam.teamSize )
                {
                    log(() => "updating research team");
                    crew.researchTeam = researchTeam;
                }

                CrewInfoTracker[item] = crew;
            }
        }

        public void transferBalance(int playerId, int amount, TransactionType type, Action<BalanceActionResult> callback)
        {
            var id = new Id(playerId);
            var cmd = new APICmd(CmdId.Request_Player_Info) + id;
            broker.ExecuteCommand<PlayerInfo>(cmd, (x, y) => {
                log(() => "****player info received");
                BalanceActionResult result;
                var factionAccount = SafeGetAccount(AccountType.Crew, y.factionId, y);
                var playerAccount = SafeGetAccount(AccountType.Player, y.entityId, y);

                result = new BalanceActionResult()
                {
                    crewBalance = factionAccount.balances[ResourceType.Points],
                    playerBalance = playerAccount.balances[ResourceType.Points],
                    amountRequested = amount,
                    playerAccount = playerId,
                    crewAccount = y.factionId,
                    transactionType = type,
                    playerName = y.playerName
                };

                var requestedCrewBalance = result.crewBalance - amount;
                var requestedPlayerBalance = result.playerBalance + amount;

                if (requestedCrewBalance < 0)
                {
                    result.reason = "insufficient crew balance";
                    result.succeeded = false;

                }
                else if (requestedPlayerBalance < 0)
                {
                    result.reason = "insufficient player balance";
                    result.succeeded = false;
                } else
                {
                    result.crewBalance = requestedCrewBalance;
                    result.playerBalance = requestedPlayerBalance;
                    result.succeeded = true;
                }

                log(() => $"***transaction: {Serializer.Serialize(result)}");

                playerAccount.balances[ResourceType.Points] = result.playerBalance;
                SaveAccount(playerAccount);
                factionAccount.balances[ResourceType.Points] = result.crewBalance;
                SaveAccount(factionAccount);
                var ledger = new PlayerLedger(y, factionAccount, playerAccount);
                normalizePlayer(ledger);

                callback(result);
            });
        }

        private BalanceActionResult convertPointsToCredits(int playerId, int points)
        {
            var account = SafeGetAccount(AccountType.Player, playerId, null);
            log(() => $"*** before account: {Serializer.Serialize(account)}");
            var result = new BalanceActionResult()
            {
                amountRequested = points,
                playerAccount = playerId,
                transactionType = TransactionType.Credits,
                playerBalance = account.balances[ResourceType.Points]
            };
            if(result.playerBalance >= points)
            {
                var credits = points * settings.creditExchangeRate;
                account.balances[ResourceType.Points] -= points;
                result.playerBalance = account.balances[ResourceType.Points];

                normalizePlayerPoints(playerId, account);
                SaveAccount(account);
                
                log(() => $"*** after account: {Serializer.Serialize(account)}");
                var idCredits = new IdCredits()
                {
                    id = playerId,
                    credits = credits
                };

                var cmd = new APICmd(CmdId.Request_Player_AddCredits, idCredits);
                broker.ExecuteCommand(cmd);
                result.succeeded = true;
                
            }
            else {
                result.succeeded = false;
                result.reason = "insuffucient point balance";
            }
            return result;            
        }

        private void SaveAccount(Account acc)
        {
            var key = Account.IdKeyFromInfo(acc.type, acc.id);
            accounts[key] = acc;
        }

        private void normalizePlayerPoints(int playerId, Account playerAccount)
        {
            var infoSet = new PlayerInfoSet()
            {
                entityId = playerId,
                upgradePoints = playerAccount.balances[ResourceType.Points],
            };

            var cmd = new APICmd(CmdId.Request_Player_SetPlayerInfo, infoSet);
            broker.ExecuteCommand(cmd);
        }



        private void normalizePlayer(PlayerLedger ledger)
        {
            var infoSet = new PlayerInfoSet()
            {
                entityId = ledger.info.entityId,
                experiencePoints = ledger.FactionAccount.balances[ResourceType.Experience],
                upgradePoints = ledger.PersonalAccount.balances[ResourceType.Points],
            };
            log(() => $"normalizing player: {ledger.info.playerName} with points: { infoSet.upgradePoints}");
            log(() => $"factionAccount: {Serializer.Serialize(ledger.FactionAccount)} playerAccount: {Serializer.Serialize(ledger.PersonalAccount)}");

            var cmd = new APICmd(CmdId.Request_Player_SetPlayerInfo, infoSet);
            broker.ExecuteCommand(cmd);
        }

        private Account SafeGetAccount(PlayerInfo player)
        {
            return SafeGetAccount(AccountType.Player, player.entityId, player);
        }

        private Account SafeGetAccount(AccountType type, int id, PlayerInfo player)
        {
            var tmpAccountKey = Account.IdKeyFromInfo(type, id);
            Account tmpAccount;
            var tmpAccountFound = accounts.TryGetValue(tmpAccountKey, out tmpAccount);

            if (!tmpAccountFound && type == AccountType.Crew)
                tmpAccount = Account.NewFactionAccountFromPlayer(player);
            else if (!tmpAccountFound)
                tmpAccount = Account.NewPlayerAccount(player, settings.startingUpgradePointBalance);
            
            return tmpAccount;
        }

        private Dictionary<int,CrewInfo> UpdatedCrewManifest(Dictionary<int, CrewInfo> crewManifest, PlayerInfo player)
        {
            CrewInfo crew;
            crew = crewManifest.TryGetValue(player.factionId, out crew) ? crew : new CrewInfo();
            crew.crewMembers[player.entityId] = player;
            crewManifest[player.factionId] = crew;
            
            return crewManifest;
        }

        private PlayerLedger shiftBalanceToPlayerOnPersonalFaction(PlayerLedger ledger)
        {
            if (ledger.PersonalAccount.id == ledger.FactionAccount.id)
            {
                ledger.PersonalAccount.balances[ResourceType.Points] += ledger.FactionAccount.balances[ResourceType.Points];
                ledger.FactionAccount.balances[ResourceType.Points] = 0;
            }
            return ledger;
        }

        private PlayerLedger captureGameEffectPoints(PlayerLedger ledger)
        {
            var norm = new PlayerNorm(ledger.FactionAccount, ledger.PersonalAccount);
            if (norm != ledger.norm) return ledger;

            log(() => $"norm is {Serializer.Serialize(norm)}\nledger norm is:{Serializer.Serialize(ledger.norm)}\nplayer points is:{ledger.info.upgrade}");

            if (ledger.PersonalAccount.balances[ResourceType.Points] != ledger.info.upgrade)
            {
                ledger.PersonalAccount.balances[ResourceType.Points] = ledger.info.upgrade;
            }
            return ledger;
        }



        private Action<IdList> generateActivePlayerListHandler(
            AccountEffectList activeCrewAccountEffects,
            LedgerEffectList activePlayerAccountEffects,
            bool resetCrews = false)
        {
            Action<IdList> handler = playerIdList =>
            {
                var incrementedFactions = new HashSet<int>();
                var updatedCrewInfos = new Dictionary<int, CrewInfo>();
                var count = 0;

                foreach (var item in playerIdList.list)
                {
                    var id = new Id(item);
                    var cmd = new APICmd(CmdId.Request_Player_Info) + id;
                    

                    broker.ExecuteCommand<PlayerInfo>(cmd, player =>
                    {
                        updatedCrewInfos = UpdatedCrewManifest(updatedCrewInfos, player);

                        var factionAccount = SafeGetAccount(AccountType.Crew, player.factionId, player);
                        var playerAccount = SafeGetAccount(AccountType.Player, player.entityId, player);

                        var factionExp = factionAccount.balances[ResourceType.Experience];
                        factionAccount.balances[ResourceType.Experience] = factionExp  < player.exp ? player.exp : factionExp;

                        if (!incrementedFactions.Contains(player.factionId))
                        {
                            var factionAccountKey = Account.IdKeyFromInfo(AccountType.Crew, player.factionId);
                            var tmpAcct = factionAccount;
                            factionAccount = activeCrewAccountEffects.Aggregate(tmpAcct, (acc, x) => x(acc));
                            incrementedFactions.Add(player.factionId);
                        }

                        var baseLedger = new PlayerLedger(player, factionAccount, playerAccount);
                        var adjustedLedger = activePlayerAccountEffects.Aggregate(baseLedger, (memo, effect) => effect(memo));
                        playerAccount = adjustedLedger.PersonalAccount;
                        factionAccount = adjustedLedger.FactionAccount;                      

                        SaveAccount(playerAccount);
                        SaveAccount(factionAccount);

                        normalizePlayer(adjustedLedger);
                        count++;
                        log(() => $"normalized {count}/{ playerIdList.list.Count}: {player.playerName}");

                        if(count == playerIdList.list.Count) {
                            log(() => $"updating crews to: {Serializer.Serialize(CrewInfoTracker)}");
                            CrewInfoTracker = updatedCrewInfos;
                        }
                    });
                }
            };
            return handler;            
        }

        public void log(string message)
        {
            if (verbose)
            {
                broker.GameAPI.Console_Write(message);
            }
        }

        public void log(Func<String> messageGenerator)
        {
            if (verbose)
            {
                broker.GameAPI.Console_Write(messageGenerator());
            }
        }
        
    }
}
