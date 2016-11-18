using System;
using System.Collections.Generic;
using System.Diagnostics;
using LiveSplit.ComponentUtil;

namespace LiveSplit.UnrealLoads.Games
{
	class SplinterCell3 : GameSupport
	{
		public override HashSet<string> GameNames { get; } = new HashSet<string>
		{
			"Splinter Cell 3",
			"Splinter Cell: Chaos Theory",
			"Splinter Cell Chaos Theory"
		};

		public override HashSet<string> ProcessNames { get; } = new HashSet<string>
		{
			"splintercell3"
		};

		public override HashSet<string> Maps { get; } = new HashSet<string>
		{
			"01_panama",
			"02_cargoship",
			"02_seoulthree",
			"03_bank",
			"03_chembunker",
			"04_gcs",
			"04_penthouse",
			"05_displace01",
			"05_nuclearplant",
			"06_hokkaido",
			"07_battery",
			"07_unhq",
			"08_seoulone",
			"09_seoultwo",
			"10_bathhouse",
			"11_kokubososho"
		};

		public override bool? IsLoading(MemoryWatcherList watchers) => false; //disable load removal

		public override Type LoadMapDetourT => typeof(LoadMapDetour_SplinterCell3);

		public override Type SaveGameDetourT => typeof(SaveGameDetour_SplinterCell3);
	}

	class LoadMapDetour_SplinterCell3 : LoadMapDetour
	{
		public new static string Symbol => "?SEC_LoadMap@UGameEngine@@QAEXABVFURL@@AAVFString@@@Z";
		public new static string Module => null;

		public LoadMapDetour_SplinterCell3(IntPtr setMapAddr, IntPtr statusAddr)
			: base(setMapAddr, statusAddr)
		{
			_overwrittenBytes = 6;
		}

		public override byte[] GetBytes()
		{
			var status = _statusPtr.ToBytes().ToHex();
			var none = Status.None.ToBytes().ToHex();
			var loadingMap = Status.LoadingMap.ToBytes().ToHex();

			var str = string.Join("\n",
				"55",                              // push ebp
				"8B EC",                           // mov ebp,esp
				"83 EC 0C",                        // sub esp,C
				"89 55 F4",                        // mov dword ptr ds:[ebp-C],edx
				"89 4D F8",                        // mov dword ptr ds:[ebp-8],ecx
				"8B 45 08",                        // mov eax,dword ptr ds:[ebp+8]
				"8B 48 1C",                        // mov ecx,dword ptr ds:[eax+1C]
				"89 4D FC",                        // mov dword ptr ds:[ebp-4],ecx
				"8B 55 FC",                        // mov edx,dword ptr ds:[ebp-4]
				"52",                              // push edx
				"#FF FF FF FF FF",                 // call <hooks.?set_map@@YAXPB_W@Z>
				"83 C4 04",                        // add esp,4
				"C7 05 " + status + loadingMap,    // mov dword ptr ds:[<?g_status@@3HA>],1
				"8B 45 0C",                        // mov eax,dword ptr ds:[ebp+C]
				"50",                              // push eax
				"8B 4D 08",                        // mov ecx,dword ptr ds:[ebp+8]
				"51",                              // push ecx
				"8B 4D F8",                        // mov ecx,dword ptr ds:[ebp-8]
				"#FF FF FF FF FF",                 // call dword ptr ds:[34378C]
				"C7 05 " + status + none,          // mov dword ptr ds:[<?g_status@@3HA>],0
				"8B E5",                           // mov esp,ebp
				"5D",                              // pop ebp
				"C2 08 00"                         // ret 8
			);

			int[] offsets;
			var bytes = Utils.ParseBytes(str, out offsets);
			_setMapCallOffset = offsets[0];
			_originalFuncCallOffset = offsets[1];

			return bytes.ToArray();
		}
	}

	class SaveGameDetour_SplinterCell3 : SaveGameDetour
	{
		public new static string Symbol => "?SEC_SaveGame@UGameEngine@@QAEXPAVALevelInfo@@PBG@Z";
		public new static string Module => null;

		public SaveGameDetour_SplinterCell3(IntPtr statusAddr)
			: base(statusAddr)
		{ }

		public override byte[] GetBytes()
		{
			var status = _statusPtr.ToBytes().ToHex();
			var none = Status.None.ToBytes().ToHex();
			var saving = Status.Saving.ToBytes().ToHex();

			var str = string.Join("\n",
				"55",                           // push ebp
				"8B EC",                        // mov ebp,esp
				"83 EC 08",                     // sub esp,8
				"89 55 F8",                     // mov dword ptr ds:[ebp-8],edx
				"89 4D FC",                     // mov dword ptr ds:[ebp-4],ecx
				"C7 05 " + status + saving,     // mov dword ptr ds:[<?g_status@@3HA>],2
				"8B 45 0C",                     // mov eax,dword ptr ds:[ebp+C]
				"50",                           // push eax
				"8B 4D 08",                     // mov ecx,dword ptr ds:[ebp+8]
				"51",                           // push ecx
				"8B 4D FC",                     // mov ecx,dword ptr ds:[ebp-4]
				"#FF FF FF FF FF",              // call dword ptr ds:[13F3798]
				"C7 05 " + status + none,       // mov dword ptr ds:[<?g_status@@3HA>],0
				"8B E5",                        // mov esp,ebp
				"5D",                           // pop ebp
				"C2 08 00"                      // ret 8
			);

			int[] offsets;
			var bytes = Utils.ParseBytes(str, out offsets);
			_originalFuncCallOffset = offsets[0];
			return bytes.ToArray();
		}
	}
}
