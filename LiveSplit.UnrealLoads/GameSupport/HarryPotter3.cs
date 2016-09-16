using LiveSplit.ComponentUtil;
using System.Collections.Generic;
using System.Diagnostics;

namespace LiveSplit.UnrealLoads.GameSupport
{
	class HarryPotter3 : IGameSupport
	{
		public HashSet<string> GameNames { get; } = new HashSet<string>
		{
			"Harry Potter 3",
			"Harry Potter and the Prisoner of Azkaban",
			"HP3",
			"HP 3"
		};

		public HashSet<string> ProcessNames { get; } = new HashSet<string>
		{
			"hppoa"
		};

		public HashSet<string> Maps { get; }

		public TimerAction[] OnMapLoad(StringWatcher map) => null;
		public TimerAction[] OnUpdate(Process game, MemoryWatcherList watchers) => null;
		public IdentificationResult IdentifyProcess(Process process) => IdentificationResult.Success;
		public bool? IsLoading(MemoryWatcherList watchers) => null;
		public TimerAction[] OnAttach(Process game) => null;
		public TimerAction[] OnDetach(Process game) => null;
	}
}

