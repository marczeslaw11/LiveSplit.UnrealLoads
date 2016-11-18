using LiveSplit.ComponentUtil;
using System.Collections.Generic;
using System.Diagnostics;

namespace LiveSplit.UnrealLoads.Games
{
	class Shrek2 : GameSupport
	{
		public override HashSet<string> GameNames => new HashSet<string>
		{
			"Shrek 2"
		};

		public override HashSet<string> ProcessNames => new HashSet<string>
		{
			"game"
		};

		public override IdentificationResult IdentifyProcess(Process process)
		{
			return process.MainModuleWow64Safe().ModuleMemorySize == 438272
				? IdentificationResult.Success
				: IdentificationResult.Failure;
		}

		public override TimerAction[] OnMapLoad(MemoryWatcherList watchers)
		{
			var map = (StringWatcher)watchers["map"];
			if (map.Current.ToLower() == "book_story_1.unr")
				return new TimerAction[] { TimerAction.Reset, TimerAction.Start };

			return null;
		}
	}
}
