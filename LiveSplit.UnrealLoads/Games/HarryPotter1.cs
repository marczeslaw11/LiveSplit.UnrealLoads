using System.Collections.Generic;

namespace LiveSplit.UnrealLoads.Games
{
	class HarryPotter1 : GameSupport
	{
		public override HashSet<string> GameNames => new HashSet<string>
		{
			"Harry Potter 1",
			"Harry Potter and the Philosopher's Stone",
			"HP1",
			"HP 1"
		};

		public override HashSet<string> ProcessNames => new HashSet<string>
		{
			"hp"
		};
	}
}
