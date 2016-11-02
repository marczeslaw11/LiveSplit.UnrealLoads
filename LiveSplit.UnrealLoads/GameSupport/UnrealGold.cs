using LiveSplit.ComponentUtil;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace LiveSplit.UnrealLoads.GameSupport
{
	class UnrealGold : IGameSupport
	{
		public HashSet<string> GameNames { get; } = new HashSet<string>
		{
			"Unreal",
			"Unreal Gold",
			"Unreal: Return to Na Pali",
			"Unreal Return to Na Pali",
			"Return to Na Pali"
		};

		public HashSet<string> ProcessNames { get; } = new HashSet<string>
		{
			"unreal"
		};

		public HashSet<string> Maps { get; } = new HashSet<string>
		{
			"vortex2",
			"nyleve",
			"dig",
			"dug",
			"passage",
			"chizra",
			"ceremony",
			"dark",
			"harobed",
			"terralift",
			"terraniux",
			"noork",
			"ruins",
			"trench",
			"isvkran4",
			"isvkran32",
			"isvdeck1",
			"spirevillage",
			"thesunspire",
			"skycaves",
			"skytown",
			"skybase",
			"veloraend",
			"bluff",
			"dasapass",
			"dasacellars",
			"naliboat",
			"nalic",
			"nalilord",
			"dcrater",
			"extremebeg",
			"extremelab",
			"extremecore",
			"extremegen",
			"extremedgen",
			"extremedark",
			"extremeend",
			"queenend",
			"endgame",
			//Return to Na Pali
			"duskfalls",
			"nevec",
			"eldora",
			"glathriel1",
			"glathriel2",
			"crashsite",
			"crashsite1",
			"crashsite2",
			"spireland",
			"nagomi",
			"velora",
			"nagomisun",
			"foundry",
			"toxic",
			"glacena",
			"abyss",
			"nalic2"
		};

		public TimerAction[] OnUpdate(Process game, MemoryWatcherList watchers)
		{
			var status = (MemoryWatcher<int>)watchers["status"];
			if (status.Current == (int)Status.None && status.Old == (int)Status.LoadingMap)
			{
				var mapW = (StringWatcher)watchers["map"];
				var map = Path.GetFileNameWithoutExtension(mapW.Current).ToLower();
				if (map == "vortex2" || map == "duskfalls")
					return new TimerAction[] { TimerAction.Start };
			}
			return null;
		}

		public TimerAction[] OnMapLoad(MemoryWatcherList watchers)
		{
			var mapW = (StringWatcher)watchers["map"];
			var map = Path.GetFileNameWithoutExtension(mapW.Current).ToLower();

			if (map == "vortex2")
				return new TimerAction[] { TimerAction.Reset };

			return null;
		}

		public IdentificationResult IdentifyProcess(Process process) => IdentificationResult.Success;
		public bool? IsLoading(MemoryWatcherList watchers) => null;
		public TimerAction[] OnAttach(Process game) => null;
		public TimerAction[] OnDetach(Process game) => null;
	}
}
