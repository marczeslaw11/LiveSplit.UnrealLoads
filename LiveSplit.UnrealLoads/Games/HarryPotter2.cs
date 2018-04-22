using LiveSplit.ComponentUtil;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace LiveSplit.UnrealLoads.Games
{
	class HarryPotter2 : GameSupport
	{
		public override HashSet<string> GameNames => new HashSet<string>
		{
			"Harry Potter 2",
			"Harry Potter II",
			"Harry Potter and the Chamber of Secrets",
			"HP2",
			"HP 2",
			"HP II"
		};

		public override HashSet<string> ProcessNames => new HashSet<string>
		{
			"game"
		};

		public override HashSet<string> Maps => new HashSet<string>
		{
			"grounds_night",
			"entryhall_hub",
			"grandstaircase_hub",
			"ch1rictusempra",
			"beanrewardroom",
			"grounds_hub",
			"quidditch_intro",
			"ch2skurge",
			"adv3dungeonquest",
			"arena",
			"ch3diffindo",
			"adv4greenhouse",
			"adv6goyle",
			"quidditch",
			"adv7slythcomroom",
			"ch4spongify",
			"sepia_hallway",
			"adv8forest",
			"adv9aragog",
			"adv11acorridor",
			"adv11bsecrets",
			"adv12chamber",
			"greathall_g",
			"ch6wizardcard"
		};

		MemoryWatcher<bool> _isSkippingCut = new MemoryWatcher<bool>(new DeepPointer("Engine.dll", 0x2E2DFC, 0x5C));
		readonly HashSet<int> _moduleMemorySizes = new HashSet<int>
		{
			704512,
			749568, // US
			674234 //no-cd
		};

		public override IdentificationResult IdentifyProcess(Process process)
		{
			if (_moduleMemorySizes.Contains(process.MainModuleWow64Safe().ModuleMemorySize)
				&& GetCommandLine(process).Contains("-SAVESLOT="))
				return IdentificationResult.Success;

			return IdentificationResult.Failure;
		}

		public override TimerAction[] OnUpdate(Process game, MemoryWatcherList watchers)
		{
			_isSkippingCut.Update(game);
			if (_isSkippingCut.Changed && _isSkippingCut.Current)
				return new TimerAction[] { TimerAction.Start };

			return null;
		}

		public override bool? IsLoading(MemoryWatcherList watchers)
		{
			if (_isSkippingCut.Current)
				return true;

			return null;
		}

		public override TimerAction[] OnMapLoad(MemoryWatcherList watchers)
		{
			var map = (StringWatcher)watchers["map"];
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
	}
}
