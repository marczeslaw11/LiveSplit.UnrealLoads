using LiveSplit.ComponentUtil;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LiveSplit.UnrealLoads.Hooks
{
	internal static class Extensions
	{
		public static byte[] ToBytes(this IntPtr ptr) => BitConverter.GetBytes((int)ptr);

		public static IntPtr ReadJMP(this Process process, IntPtr ptr)
		{
			if (process.ReadBytes(ptr, 1)[0] == 0xE9)
				return ptr + 1 + sizeof(int) + process.ReadValue<int>(ptr + 1);
			else
				return IntPtr.Zero;
		}

		public static void CopyMemory(this Process process, IntPtr src, IntPtr dest, int nbr)
		{
			var bytes = process.ReadBytes(src, nbr);
			process.WriteBytes(dest, bytes);
		}
	}

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
			var mapAddrBytes = mapAddr.ToBytes();

			_bytes = new List<byte>()
			{
				0x55,							// push ebp
				0x8B, 0xEC,						// mov ebp,esp
				0x83, 0xEC, 0x08,				// sub esp,8
				0xC7, 0x45, 0xFC, 0, 0, 0, 0,   // mov dword ptr ds:[ebp-4],0
				0xEB, 0x09,						// jmp hooks.A1018
				0x8B, 0x45, 0xFC,				// mov eax,dword ptr ds:[ebp-4]
				0x83, 0xC0, 0x01,				// add eax,1
				0x89, 0x45, 0xFC,				// mov dword ptr ds:[ebp-4],eax
				0x81, 0x7D, 0xFC, 4, 1, 0, 0,	// cmp dword ptr ds:[ebp-4],104
				0x7D, 0x27,						// jge hooks.A1048
				0x8B, 0x4D, 0xFC,				// mov ecx,dword ptr ds:[ebp-4]
				0x8B, 0x55, 0xFC,				// mov edx,dword ptr ds:[ebp-4]
				0x8B, 0x45, 0x08,				// mov eax,dword ptr ds:[ebp+8]
				0x66, 0x8B, 0x14, 0x50,			// mov dx,word ptr ds:[eax+edx*2]
				0x66, 0x89, 0x14, 0x4D			// mov word ptr ds:[ecx*2+<?g_map@@3PA_WA>
			};
			_bytes.AddRange(mapAddrBytes);
			_bytes.AddRange(new byte[]
			{
				0x8B, 0x45, 0xFC,				// mov eax,dword ptr ds:[ebp-4]
				0x8B, 0x4D, 0x08,				// mov ecx,dword ptr ds:[ebp+8]
				0x0F, 0xB7, 0x14, 0x41,			// movzx edx,word ptr ds:[ecx+eax*2]
				0x85, 0xD2,						// test edx,edx
				0x75, 0x02,						// jne hooks.A1046
				0xEB, 0x02,						// jmp hooks.A1048
				0xEB, 0xC7,						// jmp hooks.A100F
				0xB8, 0x02, 0x00, 0x00, 0x00,	// mov eax,2
				0x69, 0xC8, 0x03, 0x01, 0, 0,	// imul ecx,eax,103
				0x89, 0x4D, 0xF8,				// mov dword ptr ds:[ebp-8],ecx
				0x81, 0x7D, 0xF8, 8, 2, 0, 0,	// cmp dword ptr ds:[ebp-8],208
				0x73, 0x02,						// jae hooks.A1061
				0xEB, 0x05,						// jmp hooks.A1066
				0xE8, 0x44, 0x02, 0x00, 0x00,	// call hooks.A12AA
				0x33, 0xD2,						// xor edx,edx
				0x8B, 0x45, 0xF8,				// mov eax,dword ptr ds:[ebp-8]
				0x66, 0x89, 0x90				// mov word ptr ds:[eax+<?g_map@@3PA_WA>],
			});
			_bytes.AddRange(mapAddrBytes);
			_bytes.AddRange(new byte[]
			{
				0x8B, 0xE5,						// mov esp,ebp
				0x5D,							// pop ebp
				0xC3							// ret
			});
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
			var statusAddrBytes = statusAddr.ToBytes();

			_bytes = new List<byte>()
			{
				0x55,							// push ebp
				0x8B, 0xEC,						// mov ebp,esp
				0x83, 0xEC, 0x10,				// sub esp,10
				0x89, 0x55, 0xF0,				// mov dword ptr ds:[ebp-10],edx
				0x89, 0x4D, 0xF8,				// mov dword ptr ds:[ebp-8],ecx
				0x8B, 0x45, 0x08,				// mov eax,dword ptr ds:[ebp+8]
				0x8B, 0x48, 0x1C,				// mov ecx,dword ptr ds:[eax+1C]
				0x89, 0x4D, 0xFC,				// mov dword ptr ds:[ebp-4],ecx
				0x8B, 0x55, 0xFC,				// mov edx,dword ptr ds:[ebp-4]
				0x52,							// push edx
			};
			_setMapCallOffset = _bytes.Count;
			_bytes.AddRange(new byte[]
			{
				255, 255, 255, 255, 255,		// call set_map
				0x83, 0xC4, 0x04,				// add esp,4
				0xC7, 0x05						// mov dword ptr ds:[<?g_status@@3HA>],1
			});
			_bytes.AddRange(statusAddrBytes);
			_bytes.AddRange(new byte[]
			{
				1, 0, 0, 0,						// set status to 1
				0x8B, 0x45, 0x14,				// mov eax,dword ptr ds:[ebp+14]
				0x50,							// push eax
				0x8B, 0x4D, 0x10,				// mov ecx,dword ptr ds:[ebp+10]
				0x51,							// push ecx
				0x8B, 0x55, 0x0C,				// mov edx,dword ptr ds:[ebp+C]
				0x52,							// push edx
				0x8B, 0x45, 0x08,				// mov eax,dword ptr ds:[ebp+8]
				0x50,							// push eax
				0x8B, 0x4D, 0xF8,				// mov ecx,dword ptr ds:[ebp-8]
			});
			_originalFuncCallOffset = _bytes.Count;
			_bytes.AddRange(new byte[]
			{
				255, 255, 255, 255, 255,		// call dword ptr ds:[B3780]
				0x89, 0x45, 0xF4,				// mov dword ptr ds:[ebp-C],eax
				0xC7, 0x05						// mov dword ptr ds:[<?g_status@@3HA>],0
			});
			_bytes.AddRange(statusAddrBytes);
			_bytes.AddRange(new byte[]
			{
				0, 0, 0, 0,						// set status to 0
				0x8B, 0x45, 0xF4,				// mov eax,dword ptr ds:[ebp-C]
				0x8B, 0xE5,						// mov esp,ebp
				0x5D,							// pop ebp
				0xC2, 0x10, 0x00				// ret 10
			});
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
			var statusAddrBytes = statusAddr.ToBytes();

			_bytes = new List<byte>()
			{
				0x55,							// push ebp
				0x8B, 0xEC,						// mov ebp,esp
				0x83, 0xEC, 0x10,				// sub esp,10
				0x89, 0x55, 0xF0,				// mov dword ptr ds:[ebp-10],edx
				0x89, 0x4D, 0xF8,				// mov dword ptr ds:[ebp-8],ecx
				0x8B, 0x45, 0x08,				// mov eax,dword ptr ds:[ebp+8]
				0x8B, 0x48, 0x1C,				// mov ecx,dword ptr ds:[eax+1C]
				0x89, 0x4D, 0xFC,				// mov dword ptr ds:[ebp-4],ecx
				0x8B, 0x55, 0xFC,				// mov edx,dword ptr ds:[ebp-4]
				0x52,							// push edx
			};
			_setMapCallOffset = _bytes.Count;
			_bytes.AddRange(new byte[]
			{
				255, 255, 255, 255, 255,		// call set_map
				0x83, 0xC4, 0x04,				// add esp,4
				0xC7, 0x05						// mov dword ptr ds:[<?g_status@@3HA>],1
			});
			_bytes.AddRange(statusAddrBytes);
			_bytes.AddRange(new byte[]
			{
				1, 0, 0, 0,						// set status to 1
				0x8B, 0x45, 0x18,				// mov eax,dword ptr ds:[ebp+18]
				0x50,							// push eax
				0x8B, 0x4D, 0x14,				// mov ecx,dword ptr ds:[ebp+14]
				0x51,							// push ecx
				0x8B, 0x55, 0x10,				// mov edx,dword ptr ds:[ebp+10]
				0x52,							// push edx
				0x8B, 0x45, 0x0C,				// mov eax,dword ptr ds:[ebp+C]
				0x50,							// push eax
				0x8B, 0x4D, 0x08,				// mov ecx,dword ptr ds:[ebp+8]
				0x51,							// push ecx
				0x8B, 0x4D, 0xF8				// mov ecx,dword ptr ds:[ebp-8]
			});
			_originalFuncCallOffset = _bytes.Count;
			_bytes.AddRange(new byte[]
			{
				255, 255, 255, 255, 255,		// call dword ptr ds:[B3784]
				0x89, 0x45, 0xF4,				// mov dword ptr ds:[ebp-C],eax
				0xC7, 0x05						// mov dword ptr ds:[<?g_status@@3HA>],0
			});
			_bytes.AddRange(statusAddrBytes);
			_bytes.AddRange(new byte[]
			{
				0, 0, 0, 0,
				0x8B, 0x45, 0xF4,				// mov eax,dword ptr ds:[ebp-C]
				0x8B, 0xE5,						// mov esp,ebp
				0x5D,							// pop ebp
				0xC2, 0x14, 0x00				// ret 14
			});
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
			var statusAddrBytes = statusAddr.ToBytes();

			_bytes = new List<byte>() {
				0x55, 							// PUSH EBP
				0x8B, 0xEC,						// MOV EBP,ESP
				0x83, 0xEC, 0x08,				// SUB ESP,8
				0x89, 0x55, 0xF8,				// MOV DWORD PTR SS:[EBP-8],EDX
				0x89, 0x4D, 0xFC,				// MOV DWORD PTR SS:[EBP-4],ECX
				0xC7, 0x05						// MOV DWORD PTR DS:[?g_status@@3HA],2
			};
			_bytes.AddRange(statusAddrBytes);
			_bytes.AddRange(new byte[] {
				2, 0, 0, 0,						// set status to 2
				0x8B, 0x45, 0x08,				// MOV EAX,DWORD PTR SS:[EBP+8]
				0x50,							// PUSH EAX
				0x8B, 0x4D, 0xFC				// MOV ECX,DWORD PTR SS:[EBP-4]
			});
			_originalFuncCallOffset = _bytes.Count;
			_bytes.AddRange(new byte[] {
				255, 255, 255, 255, 255,		// CALL DWORD PTR DS:[SaveGame] (placeholder)
				0xC7, 0x05						// MOV DWORD PTR DS:[?g_status@@3HA],0
			});
			_bytes.AddRange(statusAddrBytes);
			_bytes.AddRange(new byte[] {
				0, 0, 0, 0,
				0x8B, 0xE5,						// MOV ESP,EBP
				0x5D,							// POP EBP
				0xC2, 0x04, 0x00				// RETN 4
			});
		}
	}
}
