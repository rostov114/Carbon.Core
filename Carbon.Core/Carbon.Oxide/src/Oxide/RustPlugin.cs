﻿using System;
using System.IO;
using Carbon.Extensions;
using Carbon.Core;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Game.Rust.Libraries;
using UnityEngine;
using Carbon.Plugins;
using WebRequests = Oxide.Core.Libraries.WebRequests;
using Carbon.Contracts;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Oxide.Plugins;

public class RustPlugin : Plugin, IPlugin
{
	public PluginManager Manager { get; set; }

	public Permission permission { get; set; }
	public Lang lang { get; set; }
	public Command cmd { get; set; }
	public Server server { get; set; }
	public Plugins plugins { get; set; }
	public Timers timer { get; set; }
	public OxideMod mod { get; set; }
	public WebRequests webrequest { get; set; }
	public Game.Rust.Libraries.Rust rust { get; set; }
	public CovalencePlugin.Covalence covalence { get; set; }

	public Player Player { get { return rust.Player; } private set { } }
	public Server Server { get { return rust.Server; } private set { } }

	public RustPlugin()
	{
		Setup($"Core Plugin {RandomEx.GetRandomString(5)}", "Carbon Community", new VersionNumber(1, 0, 0), string.Empty);
	}

	public override void Setup(string name, string author, VersionNumber version, string description)
	{
		base.Setup(name, author, version, description);

		permission = Interface.Oxide.Permission;
		cmd = new Command();
		server = new Server();
		Manager = new PluginManager();
		plugins = new Plugins();
		timer = new Timers(this);
		lang = new Lang(this);
		mod = new OxideMod();
		rust = new Game.Rust.Libraries.Rust();
		webrequest = new WebRequests();
		covalence = new CovalencePlugin.Covalence();

		Persistence = new GameObject($"Script_{Name}").AddComponent<Persistence>();
		UnityEngine.Object.DontDestroyOnLoad(Persistence.gameObject);

		mod.Load();
	}

	public override void Dispose()
	{
		permission.UnregisterPermissions(this);

		timer.Clear();

		if (Persistence != null)
		{
			var go = Persistence.gameObject;
			UnityEngine.Object.DestroyImmediate(Persistence);
			UnityEngine.Object.Destroy(go);
		}

		base.Dispose();
	}

	#region Logging

	/// <summary>
	/// Outputs to the game's console a message with severity level 'NOTICE'.
	/// NOTE: Oxide compatibility layer.
	/// </summary>
	/// <param name="message"></param>
	public void Puts(object message)
		=> Carbon.Logger.Log($"[{Name}] {message}");

	/// <summary>
	/// Outputs to the game's console a message with severity level 'NOTICE'.
	/// NOTE: Oxide compatibility layer.
	/// </summary>
	/// <param name="message"></param>
	/// <param name="args"></param>
	public void Puts(object message, params object[] args)
		=> Carbon.Logger.Log($"[{Name}] {(args == null ? message : string.Format(message.ToString(), args))}");

	/// <summary>
	/// Outputs to the game's console a message with severity level 'WARNING'.
	/// NOTE: Oxide compatibility layer.
	/// </summary>
	/// <param name="message"></param>
	/// <param name="args"></param>
	public void PrintWarning(object format, params object[] args)
		=> Carbon.Logger.Warn($"[{Name}] {(args == null ? format : string.Format(format.ToString(), args))}");

	/// <summary>
	/// Outputs to the game's console a message with severity level 'ERROR'.
	/// NOTE: Oxide compatibility layer.
	/// </summary>
	/// <param name="message"></param>
	/// <param name="args"></param>
	public void PrintError(object format, params object[] args)
		=> Carbon.Logger.Error($"[{Name}] {(args == null ? format : string.Format(format.ToString(), args))}");

	/// <summary>
	/// Outputs to the game's console a message with severity level 'ERROR'.
	/// NOTE: Oxide compatibility layer.
	/// </summary>
	/// <param name="message"></param>
	public void RaiseError(object message)
		=> Carbon.Logger.Error($"[{Name}] {message}", null);

	protected void LogToFile(string filename, string text, Plugin plugin = null, bool timeStamp = true)
	{
		string logFolder;

		if (plugin == null)
		{
			var subFolder = Path.GetDirectoryName(filename);
			filename = Path.GetFileName(filename);
			logFolder = Path.Combine(Defines.GetLogsFolder(), subFolder) + (timeStamp ? $"-{DateTime.Now:yyyy-MM-dd}" : "") + ".txt";
		}
		else
		{
			logFolder = Path.Combine(Defines.GetLogsFolder(), plugin.Name);
			filename = plugin.Name.ToLower() + "_" + filename.ToLower() + (timeStamp ? $"-{DateTime.Now:yyyy-MM-dd}" : "") + ".txt";
		}

		if (!Directory.Exists(logFolder))
		{
			Directory.CreateDirectory(logFolder);
		}

		File.AppendAllText(Path.Combine(logFolder, Utility.CleanPath(filename)), (timeStamp ? $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}" : text) + Environment.NewLine);
	}

	#endregion

	#region Library

	public static T GetLibrary<T>(string name = null) where T : Library
	{
		return Interface.Oxide.GetLibrary<T>();
	}

	#endregion

	#region Printing

	protected void PrintToConsole(BasePlayer player, string format, params object[] args)
	{
		if (((player != null) ? player.net : null) != null)
		{
			player.SendConsoleCommand("echo " + ((args.Length != 0) ? string.Format(format, args) : format));
		}
	}
	protected void PrintToConsole(string format, params object[] args)
	{
		if (BasePlayer.activePlayerList.Count >= 1)
		{
			ConsoleNetwork.BroadcastToAllClients("echo " + ((args.Length != 0) ? string.Format(format, args) : format));
		}
	}
	protected void PrintToChat(BasePlayer player, string format, params object[] args)
	{
		if (((player != null) ? player.net : null) != null)
		{
			player.SendConsoleCommand("chat.add", 2, 0, (args.Length != 0) ? string.Format(format, args) : format);
		}
	}
	protected void PrintToChat(string format, params object[] args)
	{
		if (BasePlayer.activePlayerList.Count >= 1)
		{
			ConsoleNetwork.BroadcastToAllClients("chat.add", 2, 0, (args.Length != 0) ? string.Format(format, args) : format);
		}
	}

	protected void SendReply(ConsoleSystem.Arg arg, string format, params object[] args)
	{
		var connection = arg.Connection;
		var basePlayer = connection?.player as BasePlayer;
		var text = (args != null && args.Length != 0) ? string.Format(format, args) : format;

		if (((basePlayer != null) ? basePlayer.net : null) != null)
		{
			basePlayer.SendConsoleCommand($"echo {text}");
			return;
		}

		Puts(text, null);
	}
	protected void SendReply(BasePlayer player, string format, params object[] args)
	{
		PrintToChat(player, format, args);
	}
	protected void SendWarning(ConsoleSystem.Arg arg, string format, params object[] args)
	{
		var connection = arg.Connection;
		var basePlayer = connection?.player as BasePlayer;
		var text = (args != null && args.Length != 0) ? string.Format(format, args) : format;

		if (((basePlayer != null) ? basePlayer.net : null) != null)
		{
			basePlayer.SendConsoleCommand($"echo {text}");
			return;
		}

		Debug.LogWarning(text);
	}
	protected void SendError(ConsoleSystem.Arg arg, string format, params object[] args)
	{
		var connection = arg.Connection;
		var basePlayer = connection?.player as BasePlayer;
		var text = (args != null && args.Length != 0) ? string.Format(format, args) : format;

		if (((basePlayer != null) ? basePlayer.net : null) != null)
		{
			basePlayer.SendConsoleCommand($"echo {text}");
			return;
		}

		Debug.LogError(text);
	}

	#endregion

	protected void ForcePlayerPosition(BasePlayer player, Vector3 destination)
	{
		player.MovePosition(destination);

		if (!player.IsSpectating() || (double)Vector3.Distance(player.transform.position, destination) > 25.0)
		{
			player.ClientRPCPlayer(null, player, "ForcePositionTo", destination);
			return;
		}

		player.SendNetworkUpdate(BasePlayer.NetworkQueue.UpdateDistance);
	}

	#region Covalence

	protected void AddCovalenceCommand(string command, string callback, params string[] perms)
	{
		cmd.AddCovalenceCommand(command, this, callback, permissions: perms);

		if (perms != null)
		{
			foreach (var permission in perms)
			{
				if (!this.permission.PermissionExists(permission))
				{
					this.permission.RegisterPermission(permission, this);
				}
			}
		}
	}
	protected void AddCovalenceCommand(string[] commands, string callback, params string[] perms)
	{
		foreach (var command in commands)
		{
			cmd.AddCovalenceCommand(command, this, callback, permissions: perms);
		}

		if (perms != null)
		{
			foreach (var permission in perms)
			{
				if (!this.permission.PermissionExists(permission))
				{
					this.permission.RegisterPermission(permission, this);
				}
			}
		}
	}

	protected void AddUniversalCommand(string command, string callback, params string[] perms)
	{
		cmd.AddCovalenceCommand(command, this, callback, permissions: perms);

		if (perms != null)
		{
			foreach (var permission in perms)
			{
				if (!this.permission.PermissionExists(permission))
				{
					this.permission.RegisterPermission(permission, this);
				}
			}
		}
	}
	protected void AddUniversalCommand(string[] commands, string callback, params string[] perms)
	{
		foreach (var command in commands)
		{
			cmd.AddCovalenceCommand(command, this, callback, permissions: perms);
		}

		if (perms != null)
		{
			foreach (var permission in perms)
			{
				if (!this.permission.PermissionExists(permission))
				{
					this.permission.RegisterPermission(permission, this);
				}
			}
		}
	}

	#endregion

}
