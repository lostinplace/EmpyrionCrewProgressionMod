# Empyrion Crew Progression mod

## NOTE:  This is currently in an Alpha state.

## FAQ

### Q:  What is this?

My friends and I play coop on a dedicated server, and we have some frustrations in the game regarding progression:
- Those of us who play a lot are always higher level than more casual players, and this causes them distress
- In the early game, we end up having to manage tech tree knowledge in very silly ways.
- There are many times where we want to buy things but we have no credits.

This is a mod based on a few premises:
- A "Faction" is a crew.  This crew is working together to survive, and they have a shared pool of research.
- Crews accumulate research in regular intervals.
- Crews accumulate research faster when they work together, in this case when they are close together for long periods of time.
- The level of the faction is the aggregate of all the experience collected by faction members.  Everyone in the faction is of the faction's level
- Each crewmember has a personal account of upgrade points, and crew faction has an account of points that can be withdrawn and used by any crewmember
- A research point can be exchanged for a set number of credits

A player by his/herself receivesupgrade points as normal, with the benefit of getting an additional point on regular intervals.  When a crew is joined, the player stops receiving regular research points, and those points go to the crew's bank.  Every crew member is always the same level, meaning that any time a crewmember gains experience, that experience goes to all of the players.

Research points are spent like normal.

### Q:  How do I access these features?

The system is accessed using chat commands.  you can bring up a chat terminal by pressing "."  Some example commands are:

`\crew balance`    - Shows your personal point balance and the balance of your crew
`\crew withdraw 3` - Withdraws 3 credits from your crew's account and adds them to your personal account
`\crew convert 10` - Converts 10 upgrade points to credits at the predetermined exchange rate (default is 10 to 1)
`\crew help`       - Brings up a help dialog that describes the actions that can be taken.

### Q:  How do I add this to my game?

Just copy the `CPM` folder from the `bin/Release` folder of this repo into the `Content/Mods` folder of your Empyrion Dedicated Server installation.  You'll have to restart the server for the mod to take effect.

### Q:  What will happen if I add it to an existing game?

Probably nothing.  Everyone's point balances should remain the same, and you will just transition to the new system.

### Q:  What do you mean, "probably" isn't this thoroughly tested?

Oh no. No no no no.  God no.  It is not.  Most of this code was written stream of thought while reacquainting myself with C# and exploring the Empyrion API.  It is very much, "At your own risk" software for the moment.

### Q:  What can I do if it screws everything up?

Just remove it, it should have no long term effects on save games.  You'll lose the point balances in the crew accounts though.

### Q:  Is this configurable?

Yup, all of the settings are available in the mod folder as `settings.json`.  The default settings are there just for reference.

The configuration works as follows:

```javascript
{
	"actions": [
		{
			"ActionName": "Normalize Players", 
			"interval": 25  // How many ticks between updating the players point and experience counts
		},
		{
			"ActionName": "Update Points",
			"interval": 6000 // How many ticks until a crew receives research points
		},
		{
			"ActionName": "Evaluate Research Teams",
			"interval": 50 // How often the system searches for the largest small cooperative in a crew
		},
		{
			"ActionName": "Save Accounts",
			"interval": 400 // How often the accounts of the players and crews are saved to disk (in case the server crashes)
		}
	],
	"verbose": false, // whether or not the logs are filled with debugging information
	"checkinProbability": 0.05, // This is a gate on cpu consumption.  the higher this is, the more even the intervals are, but the higher the consumption.
	"creditExchangeRate": 10.0, //  How many credits you receive per point when converting
	"startingUpgradePointBalance": 30 // The minimum number of research points that a player's account starts with
}
```

### Q:  If I restart, will I lose my accounts?

Nope, this saves all of your account information periodically and reloads it when the server comes back up.

### Q:  What are the dependencies?

The deps folder contains the binaries of projects that the mod relies on.  They're bundled into a single dll using the MSBuild.ILMerge Task available at https://www.nuget.org/packages/MSBuild.ILMerge.Task/

The dependencies are:
- DeJson.NET - https://github.com/greggman/DeJson.NET :  for simple json serialization/deserialization that I didn't have to write myself
- Empyrion Chat Command Manager - https://github.com/lostinplace/EmpyrionChatCommandManager : A library I wrote for several chat-based UIs I use in mods
- Empyrion API Message Broker - https://github.com/lostinplace/EmpyrionAPIMessageBroker : A library I wrote to simplify request management with the Empyrion API

### Q: How do you feel about pull requests?

My fondest dreams are of pull requests. If you want to contribute, awesome. Just submit the PR in an issue so I can track changes.
