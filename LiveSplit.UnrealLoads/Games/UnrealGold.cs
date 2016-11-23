using LiveSplit.ComponentUtil;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System;

namespace LiveSplit.UnrealLoads.Games
{
	class UnrealGold : GameSupport
	{
		public override HashSet<string> GameNames => new HashSet<string>
		{
			"Unreal",
			"Unreal Gold",
			"Unreal: Return to Na Pali",
			"Unreal Return to Na Pali",
			"Return to Na Pali"
		};

		public override HashSet<string> ProcessNames => new HashSet<string>
		{
			"unreal"
		};

		public override HashSet<string> Maps => new HashSet<string>
		{
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
			"nalic2",
			"inter14"
		};

		public override Type LoadMapDetourT => typeof(LoadMapDetour_oldUnreal);

		public override Type SaveGameDetourT => typeof(SaveGameDetour_oldUnreal);

		public override TimerAction[] OnUpdate(Process game, MemoryWatcherList watchers)
		{
			var status = (MemoryWatcher<int>)watchers["status"];
			if (status.Changed)
			{
				var mapW = (StringWatcher)watchers["map"];
				var map = Path.GetFileNameWithoutExtension(mapW.Current).ToLower();

				if (status.Old == (int)Status.LoadingMap)
				{
					if (map == "vortex2" || map == "duskfalls")
						return new TimerAction[] { TimerAction.Start };
				}
				else if (status.Current == (int)Status.LoadingMap)
				{
					if (map == "vortex2")
						return new TimerAction[] { TimerAction.Reset };
				}
			}
			return null;
		}
	}

	public class LoadMapDetour_oldUnreal : LoadMapDetour
	{
		public new static string Symbol => "?LoadMap@UGameEngine@@UAEPAVULevel@@ABVFURL@@PAVUPendingLevel@@PBV?$TMap@VFString@@V1@@@AAVFString@@PAVUTravelDataManager@@@Z";

		public LoadMapDetour_oldUnreal(IntPtr setMapAddr, IntPtr statusAddr)
			: base(setMapAddr, statusAddr)
		{
			_overwrittenBytes = 8;
		}

		public override byte[] GetBytes()
		{
			var status = _statusPtr.ToBytes().ToHex();
			var none = Status.None.ToBytes().ToHex();
			var loadingMap = Status.LoadingMap.ToBytes().ToHex();

			var str = string.Join("\n",
				"55",                           // push ebp
				"8B EC",                        // mov ebp,esp
				"83 EC 10",                     // sub esp,10
				"89 55 F0",                     // mov dword ptr ds:[ebp-10],edx
				"89 4D F8",                     // mov dword ptr ds:[ebp-8],ecx
				"8B 45 08",                     // mov eax,dword ptr ds:[ebp+8]
				"8B 48 1C",                     // mov ecx,dword ptr ds:[eax+1C]
				"89 4D FC",                     // mov dword ptr ds:[ebp-4],ecx
				"8B 55 FC",                     // mov edx,dword ptr ds:[ebp-4]
				"52",                           // push edx
				"#FF FF FF FF FF",              // call set_map
				"83 C4 04",                     // add esp,4
				"C7 05 " + status + loadingMap, // mov dword ptr ds:[<?g_status@@3HA>],1
				"8B 45 18",                     // mov eax,dword ptr ds:[ebp+18]
				"50",                           // push eax
				"8B 4D 14",                     // mov ecx,dword ptr ds:[ebp+14]
				"51",                           // push ecx
				"8B 55 10",                     // mov edx,dword ptr ds:[ebp+10]
				"52",                           // push edx
				"8B 45 0C",                     // mov eax,dword ptr ds:[ebp+C]
				"50",                           // push eax
				"8B 4D 08",                     // mov ecx,dword ptr ds:[ebp+8]
				"51",                           // push ecx
				"8B 4D F8",                     // mov ecx,dword ptr ds:[ebp-8]
				"#FF FF FF FF FF",              // call dword ptr ds:[B3784]
				"89 45 F4",                     // mov dword ptr ds:[ebp-C],eax
				"C7 05 " + status + none,       // mov dword ptr ds:[<?g_status@@3HA>],0
				"8B 45 F4",                     // mov eax,dword ptr ds:[ebp-C]
				"8B E5",                        // mov esp,ebp
				"5D",                           // pop ebp
				"C2 14 00"                      // ret 14
			);

			int[] offsets;
			var bytes = Utils.ParseBytes(str, out offsets);
			_setMapCallOffset = offsets[0];
			_originalFuncCallOffset = offsets[1];

			return bytes.ToArray();
		}
	}

	public class SaveGameDetour_oldUnreal : SaveGameDetour
	{
		public SaveGameDetour_oldUnreal(IntPtr statusAddr)
			: base(statusAddr)
		{
			_overwrittenBytes = 8;
		}
	}
}
