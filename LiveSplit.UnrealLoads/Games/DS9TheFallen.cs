using System;
using System.Collections.Generic;

namespace LiveSplit.UnrealLoads.Games
{
	class DS9TheFallen : GameSupport
	{
		public override HashSet<string> GameNames => new HashSet<string>
		{
			"Star Trek: Deep Space Nine: The Fallen"
		};

		public override HashSet<string> ProcessNames => new HashSet<string>
		{
			"ds9"
		};

		public override LoadMapDetour GetNewLoadMapDetour() => new LoadMapDetour_DS9TheFallen();

		public override SaveGameDetour GetNewSaveGameDetour() => new SaveGameDetour_DS9TheFallen();
	}

	class LoadMapDetour_DS9TheFallen : LoadMapDetour
	{
		public override StringType Encoding => StringType.ASCII;
	}

	class SaveGameDetour_DS9TheFallen : SaveGameDetour
	{
		public override string Symbol => "?SaveGame@UGameEngine@@UAEXPBD@Z";
	}
}
