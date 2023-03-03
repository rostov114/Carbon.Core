using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Carbon.Components;
using Carbon.Core;
using Oxide.Core;
using Facepunch;
using System.IO;
using Carbon.Features;
using UnityEngine;
using Carbon.Plugins.Features;
using Carbon.Contracts;
using Command = Carbon.Components.Command;
using static ConsoleSystem;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Plugins;

public class CarbonPlugin : Plugin
{
	public CUI.Handler CuiHandler { get; set; }

	public Permission Permission = new();
	public WebRequests WebRequests = new();
	public Timers Timers = new();

	public override void SetupMod(Loader.CarbonMod mod, string name, string author, VersionNumber version, string description)
	{
		CarbonMod = mod;
		Setup(name, author, version, description);
	}
	public override void Setup(string name, string author, VersionNumber version, string description)
	{
		Name = name;
		Version = version;
		Author = author;
		Description = description;
		CuiHandler = new CUI.Handler();
		Localisation = new Localisation(this);
	}

	#region CUI

	public CUI CreateCUI()
	{
		return new CUI(CuiHandler);
	}

	#endregion

	#region Config

	public BaseFile Config { get; private set; }

	public virtual Type ConfigSerializer => typeof(JsonConfig);

	protected virtual void LoadConfig()
	{
		Config = (BaseFile)Activator.CreateInstance(ConfigSerializer, Path.Combine(Defines.GetConfigsFolder(), Name + ".json"));

		if (!Config.Exists(null))
		{
			LoadDefaultConfig();
			SaveConfig();
		}

		try
		{
			Config.Load(null);
		}
		catch (Exception ex)
		{
			Carbon.Logger.Error("Failed to load config file (is the config file corrupt?) (" + ex.Message + ")");
		}
	}

	protected virtual void SaveConfig()
	{
		if (Config == null)
		{
			return;
		}
		try
		{
			Config.Save(null);
		}
		catch (Exception ex)
		{
			Carbon.Logger.Error("Failed to save config file (does the config have illegal objects in it?) (" + ex.Message + ")", ex);
		}
	}

	protected virtual void LoadDefaultConfig()
	{

	}

	#endregion

	#region Localisation

	public Localisation Localisation { get; internal set; }

	protected virtual void LoadDefaultMessages()
	{

	}

	#endregion

	#region Command

	public static bool FromRcon { get; set; }

	public static void AddChatCommand(string name, IMetadata plugin, Action<BasePlayer, string, string[]> callback, bool skipOriginal = true, string help = null, object reference = null, string[] permissions = null, string[] groups = null, int authLevel = -1, int cooldown = 0)
	{
		if (Community.Runtime.AllChatCommands.Count(x => x.Name == name) == 0)
		{
			Community.Runtime.AllChatCommands.Add(new Command
			{
				Name = name,
				Plugin = plugin,
				SkipOriginal = skipOriginal,
				Callback = (player, cmd, args) =>
				{
					try { callback.Invoke(player, cmd, args); }
					catch (Exception ex) { if (plugin is CarbonPlugin carbonPlugin) carbonPlugin.LogError("Error", ex.InnerException ?? ex); }
				},
				Help = help,
				Reference = reference,
				Permissions = permissions,
				Groups = groups,
				AuthLevel = authLevel,
				Cooldown = cooldown
			});
		}
		else Logger.Warn($"Chat command '{name}' already exists.");
	}
	public static void AddChatCommand(string name, IMetadata plugin, string method, bool skipOriginal = true, string help = null, object reference = null, string[] permissions = null, string[] groups = null, int authLevel = -1, int cooldown = 0)
	{
		AddChatCommand(name, plugin, (player, cmd, args) =>
		{
			var argData = Pool.GetList<object>();
			var result = (object[])null;
			try
			{
				var m = plugin.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				var ps = m.GetParameters();
				switch (ps.Length)
				{
					case 1:
						{
							argData.Add(player);
							result = argData.ToArray();
							break;
						}

					case 2:
						{
							argData.Add(player);
							argData.Add(cmd);
							result = argData.ToArray();
							break;
						}

					case 3:
						{
							argData.Add(player);
							argData.Add(cmd);
							argData.Add(args);
							result = argData.ToArray();
							break;
						}
				}

				m?.Invoke(plugin, result);
			}
			catch (Exception ex) { if (plugin is CarbonPlugin rustPlugin) rustPlugin.LogError("Error", ex.InnerException ?? ex); }

			if (argData != null) Pool.FreeList(ref argData);
			if (result != null) Pool.Free(ref result);
		}, skipOriginal, help, reference, permissions, groups, authLevel, cooldown);
	}
	public static void AddConsoleCommand(string name, IMetadata plugin, Action<BasePlayer, string, string[]> callback, bool skipOriginal = true, string help = null, object reference = null, string[] permissions = null, string[] groups = null, int authLevel = -1, int cooldown = 0)
	{
		if (Community.Runtime.AllConsoleCommands.Count(x => x.Name == name) == 0)
		{
			Community.Runtime.AllConsoleCommands.Add(new Command
			{
				Name = name,
				Plugin = plugin,
				SkipOriginal = skipOriginal,
				Callback = callback,
				Help = help,
				Reference = reference,
				Permissions = permissions,
				Groups = groups,
				AuthLevel = authLevel,
				Cooldown = cooldown
			});
		}
		else Logger.Warn($"Console command '{name}' already exists.");
	}
	public static void AddConsoleCommand(string name, IMetadata plugin, string method, bool skipOriginal = true, string help = null, object reference = null, string[] permissions = null, string[] groups = null, int authLevel = -1, int cooldown = 0)
	{
		AddConsoleCommand(name, plugin, (player, cmd, args) =>
		{
			var arguments = Pool.GetList<object>();
			var result = (object[])null;

			try
			{
				var fullString = args == null || args.Length == 0 ? string.Empty : string.Join(" ", args);
				var client = player == null ? Option.Unrestricted : Option.Client;
				var arg = FormatterServices.GetUninitializedObject(typeof(Arg)) as Arg;
				if (player != null) client = client.FromConnection(player.net.connection);
				client.FromRcon = FromRcon;
				arg.Option = client;
				arg.FullString = fullString;
				arg.Args = args;

				arguments.Add(arg);

				try
				{
					var methodInfo = plugin.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					var parameters = methodInfo.GetParameters();

					if (parameters.Length > 0)
					{
						for (int i = 1; i < parameters.Length; i++)
						{
							arguments.Add(null);
						}
					}

					result = arguments.ToArray();

					if (HookCaller.CallStaticHook("OnCarbonCommand", arg) == null)
					{
						methodInfo?.Invoke(plugin, result);
					}
				}
				catch (Exception ex) { if (plugin is CarbonPlugin carbonPlugin) carbonPlugin.LogError("Error", ex.InnerException ?? ex); }
			}
			catch (TargetParameterCountException) { }
			catch (Exception ex) { if (plugin is CarbonPlugin carbonPlugin) carbonPlugin.LogError("Error", ex.InnerException ?? ex); }

			Pool.FreeList(ref arguments);
			if (result != null) Pool.Free(ref result);
		}, skipOriginal, help, reference, permissions, groups, authLevel, cooldown);
	}
	public static void AddConsoleCommand(string name, IMetadata plugin, Func<Arg, bool> callback, bool skipOriginal = true, string help = null, object reference = null, string[] permissions = null, string[] groups = null, int authLevel = -1, int cooldown = 0)
	{
		AddConsoleCommand(name, plugin, (player, cmd, args) =>
		{
			var arguments = Pool.GetList<object>();
			var result = (object[])null;

			try
			{
				var fullString = args == null || args.Length == 0 ? string.Empty : string.Join(" ", args);
				var client = player == null ? Option.Unrestricted : Option.Client;
				var arg = FormatterServices.GetUninitializedObject(typeof(Arg)) as Arg;
				if (player != null) client = client.FromConnection(player.net.connection);
				client.FromRcon = FromRcon;
				arg.Option = client;
				arg.FullString = fullString;
				arg.Args = args;

				arguments.Add(arg);
				result = arguments.ToArray();

				if (HookCaller.CallStaticHook("OnCarbonCommand", arg) == null)
				{
					callback.Invoke(arg);
				}
			}
			catch (TargetParameterCountException) { }
			catch (Exception ex) { if (plugin is CarbonPlugin carbonPlugin) carbonPlugin.LogError("Error", ex.InnerException ?? ex); }

			Pool.FreeList(ref arguments);
			if (result != null) Pool.Free(ref result);
		}, skipOriginal, help, reference, permissions, groups, authLevel, cooldown);
	}

	public static void RemoveChatCommand(string name, IMetadata plugin = null)
	{
		Community.Runtime.AllChatCommands.RemoveAll(x => x.Name == name && (plugin == null || x.Plugin == plugin));
	}
	public static void RemoveConsoleCommand(string name, IMetadata plugin = null)
	{
		Community.Runtime.AllConsoleCommands.RemoveAll(x => x.Name == name && (plugin == null || x.Plugin == plugin));
	}

	#endregion

	#region Internals

	public override void IInit()
	{
		base.IInit();

		Persistence = new GameObject($"Script_{Name}").AddComponent<Persistence>();
		UnityEngine.Object.DontDestroyOnLoad(Persistence.gameObject);
	}
	public override void ILoadConfig()
	{
		LoadConfig();
	}
	public override void ILoadDefaultMessages()
	{
		CallHook("LoadDefaultMessages");
	}

	#endregion

	#region Plugins

	public static bool Exists(string name)
	{
		return Community.Runtime.Plugins.Plugins.Any(x => x.Name == name);
	}

	public static IPlugin Find(string name)
	{
		name = name.Replace(" ", "");

		foreach (var mod in Loader.LoadedMods)
		{
			foreach (var plugin in mod.Plugins)
			{
				if (plugin.Name.Replace(" ", "").Replace(".", "") == name) return plugin;
			}
		}

		return null;
	}

	public static IPlugin[] GetAll()
	{
		var list = Pool.GetList<IPlugin>();
		foreach (var mod in Loader.LoadedMods)
		{
			list.AddRange(mod.Plugins);
		}

		var result = list.ToArray();
		Pool.FreeList(ref list);
		return result;
	}

	#endregion

	public new string ToString()
	{
		return $"{Name} v{Version} by {Author}";
	}

}

public class Persistence : FacepunchBehaviour { }
