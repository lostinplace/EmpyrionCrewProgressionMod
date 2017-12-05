using DeJson;
using Eleon.Modding;
using System;
using System.Collections.Generic;
using System.IO;


namespace CrewProgressionMod
{
    enum TransactionType
    {
        UpgradePoints,
        Credits
    }
    struct BalanceActionResult
    {
        public bool succeeded;
        public int crewBalance;
        public int playerBalance;
        public int amountRequested;
        public int crewAccount;
        public int playerAccount;
        public TransactionType transactionType;
        public string reason;
        public string playerName;
    }

    public class AccountPair
    {
        public Account account1 { get; }
        public Account account2 { get; }
    }

    public enum AccountType
    {
        Player,
        Crew
    }

    public enum ResourceType
    {
        Experience,
        Points
    }
    public class Account
    {
        public Dictionary<ResourceType, int> balances;

        public AccountType type;

        public string id;

        public Account() {}

        public Account(string id,  AccountType type, Dictionary<ResourceType, int> balances)
        {
            this.type = type;
            this.id = id;
            this.balances = balances;
        }

        public static string IdKeyFromInfo(AccountType type, string id)
        {
            return $"{type}:{id}";
        }

        public static string IdKeyFromInfo(AccountType type, int id)
        {
            return $"{type}:{id}";
        }


        public static Account NewFactionAccountFromPlayer(PlayerInfo player)
        {
            var exp = player.exp;
            var factionId = player.factionId;
            var balances = new Dictionary<ResourceType, int>
            {
                { ResourceType.Experience, player.exp },
                { ResourceType.Points, 0 }
            };

            return new Account(factionId.ToString(), AccountType.Crew, balances); 
        }

        public static Account NewPlayerAccount(PlayerInfo player)
        {
            var balances = new Dictionary<ResourceType, int>
            {
                { ResourceType.Points, 0 }
            };

            return new Account(player.entityId.ToString(), AccountType.Player, balances);
        }


        public static AccountPair ExecuteTransaction(AccountPair accounts, Func<AccountPair, AccountPair> order)
        {
            return order(accounts);
        }


        public static void SaveAccountListToFile(List<Account> accounts, string path)
        {
            using (var writer = new StreamWriter(path, false))
            {
                var output = Serializer.Serialize(accounts);
                writer.Write(output);
            }   
        }

        public static List<Account> AccountsFromFile(string path) {

            try
            {
                using (var reader = new StreamReader(path))
                {
                    var st = reader.ReadToEnd();
                    var d = new Deserializer(true);
                    var result = d.Deserialize<List<Account>>(st);
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return new List<Account>();
            }
        }
    }
}
