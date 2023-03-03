using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Carbon;
using Carbon.Base;
using Carbon.Contracts;
using Carbon.Plugins;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Plugins;
using static ConsoleSystem;
using Pool = Facepunch.Pool;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Oxide.Game.Rust.Libraries
{
	public class Command : Library
	{
		public static bool FromRcon { get; set; }

		public void AddChatCommand(string command, IMetadata plugin, Action<BasePlayer, string, string[]> callback, bool skipOriginal = true, string help = null, object reference = null, string[] permissions = null, string[] groups = null, int authLevel = -1, int cooldown = 0)
		{
			CarbonPlugin.AddChatCommand(command, plugin, callback, skipOriginal, help, reference,permissions, groups, authLevel, cooldown);
		}
		public void AddChatCommand(string command, IMetadata plugin, string method, bool skipOriginal = true, string help = null, object reference = null, string[] permissions = null, string[] groups = null, int authLevel = -1, int cooldown = 0)
		{
			AddChatCommand(command, plugin, (player, cmd, args) =>
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
								if (ps.ElementAt(0).ParameterType == typeof(IPlayer)) argData.Add(player.AsIPlayer()); else argData.Add(player);
								result = argData.ToArray();
								break;
							}

						case 2:
							{
								if (ps.ElementAt(0).ParameterType == typeof(IPlayer)) argData.Add(player.AsIPlayer()); else argData.Add(player);
								argData.Add(cmd);
								result = argData.ToArray();
								break;
							}

						case 3:
							{
								if (ps.ElementAt(0).ParameterType == typeof(IPlayer)) argData.Add(player.AsIPlayer()); else argData.Add(player);
								argData.Add(cmd);
								argData.Add(args);
								result = argData.ToArray();
								break;
							}
					}

					m?.Invoke(plugin, result);
				}
				catch (Exception ex) { if (plugin is RustPlugin rustPlugin) rustPlugin.LogError("Error", ex.InnerException ?? ex); }

				if (argData != null) Pool.FreeList(ref argData);
				if (result != null) Pool.Free(ref result);
			}, skipOriginal, help, reference, permissions, groups, authLevel, cooldown);
		}
		public void AddConsoleCommand(string command, IMetadata plugin, Action<BasePlayer, string, string[]> callback, bool skipOriginal = true, string help = null, object reference = null, string[] permissions = null, string[] groups = null, int authLevel = -1, int cooldown = 0)
		{
			CarbonPlugin.AddConsoleCommand(command, plugin, callback, skipOriginal, help, reference, permissions, groups, authLevel, cooldown);
		}
		public void AddConsoleCommand(string command, IMetadata plugin, string method, bool skipOriginal = true, string help = null, object reference = null, string[] permissions = null, string[] groups = null, int authLevel = -1, int cooldown = 0)
		{
			CarbonPlugin.AddConsoleCommand(command, plugin, method, skipOriginal, help, reference, permissions, groups, authLevel, cooldown);
		}
		public void AddConsoleCommand(string command, IMetadata plugin, Func<Arg, bool> callback, bool skipOriginal = true, string help = null, object reference = null, string[] permissions = null, string[] groups = null, int authLevel = -1, int cooldown = 0)
		{
			CarbonPlugin.AddConsoleCommand(command, plugin, callback, skipOriginal, help, reference, permissions, groups, authLevel, cooldown);
		}
		public void AddCovalenceCommand(string command, IMetadata plugin, string method, bool skipOriginal = true, string help = null, object reference = null, string[] permissions = null, string[] groups = null, int authLevel = -1, int cooldown = 0)
		{
			AddChatCommand(command, plugin, method, skipOriginal, help, reference, permissions, groups, authLevel, cooldown);
			AddConsoleCommand(command, plugin, method, skipOriginal, help, reference, permissions, groups, authLevel, cooldown);
		}
		public void AddCovalenceCommand(string command, IMetadata plugin, Action<BasePlayer, string, string[]> callback, bool skipOriginal = true, string help = null, object reference = null, string[] permissions = null, string[] groups = null, int authLevel = -1, int cooldown = 0)
		{
			AddChatCommand(command, plugin, callback, skipOriginal, help, reference, permissions, groups, authLevel, cooldown);
			AddConsoleCommand(command, plugin, callback, skipOriginal, help, reference, permissions, groups, authLevel, cooldown );
		}

		public void RemoveChatCommand(string command, IMetadata plugin = null)
		{
			CarbonPlugin.RemoveChatCommand(command, plugin);
		}
		public void RemoveConsoleCommand(string command, IMetadata plugin = null)
		{
			CarbonPlugin.RemoveConsoleCommand(command, plugin);
		}
	}
}
