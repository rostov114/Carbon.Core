﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using API.Hooks;
using Carbon.Base.Interfaces;
using Carbon.Components;
using Carbon.Extensions;
using Carbon.Hooks;
using Carbon.Plugins;
using Facepunch;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Plugins;
using UnityEngine;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Core;

public class CorePlugin : CarbonPlugin
{
	public static Dictionary<string, string> OrderedFiles { get; } = new Dictionary<string, string>();

	public static void RefreshOrderedFiles()
	{
		OrderedFiles.Clear();

		foreach (var file in OsEx.Folder.GetFilesWithExtension(Defines.GetScriptFolder(), "cs"))
		{
			OrderedFiles.Add(Path.GetFileNameWithoutExtension(file), file);
		}
	}

	public static string GetPluginPath(string shortName)
	{
		foreach (var file in OrderedFiles)
		{
			if (file.Key == shortName) return file.Value;
		}

		return null;
	}

	public override void IInit()
	{
		Hooks = new List<string>()
		{
			"OnEntitySpawned",
			"OnEntityDeath",
			"OnEntityKill",
			"IOnPlayerConnected",
			"IOnUserApprove",
			"IOnBasePlayerAttacked"
		};

		base.IInit();

		foreach (var player in BasePlayer.activePlayerList)
		{
			permission.RefreshUser(player);
		}

		timer.Every(5f, () =>
		{
			if (!Logger._file._hasInit || Logger._file._buffer.Count == 0 || CommunityCommon.CommonRuntime.Config.LogFileMode != 1) return;
			Logger._file._flush();
		});
	}

	private void OnPluginLoaded(Plugin plugin)
	{
	}
	private void OnPluginUnloaded(Plugin plugin)
	{
	}
	private void OnEntitySpawned(BaseEntity entity)
	{
		Entities.AddMap(entity);
	}
	private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
	{
		Entities.RemoveMap(entity);
	}
	private void OnEntityKill(BaseEntity entity)
	{
		Entities.RemoveMap(entity);
	}

	#region Internal Hooks

	private void IOnPlayerConnected(BasePlayer player)
	{
		permission.RefreshUser(player);
		Interface.CallHook("OnPlayerConnected", player);
	}
	private object IOnUserApprove(Connection connection)
	{
		var username = connection.username;
		var text = connection.userid.ToString();
		var obj = Regex.Replace(connection.ipaddress, global::Oxide.Game.Rust.Libraries.Player.ipPattern, "");
		var authLevel = connection.authLevel;

		var canClient = Interface.CallHook("CanClientLogin", connection);
		var canUser = Interface.CallHook("CanUserLogin", username, text, obj);

		var obj4 = (canClient == null) ? canUser : canClient;
		if (obj4 is string || (obj4 is bool && !(bool)obj4))
		{
			ConnectionAuth.Reject(connection, (obj4 is string) ? obj4.ToString() : "Connection was rejected", null);
			return true;
		}

		if (Interface.CallHook("OnUserApprove", connection) != null)
			return Interface.CallHook("OnUserApproved", username, text, obj);

		return null;
	}
	private object IOnBasePlayerAttacked(BasePlayer basePlayer, HitInfo hitInfo)
	{
		if (!CommunityCommon.IsServerFullyInitializedCache || basePlayer == null || hitInfo == null || basePlayer.IsDead() || basePlayer is NPCPlayer)
		{
			return null;
		}

		if (Interface.CallHook("OnEntityTakeDamage", basePlayer, hitInfo) != null)
		{
			return true;
		}

		try
		{
			if (!basePlayer.IsDead())
			{
				basePlayer.DoHitNotify(hitInfo);
			}

			if (basePlayer.isServer)
			{
				basePlayer.Hurt(hitInfo);
			}
		}
		catch { }

		return true;
	}

	#endregion

	public static void Reply(object message, ConsoleSystem.Arg arg)
	{
		if (arg != null && arg.Player() != null)
		{
			arg.Player().SendConsoleCommand($"echo {message}");
			return;
		}
		Logger.Log(message);
	}

	#region Commands

	#region App

	[ConsoleCommand("exit", "Completely unloads Carbon from the game, rendering it fully vanilla.")]
	private void Exit(ConsoleSystem.Arg arg)
	{
		Supervisor.ASM.UnloadModule("Carbon.dll", false);
	}

	[ConsoleCommand("reboot", "Unloads Carbon from the game and then loads it back again with the latest version changes (if any).")]
	private void Reboot(ConsoleSystem.Arg arg)
	{
		Supervisor.ASM.UnloadModule("Carbon.dll", true);
	}

	[ConsoleCommand("version", "Returns currently loaded version of Carbon.")]
	private void GetVersion(ConsoleSystem.Arg arg)
	{
		Reply($"Carbon v{CommunityCommon.Version}", arg);
	}

	[ConsoleCommand("build", "Returns current version of Carbon's Assembly.")]
	private void GetBuild(ConsoleSystem.Arg arg)
	{
		Reply($"{CommunityCommon.InformationalVersion}", arg);
	}

	[ConsoleCommand("plugins", "Prints the list of mods and their loaded plugins.")]
	private void Plugins(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		var mode = arg.HasArgs(1) ? arg.Args[0] : null;

		switch (mode)
		{
			case "-j":
			case "--j":
			case "-json":
			case "--json":
				Reply(JsonConvert.SerializeObject(Loader.LoadedMods, Formatting.Indented), arg);
				break;

			default:
				var body = new StringTable("#", "Mod", "Author", "Version", "Hook Time", "Compile Time");
				var count = 1;

				foreach (var mod in Loader.LoadedMods)
				{
					body.AddRow($"{count:n0}", $"{mod.Name}{(mod.Plugins.Count > 1 ? $" ({mod.Plugins.Count:n0})" : "")}", "", "", "", "");

					foreach (var plugin in mod.Plugins)
					{
						body.AddRow($"", plugin.Name, plugin.Author, $"v{plugin.Version}", $"{plugin.TotalHookTime:0.0}s", $"{plugin.CompileTime:0}ms");
					}

					count++;
				}

				Reply(body.ToStringMinimal(), arg);
				break;
		}
	}

	[ConsoleCommand("pluginsfailed", "Prints the list of plugins that failed to load (most likely due to compilation issues).")]
	private void PluginsFailed(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		var mode = arg.HasArgs(1) ? arg.Args[0] : null;

		switch (mode)
		{
			case "-j":
			case "--j":
			case "-json":
			case "--json":
				Reply(JsonConvert.SerializeObject(Loader.FailedMods, Formatting.Indented), arg);
				break;

			default:
				var result = string.Empty;
				var count = 1;

				foreach (var mod in Loader.FailedMods)
				{
					result += $"{count:n0}. {mod.File}\n";

					foreach (var error in mod.Errors)
					{
						result += $" {error}\n";
					}

					result += "\n";
					count++;
				}

				Reply(result, arg);
				break;
		}
	}

	[ConsoleCommand("update", "Downloads, updates, saves the server and patches Carbon at runtime. (Eg. c.update win develop, c.update unix prod)")]
	private void Update(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		Carbon.Core.Updater.DoUpdate((bool result) =>
		{
			if (!result)
			{
				Logger.Error($"Unknown error while updating Carbon");
				return;
			}
			HookCaller.CallStaticHook("OnServerSave");
			Supervisor.ASM.UnloadModule("Carbon.dll", true);
		});
	}

	#endregion

#if DEBUG
	[ConsoleCommand("assembly", "Debug stuff.")]
	private void AssemblyInfo(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		int count = 0;
		StringTable body = new StringTable("#", "Assembly", "Version", "Dynamic", "Location");
		foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			body.AddRow($"{count++:n0}", assembly.GetName().Name, assembly.GetName().Version, assembly.IsDynamic, (assembly.IsDynamic) ? string.Empty : assembly.Location);
		Reply(body.ToStringMinimal(), arg);
	}
#endif

	#region Conditionals

	[ConsoleCommand("addconditional", "Adds a new conditional compilation symbol to the compiler.")]
	private void AddConditional(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		var value = arg.Args[0];

		if (!CommunityCommon.CommonRuntime.Config.ConditionalCompilationSymbols.Contains(value))
		{
			CommunityCommon.CommonRuntime.Config.ConditionalCompilationSymbols.Add(value);
			CommunityCommon.CommonRuntime.SaveConfig();
			Reply($"Added conditional '{value}'.", arg);
		}
		else
		{
			Reply($"Conditional '{value}' already exists.", arg);
		}

		foreach (var mod in Loader.LoadedMods)
		{
			var plugins = Pool.GetList<RustPlugin>();
			plugins.AddRange(mod.Plugins);

			foreach (var plugin in plugins)
			{
				if (plugin.HasConditionals)
				{
					plugin._processor_instance.Dispose();
					plugin._processor_instance.Execute();
					mod.Plugins.Remove(plugin);
				}
			}

			Pool.FreeList(ref plugins);
		}
	}

	[ConsoleCommand("remconditional", "Removes an existent conditional compilation symbol from the compiler.")]
	private void RemoveConditional(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		var value = arg.Args[0];

		if (CommunityCommon.CommonRuntime.Config.ConditionalCompilationSymbols.Contains(value))
		{
			CommunityCommon.CommonRuntime.Config.ConditionalCompilationSymbols.Remove(value);
			CommunityCommon.CommonRuntime.SaveConfig();
			Reply($"Removed conditional '{value}'.", arg);
		}
		else
		{
			Reply($"Conditional '{value}' does not exist.", arg);
		}

		foreach (var mod in Loader.LoadedMods)
		{
			var plugins = Pool.GetList<RustPlugin>();
			plugins.AddRange(mod.Plugins);

			foreach (var plugin in plugins)
			{
				if (plugin.HasConditionals)
				{
					plugin._processor_instance.Dispose();
					plugin._processor_instance.Execute();
					mod.Plugins.Remove(plugin);
				}
			}

			Pool.FreeList(ref plugins);
		}
	}

	[ConsoleCommand("conditionals", "Prints a list of all conditional compilation symbols used by the compiler.")]
	private void Conditionals(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		Reply($"Conditionals ({CommunityCommon.CommonRuntime.Config.ConditionalCompilationSymbols.Count:n0}): {CommunityCommon.CommonRuntime.Config.ConditionalCompilationSymbols.ToArray().ToString(", ", " and ")}", arg);
	}

	#endregion

	#region Hooks

	[ConsoleCommand("hooks", "Prints the list of all hooks that have been called at least once.")]
	private void HookInfo(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		StringTable body = new StringTable("#", "Name", "Hook", "Id", "Type", "Status", "Total", "Sub");
		int count = 0, success = 0, warning = 0, failure = 0;

		string option1 = arg.GetString(0, null);
		string option2 = arg.GetString(1, null);

		switch (option1)
		{
			case "loaded":
				{
					IEnumerable<HookEx> hooks;

					switch (option2)
					{
						case "--patch":
							hooks = CommunityCommon.CommonRuntime.HookManager.Patches.Where(x => !x.IsHidden);
							break;

						case "--static":
							hooks = CommunityCommon.CommonRuntime.HookManager.StaticHooks.Where(x => !x.IsHidden);
							break;

						case "--dynamic":
							hooks = CommunityCommon.CommonRuntime.HookManager.DynamicHooks.Where(x => !x.IsHidden);
							break;

						// case "--failed":
						// 	hooks = CommunityCommon.Runtime.HookManager.StaticHooks
						// 		.Where(x => !x.IsHidden && x.Status == HookState.Failure);
						// 	hooks = hooks.Concat(CommunityCommon.Runtime.HookManager.DynamicHooks
						// 		.Where(x => !x.IsHidden && x.Status == HookState.Failure));
						// 	break;

						// case "--warning":
						// 	hooks = CommunityCommon.Runtime.HookManager.StaticHooks
						// 		.Where(x => !x.IsHidden && x.Status == HookState.Warning);
						// 	hooks = hooks.Concat(CommunityCommon.Runtime.HookManager.DynamicHooks
						// 		.Where(x => !x.IsHidden && x.Status == HookState.Warning));
						// 	break;

						// case "--success":
						// 	hooks = CommunityCommon.Runtime.HookManager.StaticHooks
						// 		.Where(x => !x.IsHidden && x.Status == HookState.Success);
						// 	hooks = hooks.Concat(CommunityCommon.Runtime.HookManager.DynamicHooks
						// 		.Where(x => !x.IsHidden && x.Status == HookState.Success));
						// 	break;

						default:
							hooks = CommunityCommon.CommonRuntime.HookManager.Patches.Where(x => !x.IsHidden);
							hooks = hooks.Concat(CommunityCommon.CommonRuntime.HookManager.StaticHooks.Where(x => !x.IsHidden));
							hooks = hooks.Concat(CommunityCommon.CommonRuntime.HookManager.DynamicHooks.Where(x => !x.IsHidden));
							break;
					}

					foreach (var mod in hooks.OrderBy(x => x.HookFullName))
					{
						if (mod.Status == HookState.Failure) failure++;
						if (mod.Status == HookState.Success) success++;
						if (mod.Status == HookState.Warning) warning++;

						body.AddRow(
							$"{count++:n0}",
							mod.HookFullName,
							mod.HookName,
							mod.Identifier.Substring(mod.Identifier.Length - 6),
							mod.IsStaticHook ? "Static" : mod.IsPatch ? "Patch" : "Dynamic",
							mod.Status,
							//$"{HookCaller.GetHookTime(mod.HookName)}ms",
							$"{HookCaller.GetHookTotalTime(mod.HookName)}ms",
							(mod.IsStaticHook) ? "N/A" : $"{CommunityCommon.CommonRuntime.HookManager.GetHookSubscriberCount(mod.Identifier),3}"
						);
					}

					Reply($"total:{count} success:{success} warning:{warning} failed:{failure}"
						+ Environment.NewLine + Environment.NewLine + body.ToStringMinimal(), arg);
					break;
				}

			default: // list installed
				{
					IEnumerable<HookEx> hooks;

					switch (option1)
					{
						case "--patch":
							hooks = CommunityCommon.CommonRuntime.HookManager.InstalledPatches.Where(x => !x.IsHidden);
							break;

						case "--static":
							hooks = CommunityCommon.CommonRuntime.HookManager.InstalledStaticHooks.Where(x => !x.IsHidden);
							break;

						case "--dynamic":
							hooks = CommunityCommon.CommonRuntime.HookManager.InstalledDynamicHooks.Where(x => !x.IsHidden);
							break;

						default:
							hooks = CommunityCommon.CommonRuntime.HookManager.InstalledPatches.Where(x => !x.IsHidden);
							hooks = hooks.Concat(CommunityCommon.CommonRuntime.HookManager.InstalledStaticHooks.Where(x => !x.IsHidden));
							hooks = hooks.Concat(CommunityCommon.CommonRuntime.HookManager.InstalledDynamicHooks.Where(x => !x.IsHidden));
							break;
					}

					foreach (var mod in hooks.OrderBy(x => x.HookFullName))
					{
						if (mod.Status == HookState.Failure) failure++;
						if (mod.Status == HookState.Success) success++;
						if (mod.Status == HookState.Warning) warning++;

						body.AddRow(
							$"{count++:n0}",
							mod.HookFullName,
							mod.HookName,
							mod.Identifier.Substring(mod.Identifier.Length - 6),
							mod.IsStaticHook ? "Static" : mod.IsPatch ? "Patch" : "Dynamic",
							mod.Status,
							//$"{HookCaller.GetHookTime(mod.HookName)}ms",
							$"{HookCaller.GetHookTotalTime(mod.HookName)}ms",
							(mod.IsStaticHook) ? "N/A" : $"{CommunityCommon.CommonRuntime.HookManager.GetHookSubscriberCount(mod.Identifier),3}"
						);
					}

					Reply($"total:{count} success:{success} warning:{warning} failed:{failure}"
						+ Environment.NewLine + Environment.NewLine + body.ToStringMinimal(), arg);
					break;
				}
		}
	}

	#endregion

	#region Config

	[ConsoleCommand("loadconfig", "Loads Carbon config from file.")]
	private void CarbonLoadConfig(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin() || CommunityCommon.CommonRuntime == null) return;

		CommunityCommon.CommonRuntime.LoadConfig();

		Reply("Loaded Carbon config.", arg);
	}

	[ConsoleCommand("saveconfig", "Saves Carbon config to file.")]
	private void CarbonSaveConfig(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin() || CommunityCommon.CommonRuntime == null) return;

		CommunityCommon.CommonRuntime.SaveConfig();

		Reply("Saved Carbon config.", arg);
	}

	[CommandVar("autoupdate", "Updates carbon hooks on boot.", true)]
	private bool AutoUpdate { get { return CommunityCommon.CommonRuntime.Config.AutoUpdate; } set { CommunityCommon.CommonRuntime.Config.AutoUpdate = value; CommunityCommon.CommonRuntime.SaveConfig(); } }

	[CommandVar("modding", "Mark this server as modded or not.", true)]
	private bool Modding { get { return CommunityCommon.CommonRuntime.Config.IsModded; } set { CommunityCommon.CommonRuntime.Config.IsModded = value; CommunityCommon.CommonRuntime.SaveConfig(); } }

	[CommandVar("tag", "Displays this server in the browser list with the 'carbon' tag.", true)]
	private bool CarbonTag { get { return CommunityCommon.CommonRuntime.Config.CarbonTag; } set { CommunityCommon.CommonRuntime.Config.CarbonTag = value; CommunityCommon.CommonRuntime.SaveConfig(); } }

	[CommandVar("debug", "The level of debug logging for Carbon. Helpful for very detailed logs in case things break. (Set it to -1 to disable debug logging.)", true)]
	private int CarbonDebug { get { return CommunityCommon.CommonRuntime.Config.LogVerbosity; } set { CommunityCommon.CommonRuntime.Config.LogVerbosity = value; CommunityCommon.CommonRuntime.SaveConfig(); } }

	[CommandVar("logfiletype", "The mode for writing the log to file. (0=disabled, 1=saves updates every 5 seconds, 2=saves immediately)", true)]
	private int LogFileType { get { return CommunityCommon.CommonRuntime.Config.LogFileMode; } set { CommunityCommon.CommonRuntime.Config.LogFileMode = Mathf.Clamp(value, 0, 2); CommunityCommon.CommonRuntime.SaveConfig(); } }

	[CommandVar("hooktimetracker", "For debugging purposes, this will track the time of hooks and gives a total.", true)]
	private bool HookTimeTracker { get { return CommunityCommon.CommonRuntime.Config.HookTimeTracker; } set { CommunityCommon.CommonRuntime.Config.HookTimeTracker = value; CommunityCommon.CommonRuntime.SaveConfig(); } }

	[CommandVar("hookvalidation", "Prints a warning when plugins contain Oxide hooks that aren't available yet in Carbon.", true)]
	private bool HookValidation { get { return CommunityCommon.CommonRuntime.Config.HookValidation; } set { CommunityCommon.CommonRuntime.Config.HookValidation = value; CommunityCommon.CommonRuntime.SaveConfig(); } }

	[CommandVar("entitymapbuffersize", "The entity map buffer size. Gets applied on Carbon reboot.", true)]
	private int EntityMapBufferSize { get { return CommunityCommon.CommonRuntime.Config.EntityMapBufferSize; } set { CommunityCommon.CommonRuntime.Config.EntityMapBufferSize = value; CommunityCommon.CommonRuntime.SaveConfig(); } }

	[CommandVar("language", "Server language used by the Language API.", true)]
	private string Language { get { return CommunityCommon.CommonRuntime.Config.Language; } set { CommunityCommon.CommonRuntime.Config.Language = value; CommunityCommon.CommonRuntime.SaveConfig(); } }

#if WIN
	[CommandVar("consoleinfo", "Show the Windows-only Carbon information at the bottom of the console.", true)]
	private bool ConsoleInfo
	{
		get { return CommunityCommon.CommonRuntime.Config.ShowConsoleInfo; }
		set
		{
			CommunityCommon.CommonRuntime.Config.ShowConsoleInfo = value;

			if (value)
			{
				CommunityCommon.CommonRuntime.RefreshConsoleInfo();
			}
			else
			{
				if (ServerConsole.Instance != null && ServerConsole.Instance.input != null)
				{
					ServerConsole.Instance.input.statusText = new string[3];
				}
			}
		}
	}
#endif

	#endregion

	#region Commands

	[ConsoleCommand("find", "Searches through Carbon-processed console commands.")]
	private void Find(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		var body = new StringTable("Command", "Value", "Help");
		var filter = arg.Args != null && arg.Args.Length > 0 ? arg.Args[0] : null;

		foreach (var command in CommunityCommon.CommonRuntime.AllConsoleCommands)
		{
			if (!string.IsNullOrEmpty(filter) && !command.Command.Contains(filter)) continue;

			var value = " ";

			if (command.Reference != null)
			{
				if (command.Reference is FieldInfo field) value = field.GetValue(command.Plugin)?.ToString();
				else if (command.Reference is PropertyInfo property) value = property.GetValue(command.Plugin)?.ToString();
			}

			body.AddRow(command.Command, value, command.Help);
		}

		Reply($"Console Commands:\n{body.ToStringMinimal()}", arg);
	}

	[ConsoleCommand("findchat", "Searches through Carbon-processed chat commands.")]
	private void FindChat(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		var body = new StringTable("Command", "Help");
		var filter = arg.Args != null && arg.Args.Length > 0 ? arg.Args[0] : null;

		foreach (var command in CommunityCommon.CommonRuntime.AllChatCommands)
		{
			if (!string.IsNullOrEmpty(filter) && !command.Command.Contains(filter)) continue;

			body.AddRow(command.Command, command.Help);
		}

		Reply($"Chat Commands:\n{body.ToStringMinimal()}", arg);
	}

	#endregion

	#region Report

	[ConsoleCommand("report", "Reloads all current plugins, and returns a report based on them at the output path.")]
	private void Report(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		new Carbon.Components.Report().Init();
	}

	#endregion

	#region Modules

	[ConsoleCommand("setmodule", "Enables or disables Carbon modules. Visit root/carbon/modules and use the config file names as IDs.")]
	private void SetModule(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin() || !arg.HasArgs(2)) return;

		var hookable = CommunityCommon.CommonRuntime.ModuleProcessor.Modules.FirstOrDefault(x => x.Name == arg.Args[0]);
		var module = hookable.To<IModule>();

		if (module == null)
		{
			Reply($"Couldn't find that module.", arg);
			return;
		}

		var previousEnabled = module.GetEnabled();
		var newEnabled = arg.Args[1].ToBool();

		if (previousEnabled != newEnabled)
		{
			module.SetEnabled(newEnabled);
			module.Save();
		}

		Reply($"{module.Name} marked {(module.GetEnabled() ? "enabled" : "disabled")}.", arg);
	}

	[ConsoleCommand("saveallmodules", "Saves the configs and data files of all available modules.")]
	private void SaveAllModules(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		foreach (var hookable in CommunityCommon.CommonRuntime.ModuleProcessor.Modules)
		{
			var module = hookable.To<IModule>();
			module.Save();
		}

		Reply($"Saved {CommunityCommon.CommonRuntime.ModuleProcessor.Modules.Count:n0} module configs and data files.", arg);
	}

	[ConsoleCommand("savemoduleconfig", "Saves Carbon module config & data file.")]
	private void SaveModuleConfig(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin() || !arg.HasArgs(1)) return;

		var hookable = CommunityCommon.CommonRuntime.ModuleProcessor.Modules.FirstOrDefault(x => x.Name == arg.Args[0]);
		var module = hookable.To<IModule>();

		if (module == null)
		{
			Reply($"Couldn't find that module.", arg);
			return;
		}

		module.Save();

		Reply($"Saved '{module.Name}' module config & data file.", arg);
	}

	[ConsoleCommand("loadmoduleconfig", "Loads Carbon module config & data file.")]
	private void LoadModuleConfig(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin() || !arg.HasArgs(1)) return;

		var hookable = CommunityCommon.CommonRuntime.ModuleProcessor.Modules.FirstOrDefault(x => x.Name == arg.Args[0]);
		var module = hookable.To<IModule>();

		if (module == null)
		{
			Reply($"Couldn't find that module.", arg);
			return;
		}

		if (module.GetEnabled()) module.SetEnabled(false);
		module.Load();
		if (module.GetEnabled()) module.OnEnableStatus();

		Reply($"Reloaded '{module.Name}' module config.", arg);
	}

	#endregion

	#region Mod & Plugin Loading

	[ConsoleCommand("reload", "Reloads all or specific mods / plugins. E.g 'c.reload *' to reload everything.")]
	private void Reload(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin() || !arg.HasArgs(1)) return;

		RefreshOrderedFiles();

		var name = arg.Args[0];
		switch (name)
		{
			case "*":
				CommunityCommon.CommonRuntime.ClearPlugins();
				CommunityCommon.CommonRuntime.ReloadPlugins();
				break;

			default:
				var path = GetPluginPath(name);

				if (!string.IsNullOrEmpty(path))
				{
					CommunityCommon.CommonRuntime.ScriptProcessor.ClearIgnore(path);
					CommunityCommon.CommonRuntime.ScriptProcessor.Prepare(name, path);
					return;
				}

				foreach (var mod in Loader.LoadedMods)
				{
					var plugins = Pool.GetList<RustPlugin>();
					plugins.AddRange(mod.Plugins);

					foreach (var plugin in plugins)
					{
						if (plugin.Name == name)
						{
							plugin._processor_instance.Dispose();
							plugin._processor_instance.Execute();
							mod.Plugins.Remove(plugin);
						}
					}

					Pool.FreeList(ref plugins);
				}
				break;
		}
	}

	[ConsoleCommand("load", "Loads all mods and/or plugins. E.g 'c.load *' to load everything you've unloaded.")]
	private void LoadPlugin(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin() || !arg.HasArgs(1)) return;

		RefreshOrderedFiles();

		var name = arg.Args[0];
		switch (name)
		{
			case "*":
				//
				// Scripts
				//
				{
					var tempList = Pool.GetList<string>();
					tempList.AddRange(CommunityCommon.CommonRuntime.ScriptProcessor.IgnoreList);
					CommunityCommon.CommonRuntime.ScriptProcessor.IgnoreList.Clear();

					foreach (var plugin in tempList)
					{
						CommunityCommon.CommonRuntime.ScriptProcessor.Prepare(plugin, plugin);
					}
					Pool.FreeList(ref tempList);
				}
				break;

			default:
				var path = GetPluginPath(name);
				if (!string.IsNullOrEmpty(path))
				{
					CommunityCommon.CommonRuntime.ScriptProcessor.ClearIgnore(path);
					CommunityCommon.CommonRuntime.ScriptProcessor.Prepare(path);
					return;
				}

				/*var module = BaseModule.GetModule<DRMModule>();
				foreach (var drm in module.Config.DRMs)
				{
					foreach (var entry in drm.Entries)
					{
						if (entry.Id == name) drm.RequestEntry(entry);
					}
				}*/

				break;
		}
	}

	[ConsoleCommand("unload", "Unloads all mods and/or plugins. E.g 'c.unload *' to unload everything. They'll be marked as 'ignored'.")]
	private void UnloadPlugin(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin() || !arg.HasArgs(1)) return;

		RefreshOrderedFiles();

		var name = arg.Args[0];
		switch (name)
		{
			case "*":
				//
				// Scripts
				//
				{
					var tempList = Pool.GetList<string>();
					tempList.AddRange(CommunityCommon.CommonRuntime.ScriptProcessor.IgnoreList);
					CommunityCommon.CommonRuntime.ScriptProcessor.IgnoreList.Clear();
					CommunityCommon.CommonRuntime.ScriptProcessor.Clear();

					foreach (var plugin in tempList)
					{
						CommunityCommon.CommonRuntime.ScriptProcessor.Ignore(plugin);
					}
					Pool.FreeList(ref tempList);
				}

				//
				// Web-Scripts
				//
				{
					var tempList = Pool.GetList<string>();
					tempList.AddRange(CommunityCommon.CommonRuntime.WebScriptProcessor.IgnoreList);
					CommunityCommon.CommonRuntime.WebScriptProcessor.IgnoreList.Clear();
					CommunityCommon.CommonRuntime.WebScriptProcessor.Clear();

					foreach (var plugin in tempList)
					{
						CommunityCommon.CommonRuntime.WebScriptProcessor.Ignore(plugin);
					}
					Pool.FreeList(ref tempList);
				}
				break;

			default:
				var path = GetPluginPath(name);
				if (!string.IsNullOrEmpty(path))
				{
					CommunityCommon.CommonRuntime.ScriptProcessor.Ignore(path);
					CommunityCommon.CommonRuntime.WebScriptProcessor.Ignore(path);
				}

				foreach (var mod in Loader.LoadedMods)
				{
					var plugins = Pool.GetList<RustPlugin>();
					plugins.AddRange(mod.Plugins);

					foreach (var plugin in plugins)
					{
						if (plugin.Name == name)
						{
							plugin._processor_instance.Dispose();
							mod.Plugins.Remove(plugin);
						}
					}

					Pool.FreeList(ref plugins);
				}
				break;
		}
	}

	#endregion

	#region Permissions

	[ConsoleCommand("grant", "Grant one or more permissions to users or groups. Do 'c.grant' for syntax info.")]
	private void Grant(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		void PrintWarn()
		{
			Reply($"Syntax: c.grant <user|group> <name|id> <perm>", arg);
		}

		if (!arg.HasArgs(3))
		{
			PrintWarn();
			return;
		}

		var action = arg.Args[0];
		var name = arg.Args[1];
		var perm = arg.Args[2];
		var user = permission.FindUser(name);

		switch (action)
		{
			case "user":
				if (permission.GrantUserPermission(user.Key, perm, null))
				{
					Reply($"Granted user '{user.Value.LastSeenNickname}' permission '{perm}'", arg);
				}
				break;

			case "group":
				if (permission.GrantGroupPermission(name, perm, null))
				{
					Reply($"Granted group '{name}' permission '{perm}'", arg);
				}
				break;

			default:
				PrintWarn();
				break;
		}
	}

	[ConsoleCommand("revoke", "Revoke one or more permissions from users or groups. Do 'c.revoke' for syntax info.")]
	private void Revoke(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		void PrintWarn()
		{
			Reply($"Syntax: c.revoke <user|group> <name|id> <perm>", arg);
		}

		if (!arg.HasArgs(3))
		{
			PrintWarn();
			return;
		}

		var action = arg.Args[0];
		var name = arg.Args[1];
		var perm = arg.Args[2];
		var user = permission.FindUser(name);

		switch (action)
		{
			case "user":
				if (permission.RevokeUserPermission(user.Key, perm))
				{
					Reply($"Revoked user '{user.Value?.LastSeenNickname}' permission '{perm}'", arg);
				}
				break;

			case "group":
				if (permission.RevokeGroupPermission(name, perm))
				{
					Reply($"Revoked group '{name}' permission '{perm}'", arg);
				}
				break;

			default:
				PrintWarn();
				break;
		}
	}

	[ConsoleCommand("show", "Displays information about a specific player or group (incl. permissions, groups and user list). Do 'c.show' for syntax info.")]
	private void Show(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		void PrintWarn()
		{
			Reply($"Syntax: c.show <groups|perms>", arg);
			Reply($"Syntax: c.show <group|user> <name|id>", arg);
		}

		if (!arg.HasArgs(1)) { PrintWarn(); return; }

		var action = arg.Args[0];

		switch (action)
		{
			case "user":
				{
					if (!arg.HasArgs(2)) { PrintWarn(); return; }

					var name = arg.Args[1];
					var user = permission.FindUser(name);

					if (user.Value == null)
					{
						Reply($"Couldn't find that user.", arg);
						return;
					}

					Reply($"User {user.Value.LastSeenNickname}[{user.Key}] found in {user.Value.Groups.Count:n0} groups:\n  {user.Value.Groups.Select(x => x).ToArray().ToString(", ", " and ")}", arg);
					Reply($"and has {user.Value.Perms.Count:n0} permissions:\n  {user.Value.Perms.Select(x => x).ToArray().ToString(", ", " and ")}", arg);
					break;
				}
			case "group":
				{
					if (!arg.HasArgs(2)) { PrintWarn(); return; }

					var name = arg.Args[1];

					if (!permission.GroupExists(name))
					{
						Reply($"Couldn't find that group.", arg);
						return;
					}

					var users = permission.GetUsersInGroup(name);
					var permissions = permission.GetGroupPermissions(name, false);
					Reply($"Group {name} has {users.Length:n0} users:\n  {users.Select(x => x).ToArray().ToString(", ", " and ")}", arg);
					Reply($"and has {permissions.Length:n0} permissions:\n  {permissions.Select(x => x).ToArray().ToString(", ", " and ")}", arg);
					break;
				}
			case "groups":
				{
					var groups = permission.GetGroups();
					if (groups.Count() == 0)
					{
						Reply($"Couldn't find any group.", arg);
						return;
					}

					Reply($"Groups:\n {String.Join(", ", groups)}", arg);
					break;
				}
			case "perms":
				{
					var perms = permission.GetPermissions();
					if (perms.Count() == 0)
					{
						Reply($"Couldn't find any permission.", arg);
					}

					Reply($"Permissions:\n {String.Join(", ", perms)}", arg);

					break;
				}

			default:
				PrintWarn();
				break;
		}
	}

	[ConsoleCommand("usergroup", "Adds or removes a player from a group. Do 'c.usergroup' for syntax info.")]
	private void UserGroup(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		void PrintWarn()
		{
			Reply($"Syntax: c.usergroup <add|remove> <player> <group>", arg);
		}

		if (!arg.HasArgs(3))
		{
			PrintWarn();
			return;
		}

		var action = arg.Args[0];
		var player = arg.Args[1];
		var group = arg.Args[2];

		var user = permission.FindUser(player);

		if (user.Value == null)
		{
			Reply($"Couldn't find that player.", arg);
			return;
		}

		if (!permission.GroupExists(group))
		{
			Reply($"Group '{group}' could not be found.", arg);
			return;
		}

		switch (action)
		{
			case "add":
				if (permission.UserHasGroup(user.Key, group))
				{
					Reply($"{user.Value.LastSeenNickname}[{user.Key}] is already in '{group}' group.", arg);
					return;
				}

				permission.AddUserGroup(user.Key, group);
				Reply($"Added {user.Value.LastSeenNickname}[{user.Key}] to '{group}' group.", arg);
				break;

			case "remove":
				if (!permission.UserHasGroup(user.Key, group))
				{
					Reply($"{user.Value.LastSeenNickname}[{user.Key}] isn't in '{group}' group.", arg);
					return;
				}

				permission.RemoveUserGroup(user.Key, group);
				Reply($"Removed {user.Value.LastSeenNickname}[{user.Key}] from '{group}' group.", arg);
				break;

			default:
				PrintWarn();
				break;
		}
	}

	[ConsoleCommand("group", "Adds or removes a group. Do 'c.group' for syntax info.")]
	private void Group(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		void PrintWarn()
		{
			Reply($"Syntax: c.group add <group> [<displayName>] [<rank>]", arg);
			Reply($"Syntax: c.group remove <group>", arg);
			Reply($"Syntax: c.group set <group> <title|rank> <value>", arg);
			Reply($"Syntax: c.group parent <group> [<parent>]", arg);
		}

		if (!arg.HasArgs(1)) { PrintWarn(); return; }

		var action = arg.Args[0];

		switch (action)
		{
			case "add":
				{
					if (!arg.HasArgs(2)) { PrintWarn(); return; }

					var group = arg.Args[1];

					if (permission.GroupExists(group))
					{
						Reply($"Group '{group}' already exists. To set any values for this group, use 'c.group set'.", arg);
						return;
					}

					if (permission.CreateGroup(group, arg.HasArgs(3) ? arg.Args[2] : group, arg.HasArgs(4) ? arg.Args[3].ToInt() : 0))
					{
						Reply($"Created '{group}' group.", arg);
					}
				}
				break;

			case "set":
				{
					if (!arg.HasArgs(4)) { PrintWarn(); return; }

					var group = arg.Args[1];

					if (!permission.GroupExists(group))
					{
						Reply($"Group '{group}' does not exists.", arg);
						return;
					}

					var set = arg.Args[2];
					var value = arg.Args[3];

					switch (set)
					{
						case "title":
							permission.SetGroupTitle(group, value);
							break;

						case "rank":
							permission.SetGroupRank(group, value.ToInt());
							break;
					}

					Reply($"Set '{group}' group.", arg);
				}
				break;

			case "remove":
				{
					if (!arg.HasArgs(2)) { PrintWarn(); return; }

					var group = arg.Args[1];

					if (permission.RemoveGroup(group)) Reply($"Removed '{group}' group.", arg);
					else Reply($"Couldn't remove '{group}' group.", arg);
				}
				break;

			case "parent":
				{
					if (!arg.HasArgs(3)) { PrintWarn(); return; }

					var group = arg.Args[1];
					var parent = arg.Args[2];

					if (permission.SetGroupParent(group, parent)) Reply($"Changed '{group}' group's parent to '{parent}'.", arg);
					else Reply($"Couldn't change '{group}' group's parent to '{parent}'.", arg);
				}
				break;

			default:
				PrintWarn();
				break;
		}
	}

	#endregion

	#endregion
}
