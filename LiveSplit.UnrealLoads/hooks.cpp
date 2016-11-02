#include <windows.h>

#define STATUS_NONE			0
#define STATUS_LOADING_MAP	1
#define STATUS_SAVING		2

typedef void *(__thiscall *t_LoadMap)(void*, const void*, void*, const void*, void*);
typedef void *(__thiscall *t_LoadMap_oldUnreal)(void*, const void*, void*, const void*, void*, void*);
typedef void (__thiscall *t_SaveGame)(void*, int);

t_LoadMap			g_oLoadMap				= NULL;
t_LoadMap_oldUnreal	g_oLoadMap_oldUnreal	= NULL;
t_SaveGame			g_oSaveGame 			= NULL;

__declspec(dllexport) int		g_status = STATUS_NONE;
__declspec(dllexport) wchar_t	g_map[MAX_PATH];

void set_map(const wchar_t *map)
{
	for (int i = 0; i < MAX_PATH; i++)
	{
		g_map[i] = map[i];
		if (map[i] == '\0')
			break;
	}
	g_map[MAX_PATH - 1] = '\0';
}

void* __fastcall	Detour_LoadMap(void *This, void *edx, const void *URL, void *Pending, const void *TravelInfo, void *Error)
{
	wchar_t *map = *((wchar_t **)URL + 7);
	set_map(map);

	g_status = STATUS_LOADING_MAP;
	void *level = g_oLoadMap(This, URL, Pending, TravelInfo, Error);
	g_status = STATUS_NONE;

	return level;
}

void* __fastcall	Detour_LoadMap_oldUnreal(void *This, void *edx, const void *URL, void *Pending, const void *TravelInfo, void *Error, void *UTravelDataManager)
{
	wchar_t *map = *((wchar_t **)URL + 7);
	set_map(map);

	g_status = STATUS_LOADING_MAP;
	void *level = g_oLoadMap_oldUnreal(This, URL, Pending, TravelInfo, Error, UTravelDataManager);
	g_status = STATUS_NONE;

	return level;
}

void __fastcall		Detour_SaveGame(void *This, void *edx, int Position)
{
	g_status = STATUS_SAVING;
	g_oSaveGame(This, Position);
	g_status = STATUS_NONE;
}

int main()
{
	return 0;
}
