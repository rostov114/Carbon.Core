using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Carbon.Contracts
{
	public interface IHookable
	{
		List<string> Hooks { get; set; }
		List<HookMethodAttribute> HookMethods { get; set; }
		List<PluginReferenceAttribute> PluginReferences { get; set; }
		Dictionary<string, List<MethodInfo>> HookCache { get; set; }
		Dictionary<string, List<MethodInfo>> HookMethodAttributeCache { get; set; }
		List<string> IgnoredHooks { get; set; }

		void Subscribe(string hook);
		void Unsubscribe(string hook);
		bool IsHookIgnored(string hook);

		void TrackStart();
		void TrackEnd();
	}
}
