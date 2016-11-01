using LiveSplit.ComponentUtil;
using System.Collections.Generic;
using System.Diagnostics;

namespace LiveSplit.UnrealLoads.GameSupport
{
	class Shrek2 : IGameSupport
	{
		public HashSet<string> GameNames { get; } = new HashSet<string>
		{
			"Shrek 2"
		};

		public HashSet<string> ProcessNames { get; } = new HashSet<string>
		{
			"game"
		};

		public HashSet<string> Maps { get; }

		public IdentificationResult IdentifyProcess(Process process)
		{
			return process.MainModuleWow64Safe().ModuleMemorySize == 438272
				? IdentificationResult.Success
				: IdentificationResult.Failure;
		}

		public TimerAction[] OnMapLoad(MemoryWatcherList watchers)
		{
			var map = (StringWatcher)watchers["map"];
			if (map.Current.ToLower() == "book_story_1.unr")
				return new TimerAction[] { TimerAction.Reset, TimerAction.Start };

			return null;
		}

		public TimerAction[] OnUpdate(Process game, MemoryWatcherList watchers) => null;
		public bool? IsLoading(MemoryWatcherList watchers) => null;
		public TimerAction[] OnAttach(Process game) => null;
		public TimerAction[] OnDetach(Process game) => null;
	}
}
