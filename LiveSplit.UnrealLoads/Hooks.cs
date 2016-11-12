using LiveSplit.ComponentUtil;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LiveSplit.UnrealLoads.Hooks
{
	public abstract class Detour
	{
		public byte[] Bytes => _bytes.ToArray();
		public IntPtr Pointer { get; }
		public IntPtr InjectedFuncPtr { get; protected set; }
		public IntPtr DetouredFuncPtr { get; protected set; }

		protected List<byte> _bytes;
		protected int _originalFuncCallOffset = -1;
		protected bool _usesTrampoline;
		protected int _overwrittenBytes;

		public Detour(Process process, IntPtr funcToDetour)
		{
			Pointer = funcToDetour;
			DetouredFuncPtr = process.ReadJMP(funcToDetour);
			_usesTrampoline = DetouredFuncPtr == IntPtr.Zero;
			_overwrittenBytes = 5;
		}

		public virtual void Install(Process process)
		{
			Debug.WriteLine($"[NoLoads] Hooking using {(_usesTrampoline ? "trampoline" : "thunk")}...");

			if (InjectedFuncPtr == IntPtr.Zero)
				InjectedFuncPtr = process.AllocateMemory(Bytes.Length);

			if (_usesTrampoline)
			{
				DetouredFuncPtr = process.WriteDetour(Pointer, _overwrittenBytes, InjectedFuncPtr);
			}
			else
			{
				// in case of thunks just replace them
				process.WriteJumpInstruction(Pointer, InjectedFuncPtr);
			}

			process.WriteBytes(InjectedFuncPtr, Bytes);
			if (_originalFuncCallOffset >= 0)
				process.WriteCallInstruction(InjectedFuncPtr + _originalFuncCallOffset, DetouredFuncPtr);
		}

		public virtual void Uninstall(Process process)
		{
			if (_usesTrampoline)
			{
				if (DetouredFuncPtr == IntPtr.Zero)
					throw new InvalidOperationException("Not installed");
				process.CopyMemory(DetouredFuncPtr, Pointer, _overwrittenBytes);
			}
			else
			{
				process.WriteJumpInstruction(Pointer, DetouredFuncPtr);
			}
		}

		public virtual void FreeMemory(Process process)
		{
			if (process == null || process.HasExited)
				return;

			process.FreeMemory(InjectedFuncPtr);
			InjectedFuncPtr = IntPtr.Zero;
			if (_usesTrampoline)
			{
				process.FreeMemory(DetouredFuncPtr);
				DetouredFuncPtr = IntPtr.Zero;
			}
		}
	}

	public class SetMapFunction
	{
		public byte[] Bytes => _bytes.ToArray();
		public IntPtr InjectedFuncPtr { get; private set; }

		List<byte> _bytes;

		public IntPtr Inject(Process process)
		{
			InjectedFuncPtr = process.AllocateMemory(Bytes.Length);
			process.WriteBytes(InjectedFuncPtr, Bytes);
			return InjectedFuncPtr;
		}

		public void FreeMemory(Process process)
		{
			if (process == null || process.HasExited)
				return;

			process.FreeMemory(InjectedFuncPtr);
			InjectedFuncPtr = IntPtr.Zero;
		}

		public SetMapFunction(IntPtr mapAddr)
		{
			var map = mapAddr.ToBytes().ToHex();

			var str = string.Join("\n",
				"55",					// push ebp
				"8B EC",				// mov ebp,esp
				"83 EC 08",				// sub esp,8
				"C7 45 FC 0 0 0 0",		// mov dword ptr ds:[ebp-4],0
				"EB 09",				// jmp hooks.A1018
				"8B 45 FC",				// mov eax,dword ptr ds:[ebp-4]
				"83 C0 01",				// add eax,1
				"89 45 FC",				// mov dword ptr ds:[ebp-4],eax
				"81 7D FC 4 1 0 0",		// cmp dword ptr ds:[ebp-4],104
				"7D 27",				// jge hooks.A1048
				"8B 4D FC",				// mov ecx,dword ptr ds:[ebp-4]
				"8B 55 FC",				// mov edx,dword ptr ds:[ebp-4]
				"8B 45 08",				// mov eax,dword ptr ds:[ebp+8]
				"66 8B 14 50",			// mov dx,word ptr ds:[eax+edx*2]
				"66 89 14 4D " + map,	// mov word ptr ds:[ecx*2+<?g_map@@3PA_WA>
				"8B 45 FC",				// mov eax,dword ptr ds:[ebp-4]
				"8B 4D 08",				// mov ecx,dword ptr ds:[ebp+8]
				"0F B7 14 41",			// movzx edx,word ptr ds:[ecx+eax*2]
				"85 D2",				// test edx,edx
				"75 02",				// jne hooks.A1046
				"EB 02",				// jmp hooks.A1048
				"EB C7",				// jmp hooks.A100F
				"B8 2 0 0 0",			// mov eax,2
				"69 C8 3 1 0 0",		// imul ecx,eax,103
				"89 4D F8",				// mov dword ptr ds:[ebp-8],ecx
				"81 7D F8 8 2 0 0",		// cmp dword ptr ds:[ebp-8],208
				"73 02",				// jae hooks.A1061
				"EB 05",				// jmp hooks.A1066
				"E8 44 02 00 00",		// call hooks.A12AA
				"33 D2",				// xor edx,edx
				"8B 45 F8",				// mov eax,dword ptr ds:[ebp-8]
				"66 89 90" + map,		// mov word ptr ds:[eax+<?g_map@@3PA_WA>],
				"8B E5",				// mov esp,ebp
				"5D",					// pop ebp
				"C3"					// ret
			);

			_bytes = Utils.ParseBytes(str);
		}
	}

	public class LoadMapDetour : Detour
	{
		public const string SYMBOL = "?LoadMap@UGameEngine@@UAEPAVULevel@@ABVFURL@@PAVUPendingLevel@@PBV?$TMap@VFString@@V1@@@AAVFString@@@Z";

		IntPtr _setMapPtr;
		int _setMapCallOffset;

		public override void Install(Process process)
		{
			base.Install(process);
			process.WriteCallInstruction(InjectedFuncPtr + _setMapCallOffset, _setMapPtr);
		}

		public LoadMapDetour(Process process, IntPtr functionToDetour, IntPtr setMapAddr, IntPtr statusAddr)
			: base(process, functionToDetour)
		{
			_setMapPtr = setMapAddr;
			var status = statusAddr.ToBytes().ToHex();

			var str = string.Join("\n",
				"55",							// push ebp
				"8B EC",						// mov ebp,esp
				"83 EC 10",						// sub esp,10
				"89 55 F0",						// mov dword ptr ds:[ebp-10],edx
				"89 4D F8",						// mov dword ptr ds:[ebp-8],ecx
				"8B 45 08",						// mov eax,dword ptr ds:[ebp+8]
				"8B 48 1C",						// mov ecx,dword ptr ds:[eax+1C]
				"89 4D FC",						// mov dword ptr ds:[ebp-4],ecx
				"8B 55 FC",						// mov edx,dword ptr ds:[ebp-4]
				"52",							// push edx
				"#FF FF FF FF FF",				// call set_map
				"83 C4 04",						// add esp,4
				"C7 05 " + status + " 1 0 0 0",	// mov dword ptr ds:[<?g_status@@3HA>],1
				"8B 45 14",						// mov eax,dword ptr ds:[ebp+14]
				"50",							// push eax
				"8B 4D 10",						// mov ecx,dword ptr ds:[ebp+10]
				"51",							// push ecx
				"8B 55 0C",						// mov edx,dword ptr ds:[ebp+C]
				"52",							// push edx
				"8B 45 08",						// mov eax,dword ptr ds:[ebp+8]
				"50",							// push eax
				"8B 4D F8",						// mov ecx,dword ptr ds:[ebp-8]
				"#FF FF FF FF FF",				// call dword ptr ds:[B3780]
				"89 45 F4",						// mov dword ptr ds:[ebp-C],eax
				"C7 05 " + status + " 0 0 0 0",	// mov dword ptr ds:[<?g_status@@3HA>],0
				"8B 45 F4",						// mov eax,dword ptr ds:[ebp-C]
				"8B E5",						// mov esp,ebp
				"5D",							// pop ebp
				"C2 10 00"						// ret 10
			);

			int[] offsets;
			_bytes = Utils.ParseBytes(str, out offsets);
			_setMapCallOffset = offsets[0];
			_originalFuncCallOffset = offsets[1];
		}
	}

	public class LoadMapDetour_oldUnreal : Detour
	{
		public const string SYMBOL = "?LoadMap@UGameEngine@@UAEPAVULevel@@ABVFURL@@PAVUPendingLevel@@PBV?$TMap@VFString@@V1@@@AAVFString@@PAVUTravelDataManager@@@Z";

		int _setMapCallOffset;
		IntPtr _setMapPtr;

		public override void Install(Process process)
		{
			base.Install(process);
			process.WriteCallInstruction(InjectedFuncPtr + _setMapCallOffset, _setMapPtr);
		}

		public LoadMapDetour_oldUnreal(Process process, IntPtr funcToDetour, IntPtr setMapAddr, IntPtr statusAddr)
			: base(process, funcToDetour)
		{
			_overwrittenBytes = 8;
			_setMapPtr = setMapAddr;
			var status = statusAddr.ToBytes().ToHex();

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
				"52",							// push edx
				"#FF FF FF FF FF",				// call set_map
				"83 C4 04",						// add esp,4
				"C7 05 " + status + " 1 0 0 0",	// mov dword ptr ds:[<?g_status@@3HA>],1
				"8B 45 18",						// mov eax,dword ptr ds:[ebp+18]
				"50",							// push eax
				"8B 4D 14",						// mov ecx,dword ptr ds:[ebp+14]
				"51",							// push ecx
				"8B 55 10",						// mov edx,dword ptr ds:[ebp+10]
				"52",							// push edx
				"8B 45 0C",						// mov eax,dword ptr ds:[ebp+C]
				"50",							// push eax
				"8B 4D 08",						// mov ecx,dword ptr ds:[ebp+8]
				"51",							// push ecx
				"8B 4D F8",						// mov ecx,dword ptr ds:[ebp-8]
				"#FF FF FF FF FF",				// call dword ptr ds:[B3784]
				"89 45 F4",						// mov dword ptr ds:[ebp-C],eax
				"C7 05 " + status + " 0 0 0 0",	// mov dword ptr ds:[<?g_status@@3HA>],0
				"8B 45 F4",						// mov eax,dword ptr ds:[ebp-C]
				"8B E5",						// mov esp,ebp
				"5D",							// pop ebp
				"C2 14 00"						// ret 14
			);

			int[] offsets;
			_bytes = Utils.ParseBytes(str, out offsets);
			_setMapCallOffset = offsets[0];
			_originalFuncCallOffset = offsets[1];
		}
	}

	public class SaveGameDetour : Detour
	{
		public const string SYMBOL = "?SaveGame@UGameEngine@@UAEXH@Z";

		public bool OldUnreal
		{
			get	{ return _overwrittenBytes != 5; }
			set	{ _overwrittenBytes = value ? 8 : 5; }
		}

		public SaveGameDetour(Process process, IntPtr funcToDetour, IntPtr statusAddr)
			: base(process, funcToDetour)
		{
			var statusStr = statusAddr.ToBytes().ToHex();

			var str = string.Join("\n",
				"55", 								// PUSH EBP
				"8B EC",							// MOV EBP,ESP
				"83 EC 08",							// SUB ESP,8
				"89 55 F8",							// MOV DWORD PTR SS:[EBP-8],EDX
				"89 4D FC",							// MOV DWORD PTR SS:[EBP-4],ECX
				"C7 05 " + statusStr,				// MOV DWORD PTR DS:[?g_status@@3HA],2
				"02 00 00 00",						// set status to 2
				"8B 45 08",							// MOV EAX,DWORD PTR SS:[EBP+8]
				"50",								// PUSH EAX
				"8B 4D FC",							// MOV ECX,DWORD PTR SS:[EBP-4]
				"#FF FF FF FF FF",					// CALL DWORD PTR DS:[SaveGame] (placeholder)
				"C7 05 " + statusStr + " 0 0 0 0",	// MOV DWORD PTR DS:[?g_status@@3HA],0
				"8B E5",							// MOV ESP,EBP
				"5D",								// POP EBP
				"C2 04 00"							// RETN 4
			);

			int[] offsets;
			_bytes = Utils.ParseBytes(str, out offsets);
			_originalFuncCallOffset = offsets[0];
		}
	}
}
