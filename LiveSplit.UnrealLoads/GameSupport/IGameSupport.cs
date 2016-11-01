using LiveSplit.ComponentUtil;
using System.Collections.Generic;
using System.Diagnostics;

namespace LiveSplit.UnrealLoads.GameSupport
{
	public enum IdentificationResult
	{
		Success,
		Failure,
		Undecisive
	}

	public enum TimerAction
	{
		DoNothing,
		Start,
		Reset,
		Split,
		PauseGameTime,
		UnpauseGameTime
	}

	public interface IGameSupport
	{
		HashSet<string>			GameNames { get; }
		HashSet<string>			ProcessNames { get; }
		HashSet<string>			Maps { get; }
        IdentificationResult	IdentifyProcess(Process process);
		bool?					IsLoading(MemoryWatcherList watchers);
		TimerAction[]			OnAttach(Process game);
		TimerAction[]			OnDetach(Process game);
		TimerAction[]			OnUpdate(Process game, MemoryWatcherList watchers);
		TimerAction[]			OnMapLoad(MemoryWatcherList watchers);
	}
}
