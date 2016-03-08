using LiveSplit.Model;
using LiveSplit.UI.Components;
using System;
using System.Reflection;

namespace LiveSplit.UnrealLoads
{
	public class UnrealLoadsFactory : IComponentFactory
	{
		public string ComponentName => "UnrealLoads";

		public string Description => "Autosplitting and load removal component for some Unreal Engine 1 games";

		public ComponentCategory Category => ComponentCategory.Control;

		public IComponent Create(LiveSplitState state) => new UnrealLoadsComponent(state);

		public string UpdateName => ComponentName;

		public string UpdateURL => "https://raw.githubusercontent.com/Dalet/LiveSplit.UnrealLoads/master/";

		public Version Version => Assembly.GetExecutingAssembly().GetName().Version;

		public string XMLURL => UpdateURL + "Components/update.LiveSplit.UnrealLoads.xml";
	}
}
