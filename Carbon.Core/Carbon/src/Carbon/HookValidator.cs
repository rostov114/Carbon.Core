///
/// Copyright (c) 2022 Carbon Community 
/// All rights reserved
///

using System.Collections.Generic;
using System.Reflection;
using Carbon.Hooks;
using Carbon.Oxide.Metadata;
using Newtonsoft.Json;

namespace Carbon.Core
{
	public class HookValidator
	{
		public static HookPackage OxideHooks { get; private set; }

		public static void Refresh()
		{
			Community.Runtime.CorePlugin.webrequest.Enqueue("https://raw.githubusercontent.com/OxideMod/Oxide.Rust/develop/resources/Rust.opj", null, (error, data) =>
			{
				OxideHooks = JsonConvert.DeserializeObject<HookPackage>(data);
			}, null);
		}

		public static bool IsIncompatibleOxideHook(string hook)
		{
			if (CarbonHookExists(hook)) return false;

			if (OxideHooks != null)
			{
				foreach (var manifest in OxideHooks.Manifests)
				{
					foreach (var entry in manifest.Hooks)
					{
						var hookName = (string.IsNullOrEmpty(entry.Hook.BaseHookName) ? entry.Hook.HookName : entry.Hook.BaseHookName).Split(' ')[0];
						if (hookName.Contains("/")) continue;

						if (hookName == hook) return true;
					}
				}
			}

			return false;
		}

		public static bool CarbonHookExists(string hookName)
		{
			foreach (var hook in Defines.CoreHooks)
			{
				if (hook.Name == hookName) return true;
			}

			foreach (var hook in Defines.DynamicHooks)
			{
				if (hook.Name == hookName) return true;
			}

			return false;
		}
	}
}
