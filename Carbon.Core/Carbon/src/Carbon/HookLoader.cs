///
/// Copyright (c) 2022 Carbon Community 
/// All rights reserved
/// 

using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using Carbon.Core;
using Carbon.Extensions;
using Carbon.Processors;

namespace Carbon.Hooks
{
	public class HookLoader
	{
		public const string HooksFile = "D:\\Work\\Repositories\\raulssorban\\Carbon.Hooks\\Carbon.Hooks\\bin\\Debug\\net48\\Carbon.Hooks.dll" /*"https://github.com/CarbonCommunity/Carbon.Core/raw/develop/Tools/Hooks/Carbon.Hooks.dll"*/;

		public static Assembly CurrentHooks { get; private set; }

		public static void DownloadHooks(Action onDownloaded)
		{
			if (HooksFile.StartsWith("http"))
			{
				var client = new WebClient();

				Community.ClearPlugins();

				client.DownloadDataCompleted += (object sender, DownloadDataCompletedEventArgs e) =>
				{
					try
					{
						CurrentHooks = Assembly.Load(e.Result);

						PostHookLoad();

						onDownloaded?.Invoke();
					}
					catch (Exception ex)
					{
						Logger.Error("DownloadHooks failed", ex);
					}
				};

				client.DownloadDataAsync(new Uri(HooksFile));
			}
			else
			{
				CurrentHooks = Assembly.Load(OsEx.File.ReadBytes(HooksFile));

				PostHookLoad();

				onDownloaded?.Invoke();
			}
		}

		internal static void PostHookLoad()
		{
			foreach (var hook in Community.Runtime.HookProcessor.Patches)
			{
				Community.Runtime.HookProcessor.UninstallHooks(hook.Key, shutdown: true);
			}

			Defines.DynamicHooks?.Clear();
			Defines.DynamicHooks = new List<Hook>();

			foreach (var type in CurrentHooks.GetTypes())
			{
				var hook = type.GetCustomAttribute<Hook>();
				if (hook == null) continue;

				hook.Type = type;
				Defines.DynamicHooks.Add(hook);
			}

			Community.Runtime.HookProcessor.InstallAlwaysPatchedHooks();
		}
	}
}
