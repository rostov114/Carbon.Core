﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using API.Contracts;
using API.Events;
using Carbon.Base.Interfaces;
using Carbon.Contracts;
using Carbon.Core;
using Carbon.Extensions;
using Newtonsoft.Json;
using Oxide.Plugins;
using UnityEngine;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon;

public class Community
{
	public static Community Runtime { get; set; }

	public static string Version { get; set; } = "Unknown";
	public static string InformationalVersion { get; set; } = "Unknown";

	public static GameObject GameObject { get => _gameObject.Value; }
	private static readonly Lazy<GameObject> _gameObject = new(() =>
	{
		GameObject gameObject = GameObject.Find("Carbon")
			?? throw new Exception("Carbon GameObject not found");
		return gameObject;
	});

	public IAnalyticsManager Analytics { get => _analyticsManager.Value; }
	private readonly Lazy<IAnalyticsManager> _analyticsManager
		= new(GameObject.GetComponent<IAnalyticsManager>);

	public IAssemblyManager AssemblyEx { get => _assemblyEx.Value; }
	private readonly Lazy<IAssemblyManager> _assemblyEx
		= new(GameObject.GetComponent<IAssemblyManager>);

	public IDownloadManager Downloader { get => _downloadManager.Value; }
	private readonly Lazy<IDownloadManager> _downloadManager
		= new(GameObject.GetComponent<IDownloadManager>);

	public IEventManager Events { get => _eventManager.Value; }
	private readonly Lazy<IEventManager> _eventManager
		= new(GameObject.GetComponent<IEventManager>);


	public IHookManager HookManager { get; set; }
	public IScriptProcessor ScriptProcessor { get; set; }
	public IModuleProcessor ModuleProcessor { get; set; }
	public IWebScriptProcessor WebScriptProcessor { get; set; }
	public ICarbonProcessor CarbonProcessor { get; set; }

	public static bool IsServerFullyInitialized => IsServerFullyInitializedCache = RelationshipManager.ServerInstance != null;
	public static bool IsServerFullyInitializedCache { get; internal set; }

	public static bool IsConfigReady => Runtime != null && Runtime.Config != null;

	public Config Config { get; set; }
	public RustPlugin CorePlugin { get; set; }
	public Loader.CarbonMod Plugins { get; set; }
	public Entities Entities { get; set; }

	public Community()
	{
		try
		{
			Events.Subscribe(CarbonEvent.CarbonStartup, args =>
			{
				Logger.Log($"Carbon fingerprint: {Analytics.ClientID}");
				Analytics.StartSession();
			});

			Events.Subscribe(CarbonEvent.CarbonStartupComplete, args =>
			{
				Analytics.LogEvent("on_server_startup", new Dictionary<string, object>
				{
					{ "branch", Analytics.Branch },
					{ "platform", Analytics.Platform },
					{ "short_version", Analytics.Version },
					{ "full_version", Analytics.InformationalVersion },
				});
			});

			Events.Subscribe(CarbonEvent.AllPluginsLoaded, args =>
			{
				Analytics.LogEvent("on_server_initialized", new Dictionary<string, object>
				{
					{ "plugin_count", Loader.LoadedMods.Sum(x => x.Plugins.Count) },
				});
			});

			Events.Subscribe(CarbonEvent.OnServerSave, args =>
			{
				Analytics.Keepalive();
			});
		}
		catch (Exception ex)
		{
			Logger.Error("Critical error", ex);
		}
	}

	public void ClearCommands(bool all = false)
	{
		if (all)
		{
			AllChatCommands.Clear();
			AllConsoleCommands.Clear();
		}
		else
		{
			AllChatCommands.RemoveAll(x => x.Plugin is not IModule && (x.Plugin is RustPlugin && !(x.Plugin as RustPlugin).IsCorePlugin));
			AllConsoleCommands.RemoveAll(x => x.Plugin is not IModule && (x.Plugin is RustPlugin && !(x.Plugin as RustPlugin).IsCorePlugin));
		}
	}

	#region Config

	public void LoadConfig()
	{
		if (!OsEx.File.Exists(Defines.GetConfigFile()))
		{
			SaveConfig();
			return;
		}

		Config = JsonConvert.DeserializeObject<Config>(OsEx.File.ReadText(Defines.GetConfigFile()));

		var needsSave = false;
		if (Config.ConditionalCompilationSymbols == null)
		{
			Config.ConditionalCompilationSymbols = new();
			needsSave = true;
		}

		if (!Config.ConditionalCompilationSymbols.Contains("CARBON"))
			Config.ConditionalCompilationSymbols.Add("CARBON");

		if (!Config.ConditionalCompilationSymbols.Contains("RUST"))
			Config.ConditionalCompilationSymbols.Add("RUST");

		Config.ConditionalCompilationSymbols = Config.ConditionalCompilationSymbols.Distinct().ToList();

		if (needsSave) SaveConfig();
	}

	public void SaveConfig()
	{
		if (Config == null) Config = new Config();

		OsEx.File.Create(Defines.GetConfigFile(), JsonConvert.SerializeObject(Config, Formatting.Indented));
	}

	#endregion

	#region Plugins

	public virtual void ReloadPlugins()
	{
		Loader.IsBatchComplete = false;
		Loader.ClearAllErrored();
		Loader.ClearAllRequirees();
	}
	public void ClearPlugins()
	{
		Runtime.ClearCommands();
		Loader.UnloadCarbonMods();
	}

	#endregion

	public void RefreshConsoleInfo()
	{
#if WIN
		if (!IsConfigReady || !Config.ShowConsoleInfo) return;

		if (!IsServerFullyInitialized) return;
		if (ServerConsole.Instance.input.statusText.Length != 4) ServerConsole.Instance.input.statusText = new string[4];

		var version =
#if DEBUG
			InformationalVersion;
#else
            Version;
#endif

		ServerConsole.Instance.input.statusText[3] = $" Carbon v{version}, {Loader.LoadedMods.Count:n0} mods, {Loader.LoadedMods.Sum(x => x.Plugins.Count):n0} plgs";
#endif
	}

	#region Commands

	public List<OxideCommand> AllChatCommands { get; } = new List<OxideCommand>();
	public List<OxideCommand> AllConsoleCommands { get; } = new List<OxideCommand>();

	#endregion

	#region Logging

	public static void LogCommand(object message, BasePlayer player = null)
	{
		if (player == null)
		{
			Carbon.Logger.Log(message);
			return;
		}

		player.SendConsoleCommand($"echo {message}");
	}

	#endregion
}
