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

		public IntPtr Install(Process process)
		{
			Debug.WriteLine($"[NoLoads] Hooking using {(_usesTrampoline ? "trampolines" : "thunks")}...");

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

			return InjectedFuncPtr;
		}

		public void Uninstall(Process process)
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

		public void FreeMemory(Process process)
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

	public class LoadMapDetour : Detour
	{
		public const string SYMBOL = "?LoadMap@UGameEngine@@UAEPAVULevel@@ABVFURL@@PAVUPendingLevel@@PBV?$TMap@VFString@@V1@@@AAVFString@@@Z";

		public LoadMapDetour(Process process, IntPtr functionToDetour, IntPtr mapAddr, IntPtr statusAddr)
			: base(process, functionToDetour)
		{
			var mapAddrBytes = mapAddr.ToBytes();
			var statusAddrBytes = statusAddr.ToBytes();

			_bytes = new List<byte>() {
				0x55,							// PUSH EBP
				0x8B, 0xEC,						// MOV EBP,ESP
				0x83, 0xEC, 0x14,				// SUB ESP,14
				0x89, 0x55, 0xEC,				// MOV DWORD PTR SS:[EBP-14],EDX
				0x89, 0x4D, 0xF4,				// MOV DWORD PTR SS:[EBP-C],ECX
				0x8B, 0x45, 0x08,				// MOV EAX,DWORD PTR SS:[EBP+8]
				0x8B, 0x48, 0x1C,				// MOV ECX,DWORD PTR DS:[EAX+1C]
				0x89, 0x4D, 0xF8,				// MOV DWORD PTR SS:[EBP-8],ECX
				0xC7, 0x45, 0xFC, 0, 0, 0, 0,	// MOV DWORD PTR SS:[EBP-4],0
				0xEB, 0x09,						// JMP SHORT main.00E01291
				0x8B, 0x55, 0xFC,				// /MOV EDX,DWORD PTR SS:[EBP-4]
				0x83, 0xC2, 0x01,				// |ADD EDX,1
				0x89, 0x55, 0xFC,				// |MOV DWORD PTR SS:[EBP-4],EDX
				0x81, 0x7D, 0xFC, 4, 1, 0, 0,	//>|CMP DWORD PTR SS:[EBP-4],104
				0x7D, 0x27,						// |JGE SHORT main.00E012C1
				0x8B, 0x45, 0xFC,				// |MOV EAX,DWORD PTR SS:[EBP-4]
				0x8B, 0x4D, 0xFC,				// |MOV ECX,DWORD PTR SS:[EBP-4]
				0x8B, 0x55, 0xF8,				// |MOV EDX,DWORD PTR SS:[EBP-8]
				0x66, 0x8B, 0x0C, 0x4A,			// |MOV CX,WORD PTR DS:[EDX+ECX*2]
				0x66, 0x89, 0x0C, 0x45			// |MOV WORD PTR DS:[EAX*2+?g_map@@3PA_WA],CX
			};
			_bytes.AddRange(mapAddrBytes);
			_bytes.AddRange(new byte[] {
				0x8B, 0x55, 0xFC,				// |MOV EDX,DWORD PTR SS:[EBP-4]
				0x8B, 0x45, 0xF8,				// |MOV EAX,DWORD PTR SS:[EBP-8]
				0x0F, 0xB7, 0x0C, 0x50,			// |MOVZX ECX,WORD PTR DS:[EAX+EDX*2]
				0x85, 0xC9,						// |TEST ECX,ECX
				0x75, 0x02,						// |JNZ SHORT main.00E012BF
				0xEB, 0x02,						// |JMP SHORT main.00E012C1
				0xEB, 0xC7,						// \JMP SHORT main.00E01288
				0xBA, 2, 0, 0, 0,				// MOV EDX,2
				0x69, 0xC2, 3, 1, 0, 0,			// IMUL EAX,EDX,103
				0x33, 0xC9,						// XOR ECX, ECX
				0x66, 0x89, 0x88				// MOV WORD PTR DS:[EAX+?g_map@@3PA_WA],CX
			});
			_bytes.AddRange(mapAddrBytes);
			_bytes.AddRange(new byte[] {
				0xC7, 0x05						// MOV DWORD PTR DS:[?g_status@@3HA],1
			});
			_bytes.AddRange(statusAddrBytes);
			_bytes.AddRange(new byte[] {
				1, 0, 0, 0,						// set status to 1
				0x8B, 0x55, 0x14,				// MOV EDX,DWORD PTR SS:[EBP+14]
				0x52,							// PUSH EDX
				0x8B, 0x45, 0x10,				// MOV EAX,DWORD PTR SS:[EBP+10]
				0x50,							// PUSH EAX
				0x8B, 0x4D, 0x0C,				// MOV CDX,DWORD PTR SS:[EBP+C]
				0x51,							// PUSH ECX
				0x8B, 0x55, 0x08,				// MOV EDX,DWORD PTR SS:[EBP+8]
				0x52,							// PUSH EDX
				0x8B, 0x4D, 0xF4				// MOV ECX,DWORD PTR SS:[EBP-C]
			});
			_originalFuncCallOffset = _bytes.Count;
			_bytes.AddRange(new byte[] {
				255, 255, 255, 255, 255,		// CALL DWORD PTR DS:[LoadMap] (placeholder)
				0x89, 0x45, 0xF0,				// MOV DWORD PTR SS:[EBP-10],EAX
				0xC7, 0x05						// MOV DWORD PTR DS:[?g_status@@3HA],0
			});
			_bytes.AddRange(statusAddrBytes);
			_bytes.AddRange(new byte[] {
				0, 0, 0, 0,						// set status to 0
				0x8B, 0x45, 0xF0,				// MOV EAX,DWORD PTR SS:[EBP-10]
				0x8B, 0xE5,						// MOV ESP,EBP
				0x5D,							// POP EBP
				0xC2, 0x10, 0x00				// RETN 10
			});
		}
	}

	public class LoadMapDetour_oldUnreal : Detour
	{
		public const string SYMBOL = "?LoadMap@UGameEngine@@UAEPAVULevel@@ABVFURL@@PAVUPendingLevel@@PBV?$TMap@VFString@@V1@@@AAVFString@@PAVUTravelDataManager@@@Z";

		public LoadMapDetour_oldUnreal(Process process, IntPtr funcToDetour, IntPtr mapAddr, IntPtr statusAddr)
			: base(process, funcToDetour)
		{
			_overwrittenBytes = 8;
			var mapAddrBytes = mapAddr.ToBytes();
			var statusAddrBytes = statusAddr.ToBytes();

			_bytes = new List<byte>()
			{
				0x55,							// push ebp
				0x8B, 0xEC,						// mov ebp,esp
				0x83, 0xEC, 0x18,				// sub esp,18
				0x89, 0x55, 0xE8,				// mov dword ptr ds:[ebp-18],edx
				0x89, 0x4D, 0xF0,				// mov dword ptr ds:[ebp-10],ecx
				0x8B, 0x45, 0x08,				// mov eax,dword ptr ds:[ebp+8]
				0x8B, 0x48, 0x1C,				// mov ecx,dword ptr ds:[eax+1C]
				0x89, 0x4D, 0xF8,				// mov dword ptr ds:[ebp-8],ecx
				0xC7, 0x45, 0xFC, 0, 0, 0, 0,	// mov dword ptr ds:[ebp-4],0
				0xEB, 0x09,						// jmp hooks.F910E7
				0x8B, 0x55, 0xFC,				// mov edx,dword ptr ds:[ebp-4]
				0x83, 0xC2, 0x01,				// add edx,1
				0x89, 0x55, 0xFC,				// mov dword ptr ds:[ebp-4],edx
				0x81, 0x7D, 0xFC, 4, 1, 0, 0,	// cmp dword ptr ds:[ebp-4],104
				0x7D, 0x27,						// jge hooks.F91117
				0x8B, 0x45, 0xFC,				// mov eax,dword ptr ds:[ebp-4]
				0x8B, 0x4D, 0xFC,				// mov ecx,dword ptr ds:[ebp-4]
				0x8B, 0x55, 0xF8,				// mov edx,dword ptr ds:[ebp-8]
				0x66, 0x8B, 0x0C, 0x4A,			// mov cx,word ptr ds:[edx+ecx*2]
				0x66, 0x89, 0x0C, 0x45          // mov word ptr ds:[eax*2+<?g_map@@3PA_WA>
			};
			_bytes.AddRange(mapAddrBytes);
			_bytes.AddRange(new byte[]
			{
				0x8B, 0x55, 0xFC,				// mov edx,dword ptr ds:[ebp-4]
				0x8B, 0x45, 0xF8,				// mov eax,dword ptr ds:[ebp-8]
				0x0F, 0xB7, 0x0C, 0x50,			// movzx ecx,word ptr ds:[eax+edx*2]
				0x85, 0xC9,						// test ecx,ecx
				0x75, 0x02,						// jne hooks.F91115
				0xEB, 0x02,						// jmp hooks.F91117
				0xEB, 0xC7,						// jmp hooks.F910DE
				0xBA, 0x02, 0x00, 0x00, 0x00,	// mov edx,2
				0x69, 0xC2, 3, 1, 0, 0,			// imul eax,edx,103
				0x89, 0x45, 0xF4,				// mov dword ptr ds:[ebp-C],eax
				0x81, 0x7D, 0xF4, 8, 2, 0, 0,	// cmp dword ptr ds:[ebp-C],208
				0x73, 0x02,						// jae hooks.F91130
				0xEB, 0x05,						// jmp hooks.F91135
				0xE8, 0xB5, 0x01, 0x00, 0x00,	// call hooks.F912EA
				0x33, 0xC9,						// xor ecx,ecx
				0x8B, 0x55, 0xF4,				// mov edx,dword ptr ds:[ebp-C]
				0x66, 0x89, 0x8A,               // mov word ptr ds:[edx+<?g_map@@3PA_WA>],
			});
			_bytes.AddRange(mapAddrBytes);
			_bytes.AddRange(new byte[]
			{
				0xC7, 0x05,                     // mov dword ptr ds:[<?g_status@@3HA>],1
			});
			_bytes.AddRange(statusAddrBytes);
			_bytes.AddRange(new byte[]
			{
				1, 0, 0, 0,
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
				0x8B, 0x4D, 0xF0,               // mov ecx,dword ptr ds:[ebp-10]
			});
			_originalFuncCallOffset = _bytes.Count;
			_bytes.AddRange(new byte[]
			{
				255, 255, 255, 255, 255,		// call dword ptr ds:[FA3784] (placeholder)
				0x89, 0x45, 0xEC,				// mov dword ptr ds:[ebp-14],eax
				0xC7, 0x05,						// mov dword ptr ds:[<?g_status@@3HA>]
			});
			_bytes.AddRange(statusAddrBytes);
			_bytes.AddRange(new byte[]
			{
				0, 0, 0, 0,						// set status to 0
				0x8B, 0x45, 0xEC,				// mov eax,dword ptr ds:[ebp-14]
				0x8B, 0xE5,						// mov esp,ebp
				0x5D,							// pop ebp
				0xC2, 0x14, 0x00,				// ret 14
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
