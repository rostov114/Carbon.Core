using System;
using Carbon.Components;
using Carbon.Core;
using Network.Visibility;
using Oxide.Core;

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

	public virtual void SetupMod(Loader.CarbonMod mod, string name, string author, VersionNumber version, string description)
	{
		_carbon = mod;
		Setup(name, author, version, description);
	}
	public virtual void Setup(string name, string author, VersionNumber version, string description)
	{
		Name = name;
		Version = version;
		Author = author;
		Description = description;

		Type = GetType();

		CuiHandler = new CUI.Handler();
	}

	#region CUI

	public CUI CreateCUI()
	{
		return new CUI(CuiHandler);
	}

	#endregion

	#region Logging

	public void Log(object message) => Logger.Log($"[{Name}] {message}");
	public void LogWarning(object message) => Logger.Warn($"[{Name}] {message}");
	public void LogError(object message, Exception ex) => Logger.Error($"[{Name}] {message}", ex);
	public void LogError(object message) => Logger.Error($"[{Name}] {message}", null);

	#endregion

	protected virtual void LoadConfig()
	{
		Config = new DynamicConfigFile(Path.Combine(Manager.ConfigPath, Name + ".json"));

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

	#region Internals

	public void ILoadConfig()
	{
		LoadConfig();
	}
	public void ILoadDefaultMessages()
	{
		CallHook("LoadDefaultMessages");
	}

	#endregion
}
