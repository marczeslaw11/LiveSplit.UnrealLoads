﻿using System;
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

		public override Type SaveGameDetourT => typeof(SaveGameDetour_DS9TheFallen);
	}

	class SaveGameDetour_DS9TheFallen : SaveGameDetour
	{
		public new static string Symbol => "?SaveGame@UGameEngine@@UAEXPBD@Z";

		public SaveGameDetour_DS9TheFallen(IntPtr statusAddr)
			: base(statusAddr)
		{
		}
	}
}