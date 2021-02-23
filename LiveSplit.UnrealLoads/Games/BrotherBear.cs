using LiveSplit.ComponentUtil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace LiveSplit.UnrealLoads.Games
{
	class BrotherBear : GameSupport
	{
		public override HashSet<string> GameNames => new HashSet<string>
		{
			"Disney's Brother Bear",
			"Brother Bear",
			"Mój Brat Niedźwiedź"
		};

		public override HashSet<string> ProcessNames => new HashSet<string>
		{
			"game"
		};

		public override HashSet<string> Maps => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"ASP_Combat1",
			"AspenForest",
			"AvalancheRun",
			"CavePainting1",
			"CavePainting2",
			"CavePainting3",
			"Final_Battle",
			"IceRun",
			"SAL_Combat1",
			"SAL_Combat2",
			"Salmon_run",
			"SalmonForest",
			"SecretTotemCave",
			"ValleyOfFire",
			"VOF_Combat1",
			"VOF_Combat2"
		};

		private readonly MemoryWatcher<bool> _isSkippingCut = new MemoryWatcher<bool>(new DeepPointer("Engine.dll", 0x1D9308, 0x5C));
		//private readonly HashSet<int> _moduleMemorySizes = new HashSet<int>
		//{
		//	704512,
		//	749568, // US
		//	674234 //no-cd
		//};

		//public override IdentificationResult IdentifyProcess(Process process)
		//{
		//	if (_moduleMemorySizes.Contains(process.MainModuleWow64Safe().ModuleMemorySize)
		//		&& GetCommandLine(process).Contains("-SAVESLOT="))
		//		return IdentificationResult.Success;
		//
		//	return IdentificationResult.Failure;
		//}

		public override TimerAction[] OnUpdate(Process game, MemoryWatcherList watchers)
		{
			_isSkippingCut.Update(game);
			var map = (StringWatcher)watchers["map"];

			if (_isSkippingCut.Changed && _isSkippingCut.Current
				&& (string.IsNullOrEmpty(map.Old) || map.Current.Equals("CavePainting1.unr", StringComparison.OrdinalIgnoreCase)))
			{
				return new TimerAction[] { TimerAction.Start };
			}

			return null;
		}

		public override bool? IsLoading(MemoryWatcherList watchers)
		{
			if (_isSkippingCut.Current)
				return true;
		
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

		public override TimerAction[] OnDetach(Process game)
		{
			return new TimerAction[] { TimerAction.UnpauseGameTime };
		}
	}
}