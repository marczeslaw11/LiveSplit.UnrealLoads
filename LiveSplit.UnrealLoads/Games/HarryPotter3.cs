using System.Collections.Generic;

namespace LiveSplit.UnrealLoads.Games
{
	class HarryPotter3 : GameSupport
	{
		public override HashSet<string> GameNames => new HashSet<string>
		{
			"Harry Potter 3",
			"Harry Potter and the Prisoner of Azkaban",
			"HP3",
			"HP 3"
		};

		public override HashSet<string> ProcessNames => new HashSet<string>
		{
			"hppoa"
		};
	}
}

