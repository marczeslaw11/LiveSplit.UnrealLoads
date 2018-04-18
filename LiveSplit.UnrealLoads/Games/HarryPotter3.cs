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

		public override HashSet<string> Maps => new HashSet<string>
		{
			"hp3_ch1carperetractum",
			"hp3_ch1carperetractumb",
			"hp3_groundshub",
			"hp3_comc",
			"hp3_ch2draconiforsb",
			"hp3_adv2exppatronum",
			"hp3_adv2glaciusb",
			"hp3_adv4shack",
			"hp3_whompingwillow",
			"hp3_infirmary",
			"hp3_dementorbattle",
			"hp3_fa1ron"
		};
	}
}

