using LiveSplit.ComponentUtil;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System;

namespace LiveSplit.UnrealLoads.Games
{
	class KlingonHonorGuard : GameSupport
	{
		public override HashSet<string> GameNames => new HashSet<string>
		{
			"Star Trek: Klingon Honor Guard",
			"Star Trek Klingon Honor Guard",
			"Klingon Honor Guard",
		};

		public override HashSet<string> ProcessNames => new HashSet<string>
		{
			"khg"
		};

		public override HashSet<string> Maps => new HashSet<string>
		{
			"dr01",
			"dr02",
			"dr03",
			"dr04",
			"dr05",
			"dr06",
			"dr07",
			"dr08",
			"dr09",
			"dr10",
			"dr11",
			"dr12",
			"klingon",
			"m03",
			"m04a",
			"m04b",
			"m05",
			"m06a",
			"m06b",
			"m07a",
			"m07b",
			"m08",
			"m09a",
			"m09b",
			"m10",
			"m11",
			"m12",
			"m13",
			"m14",
			"m15",
			"m16",
			"m17",
			"m18a",
			"m18b",
			"m18c",
			"m19",
			"m20a",
			"m20b",
			"m20c"
		};

		StringWatcher _map;

		public override LoadMapDetour GetNewLoadMapDetour() => new LoadMapDetour_KlingonHonorGuard();

		public override SaveGameDetour GetNewSaveGameDetour() => new SaveGameDetour_KlingonHonorGuard();

		public override TimerAction[] OnUpdate(Process game, MemoryWatcherList watchers)
		{
			var status = (MemoryWatcher<int>)watchers["status"];
			_map = (StringWatcher)watchers["map"];

			if(status.Changed)
			{
				var map = Path.GetFileNameWithoutExtension(_map.Current).ToLower();

				if(status.Old == (int)Status.LoadingMap)
				{
					if(map == "m02")
						return new TimerAction[] { TimerAction.Start };
				}
				else if(status.Current == (int)Status.LoadingMap)
				{
					if(map == "m02")
						return new TimerAction[] { TimerAction.Reset };
				}
			}
			return null;
		}

		public class LoadMapDetour_KlingonHonorGuard : LoadMapDetour
		{
			public override string Symbol => "?LoadMap@UGameEngine@@UAEPAVULevel@@ABVFURL@@PAVUPendingLevel@@PAD@Z";
			public override string Module => "Engine.dll";
			public override StringType Encoding => StringType.ASCII;

			protected override int OverwrittenBytes => 10;


			public override byte[] GetBytes()
			{
				var status = StatusPtr.ToBytes().ToHex();
				var none = Status.None.ToBytes().ToHex();
				var loadingMap = Status.LoadingMap.ToBytes().ToHex();

				var str = string.Join("\n",
					"55",
					"8B EC",
					"83 EC 10",
					"89 55 F0",
					"89 4D F8",
					"8B 45 08",
					"8B 48 1C",
					"89 4D FC",
					"8B 55 FC",
					"52",
					"#FF FF FF FF FF",
					"83 C4 04",
					"C7 05 " + status + loadingMap,
					"8B 45 10",
					"50",
					"8B 4D 0C",
					"51",
					"8B 55 08",
					"52",
					"8B 4D F8",
					"#FF FF FF FF FF",
					"89 45 F4",
					"C7 05 " + status + none,
					"8B 45 F4",
					"8B E5",
					"5D",
					"C2 0C 00"
				);

				int[] offsets;
				var bytes = Utils.ParseBytes(str, out offsets);
				_setMapCallOffset = offsets[0];
				_originalFuncCallOffset = offsets[1];

				return bytes.ToArray();
			}
		}

		public class SaveGameDetour_KlingonHonorGuard : SaveGameDetour
		{
			public override string Symbol => "?SaveGame@UGameEngine@@UAEXH@Z";
			public override string Module => "Engine.dll";
		}
	}
}
