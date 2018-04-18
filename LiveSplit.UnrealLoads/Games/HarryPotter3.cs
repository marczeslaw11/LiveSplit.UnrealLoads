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
			"hp3_ch1carperetractum.unr",
			"hp3_ch1carperetractumb.unr",
			"hp3_groundshub.unr",
			"hp3_comc.unr",
			"hp3_ch2draconiforsb.unr",
			"hp3_adv2exppatronum.unr",
			"hp3_adv2glaciusb.unr",
			"hp3_adv4shack.unr",
			"hp3_whompingwillow.unr",
			"hp3_infirmary.unr",
			"hp3_dementorbattle.unr",
			"hp3_fa1ron.unr"
		};
	}
}

