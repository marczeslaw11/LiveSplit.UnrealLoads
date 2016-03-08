using LiveSplit.ComponentUtil;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace LiveSplit.UnrealLoads.GameSupport
{
	class HarryPotter2 : IGameSupport
	{
		public HashSet<string> GameNames { get; } = new HashSet<string>
		{
			"Harry Potter 2",
			"Harry Potter II",
			"Harry Potter and the Chamber of Secrets",
			"HP2",
			"HP 2",
			"HP II"
		};

		public HashSet<string> ProcessNames { get; } = new HashSet<string>
		{
			"game"
		};

		public HashSet<string> Maps { get; }

		MemoryWatcher<bool> _isSkippingCut = new MemoryWatcher<bool>(new DeepPointer("Engine.dll", 0x2E2DFC, 0x5C));
		readonly HashSet<int> _moduleMemorySizes = new HashSet<int>
		{
			704512,
			674234 //no-cd
		};

		public HarryPotter2() { }

		public IdentificationResult IdentifyProcess(Process process)
		{
			if (_moduleMemorySizes.Contains(process.MainModuleWow64Safe().ModuleMemorySize)
				&& GetCommandLine(process).Contains("-SAVESLOT="))
				return IdentificationResult.Success;

			return IdentificationResult.Failure;
		}

		public TimerAction[] OnUpdate(Process game, MemoryWatcherList watchers)
		{
			_isSkippingCut.Update(game);
			if (_isSkippingCut.Changed && _isSkippingCut.Current)
				return new TimerAction[] { TimerAction.Start };

			return null;
		}

		public bool? IsLoading(MemoryWatcherList watchers)
		{
			if (_isSkippingCut.Current)
				return true;

			return null;
		}

		public TimerAction[] OnMapLoad(StringWatcher map)
		{
			//reset only if it is the first map loaded
			if (string.IsNullOrEmpty(map.Old) && map.Current.ToLower() == "privetdr.unr")
				return new TimerAction[] { TimerAction.Reset };

			return null;
		}

		static string GetCommandLine(Process process)
		{
			var commandLine = new StringBuilder();
			using (var searcher = new System.Management.ManagementObjectSearcher(
				"SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id))
			{
				foreach (var @object in searcher.Get())
					commandLine.Append(@object["CommandLine"] + " ");
			}

			return commandLine.ToString();
		}

		public TimerAction[] OnAttach(Process game) => null;
		public TimerAction[] OnDetach(Process game) => null;
	}
}
