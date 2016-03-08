using LiveSplit.ComponentUtil;
using LiveSplit.UnrealLoads.GameSupport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LiveSplit.UnrealLoads
{
	class GameMemory
	{
		public const int SLEEP_TIME = 15;

		public static readonly IGameSupport[] SupportedGames = new IGameSupport[] {
			new HarryPotter2(),
			new Shrek2(),
			new WheelOfTime()
		};

		enum Status
		{
			None,
			LoadingMap,
			Saving
		}

		public event EventHandler OnReset;
		public event EventHandler OnStart;
		public event EventHandler OnLoadStarted;
		public event EventHandler OnLoadEnded;
		public event MapChangeEventHandler OnMapChange;
		public delegate void MapChangeEventHandler(object sender, string map);

		public IGameSupport Game { get; private set; }

		Task _thread;
		CancellationTokenSource _cancelSource;
		SynchronizationContext _uiThread;
		HashSet<int> _ignorePIDs;
		int _lastPID;

		MemoryWatcherList _watchers;
		MemoryWatcher<int> _status;
		StringWatcher _map;

		ProcessModuleWow64Safe _engine;
		IntPtr _loadMapJMP;
		IntPtr _saveGameJMP;
		IntPtr _realLoadMapPtr;
		IntPtr _realSaveGamePtr;
		IntPtr _fakeLoadMapPtr;
		IntPtr _fakeSaveGamePtr;
		IntPtr _statusPtr;
		IntPtr _mapPtr;
		readonly int MAP_SIZE = Encoding.Unicode.GetMaxByteCount(260); // MAX_PATH == 260
		const string LOADMAP_SYMBOL = "?LoadMap@UGameEngine@@UAEPAVULevel@@ABVFURL@@PAVUPendingLevel@@PBV?$TMap@VFString@@V1@@@AAVFString@@@Z";
		const string SAVEGAME_SYMBOL = "?SaveGame@UGameEngine@@UAEXH@Z";

		public GameMemory()
		{
			_ignorePIDs = new HashSet<int>();
			_watchers = new MemoryWatcherList();
		}

		public void StartMonitoring()
		{
			if (_thread != null && _thread.Status == TaskStatus.Running)
				throw new InvalidOperationException();

			if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
				throw new InvalidOperationException("SynchronizationContext.Current is not a UI thread.");

			_uiThread = SynchronizationContext.Current;
			_cancelSource = new CancellationTokenSource();
			_thread = Task.Factory.StartNew(MemoryReadThread);
		}

		public void Stop()
		{
			if (_cancelSource == null || _thread == null || _thread.Status != TaskStatus.Running)
				return;

			_cancelSource.Cancel();
			_thread.Wait();
		}

		void MemoryReadThread()
		{
			Process game = null;

			while (!_cancelSource.IsCancellationRequested)
			{
				try
				{
					Trace.WriteLine("[NoLoads] Waiting for a game process...");

					while ((game = GetGameProcess()) == null)
					{
						Thread.Sleep(250);
						if (_cancelSource.IsCancellationRequested)
							return;
					}

					Trace.WriteLine($"[NoLoads] Attached to {game.ProcessName}.exe ({Game.GetType().Name})");
					_uiThread.Post(d => OnLoadEnded?.Invoke(this, EventArgs.Empty), null); //unpause at launch

					uint frameCounter = 0;
					bool isLoading;
					bool prevIsLoading = false;
					var map = string.Empty;

					Game.OnAttach(game);

					while (!game.HasExited)
					{
						_watchers.UpdateAll(game);
						DoTimerAction(Game.OnUpdate(game, _watchers));

						var gameSupportIsLoading = Game.IsLoading(_watchers);
						if (!gameSupportIsLoading.HasValue)
							isLoading = _status.Current != (int)Status.None;
						else
							isLoading = gameSupportIsLoading.Value;

						Debug.WriteLineIf(_status.Changed, string.Format("[NoLoads] Status changed from {1} to {2} - {0}", frameCounter, (Status)_status.Old, (Status)_status.Current));

						if (_map.Changed)
						{
							map = _map.Current.ToLower();
							_uiThread.Post(d => OnMapChange?.Invoke(this, _map.Current), null);
							Debug.WriteLine(string.Format("[NoLoads] Map is changing from \"{0}\" to \"{1}\" - {2}", _map.Old, _map.Current, frameCounter));
						}

						if (_status.Changed && _status.Current == (int)Status.LoadingMap)
						{
							DoTimerAction(Game.OnMapLoad(_map));
						}

						if (isLoading != prevIsLoading)
						{
							if (isLoading)
							{
								_uiThread.Post(d => OnLoadStarted?.Invoke(this, EventArgs.Empty), null);
								Trace.WriteLine(string.Format("[NoLoads] Load start - {0}", frameCounter));
							}
							else
							{
								_uiThread.Post(d => OnLoadEnded?.Invoke(this, EventArgs.Empty), null);
								Trace.WriteLine(string.Format("[NoLoads] Load end - {0}", frameCounter));
							}
						}

						prevIsLoading = isLoading;
						frameCounter++;

						Thread.Sleep(SLEEP_TIME);

						if (_cancelSource.IsCancellationRequested)
							break;
					}

					//pause on crash/exit
					_uiThread.Post(d => OnLoadStarted?.Invoke(this, EventArgs.Empty), null);
					DoTimerAction(Game.OnDetach(game));
				}
				catch (Exception ex)
				{
					Trace.WriteLine(ex.ToString());
					Thread.Sleep(1000);
				}
			}

			Unpatch(game);
		}

		void DoTimerAction(TimerAction action)
		{
			EventHandler evnt = null;
			switch (action)
			{
				case TimerAction.DoNothing:
					break;
				case TimerAction.Reset:
					evnt = OnReset;
					break;
				case TimerAction.Start:
					evnt = OnStart;
					break;
				case TimerAction.PauseGameTime:
					evnt = OnLoadStarted;
					break;
				case TimerAction.UnpauseGameTime:
					evnt = OnLoadEnded;
					break;
			}

			_uiThread.Post(d => evnt?.Invoke(this, EventArgs.Empty), null);
		}

		void DoTimerAction(IEnumerable<TimerAction> actions)
		{
			if (actions == null)
				return;

			foreach (var action in actions)
				DoTimerAction(action);
		}

		Process GetGameProcess()
		{
			Process game = null;
			foreach (var p in Process.GetProcesses())
			{
				var names = SupportedGames.SelectMany(g => g.ProcessNames);
				if (!names.Contains(p.ProcessName.ToLower()) || p.HasExited || _ignorePIDs.Contains(p.Id)
					|| (_engine = p.ModulesWow64Safe().FirstOrDefault(m => m.ModuleName.ToLower() == "engine.dll")) == null)
					continue;

				var ignoreProcess = true;
				foreach (var gameSupport in SupportedGames)
				{
					if (!gameSupport.ProcessNames.Contains(p.ProcessName.ToLower()))
						continue;

					switch (gameSupport.IdentifyProcess(p))
					{
						case IdentificationResult.Success:
							game = p;
							Game = gameSupport;
							break;
						case IdentificationResult.Undecisive:
							ignoreProcess = false; // don't ignore if at least one game is unsure
							break;
					}
				}

				if (game != null)
					break;

				if (ignoreProcess)
					_ignorePIDs.Add(p.Id);
			}

			if (game == null)
				return null;

			if (_lastPID != game.Id)
			{
				_watchers.Clear();

				if (!Patch(game))
					return null;

				_status = new MemoryWatcher<int>(_statusPtr);
				_map = new StringWatcher(_mapPtr, ReadStringType.UTF16, MAP_SIZE);
				_watchers.AddRange(new MemoryWatcher[] { _status, _map });
			}

			_lastPID = game.Id;
			return game;
		}

		bool Patch(Process game)
		{
			var exportsParser = new ExportTableParser(game, "engine.dll");
			exportsParser.Parse();

			_loadMapJMP = exportsParser.Exports[LOADMAP_SYMBOL] + 1;
			_saveGameJMP = exportsParser.Exports[SAVEGAME_SYMBOL] + 1;

			_realLoadMapPtr = _loadMapJMP + sizeof(int) + game.ReadValue<int>(_loadMapJMP);
			_realSaveGamePtr = _saveGameJMP + sizeof(int) + game.ReadValue<int>(_saveGameJMP);

			_statusPtr = game.AllocateMemory(sizeof(int));
			_mapPtr = game.AllocateMemory(MAP_SIZE);

			var statusAddrBytes = BitConverter.GetBytes((int)_statusPtr);
			var mapAddrBytes = BitConverter.GetBytes((int)_mapPtr);

			var fakeSaveGameBytes = new List<byte>() {
				0x55, 							// PUSH EBP
				0x8B, 0xEC,						// MOV EBP,ESP
				0x83, 0xEC, 0x08,				// SUB ESP,8
				0x89, 0x55, 0xF8,				// MOV DWORD PTR SS:[EBP-8],EDX
				0x89, 0x4D, 0xFC,				// MOV DWORD PTR SS:[EBP-4],ECX
				0xC7, 0x05						// MOV DWORD PTR DS:[?g_status@@3HA],2
			};
			fakeSaveGameBytes.AddRange(statusAddrBytes);
			fakeSaveGameBytes.AddRange(new byte[] {
				2, 0, 0, 0,						// set status to 2
				0x8B, 0x45, 0x08,				// MOV EAX,DWORD PTR SS:[EBP+8]
				0x50,							// PUSH EAX
				0x8B, 0x4D, 0xFC				// MOV ECX,DWORD PTR SS:[EBP-4]
			});
			var callOffset = fakeSaveGameBytes.Count;
			fakeSaveGameBytes.AddRange(new byte[] {
				255, 255, 255, 255, 255,		// CALL DWORD PTR DS:[SaveGame] (placeholder)
				0xC7, 0x05						// MOV DWORD PTR DS:[?g_status@@3HA],0
			});
			fakeSaveGameBytes.AddRange(statusAddrBytes);
			fakeSaveGameBytes.AddRange(new byte[] {
				0, 0, 0, 0,
				0x8B, 0xE5,						// MOV ESP,EBP
				0x5D,							// POP EBP
				0xC2, 0x04, 0x00				// RETN 4
			});

			_fakeSaveGamePtr = game.AllocateMemory(fakeSaveGameBytes.Count);
			game.WriteBytes(_fakeSaveGamePtr, fakeSaveGameBytes.ToArray());
			game.WriteCallInstruction(_fakeSaveGamePtr + callOffset, _realSaveGamePtr);

			var fakeLoadMapBytes = new List<byte>() {
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
			fakeLoadMapBytes.AddRange(mapAddrBytes);
			fakeLoadMapBytes.AddRange(new byte[] {
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
			fakeLoadMapBytes.AddRange(mapAddrBytes);
			fakeLoadMapBytes.AddRange(new byte[] {
				0xC7, 0x05						// MOV DWORD PTR DS:[?g_status@@3HA],1
			});
			fakeLoadMapBytes.AddRange(statusAddrBytes);
			fakeLoadMapBytes.AddRange(new byte[] {
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
			callOffset = fakeLoadMapBytes.Count;
			fakeLoadMapBytes.AddRange(new byte[] {
				255, 255, 255, 255, 255,		// CALL DWORD PTR DS:[LoadMap] (placeholder)
				0x89, 0x45, 0xF0,				// MOV DWORD PTR SS:[EBP-10],EAX
				0xC7, 0x05						// MOV DWORD PTR DS:[?g_status@@3HA],0
			});
			fakeLoadMapBytes.AddRange(statusAddrBytes);
			fakeLoadMapBytes.AddRange(new byte[] {
				0, 0, 0, 0,						// set status to 0
				0x8B, 0x45, 0xF0,				// MOV EAX,DWORD PTR SS:[EBP-10]
				0x8B, 0xE5,						// MOV ESP,EBP
				0x5D,							// POP EBP
				0xC2, 0x10, 0x00				// RETN 10
			});

			_fakeLoadMapPtr = game.AllocateMemory(fakeLoadMapBytes.Count);
			game.WriteBytes(_fakeLoadMapPtr, fakeLoadMapBytes.ToArray());
			game.WriteCallInstruction(_fakeLoadMapPtr + callOffset, _realLoadMapPtr);

			game.Suspend();
			try
			{
				// patch JMPs
				game.WriteBytes(_loadMapJMP,
					BitConverter.GetBytes((int)_fakeLoadMapPtr - (int)(_loadMapJMP + sizeof(int))));
				game.WriteBytes(_saveGameJMP,
					BitConverter.GetBytes((int)_fakeSaveGamePtr - (int)(_saveGameJMP + sizeof(int))));

				Debug.WriteLine("Status: " + _statusPtr.ToString("X") + " Map: " + _mapPtr.ToString("X"));
				Debug.WriteLine("FakeSaveGame: " + _fakeSaveGamePtr.ToString("X") + " FakeLoadMap: " + _fakeLoadMapPtr.ToString("X"));
				Debug.WriteLine("SaveGame: " + _realSaveGamePtr.ToString("X") + " LoadMap: " + _realLoadMapPtr.ToString("X"));
				Debug.WriteLine("Hooks installed");
			}
			catch
			{
				FreeMemory(game);
				return false;
			}
			finally
			{
				game.Resume();
			}

			return true;
		}

		bool Unpatch(Process game)
		{
			if (game == null || game.HasExited)
				return false;

			game.Suspend();
			try
			{
				// restore JMPs
				game.WriteBytes(_loadMapJMP,
					BitConverter.GetBytes((int)_realLoadMapPtr - (int)(_loadMapJMP + sizeof(int))));
				game.WriteBytes(_saveGameJMP,
					BitConverter.GetBytes((int)_realSaveGamePtr - (int)(_saveGameJMP + sizeof(int))));
			}
			catch
			{
				Debug.WriteLine("[NoLoads] Restoring the thunks failed.");
				return false;
			}
			finally
			{
				game.Resume();
				FreeMemory(game);
			}

			return true;
		}

		void FreeMemory(Process game)
		{
			if (game == null || game.HasExited)
				return;

			game.FreeMemory(_statusPtr);
			game.FreeMemory(_mapPtr);
			game.FreeMemory(_fakeLoadMapPtr);
			game.FreeMemory(_fakeSaveGamePtr);
		}
	}
}
