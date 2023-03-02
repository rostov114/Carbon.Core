﻿using System;
using System.IO;
using System.Reflection;
using Carbon.Base.Interfaces;
using Carbon.Contracts;
using Carbon.Core;
using Carbon.Features;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Base;

public class BaseModule : BaseHookable, IPluginMetadata
{
	public virtual bool EnabledByDefault => false;

	public static T GetModule<T>()
	{
		foreach (var module in Community.Runtime.ModuleProcessor.Modules)
		{
			if (module.GetType() == typeof(T) && module is T result) return result;
		}

		return default;
	}
}
public class CarbonModule<C, D> : BaseModule, IHookableModule
{
	public JsonConfig File { get; private set; }
	public JsonConfig Data { get; private set; }

	public virtual Type Type => default;

	public Configuration ConfigInstance { get; set; }
	public D DataInstance { get; private set; }

	public C Config { get; private set; }

	public new virtual string Name => "Not set";

	protected void Puts(object message)
		=> Logger.Log($"[{Name}] {message}");
	protected void PutsError(object message, Exception ex = null)
		=> Logger.Error($"[{Name}] {message}", ex);
	protected void PutsWarn(object message)
		=> Logger.Warn($"[{Name}] {message}");

	public virtual void Dispose()
	{
		File = null;
		ConfigInstance = null;
	}

	public virtual void Init()
	{
		base.Name = Name;

		foreach (var method in Type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
		{
			if (Community.Runtime.HookManager.IsHookLoaded(method.Name))
				Community.Runtime.HookManager.Subscribe(method.Name, Name);
		}

		Loader.ProcessCommands(Type, this, flags: BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

		File = new JsonConfig(Path.Combine(Defines.GetModulesFolder(), Name, "config.json"));
		Data = new JsonConfig(Path.Combine(Defines.GetModulesFolder(), Name, "data.json"));

		Load();
		if (ConfigInstance.Enabled) OnEnableStatus();
	}
	public virtual void InitEnd()
	{
		Puts($"Initialized.");
	}
	public virtual void Load()
	{
		var shouldSave = false;

		if (!File.Exists())
		{
			ConfigInstance = new Configuration { Config = Activator.CreateInstance<C>() };
			if (EnabledByDefault) ConfigInstance.Enabled = true;
			shouldSave = true;
		}
		else
		{
			try { ConfigInstance = File.ReadObject<Configuration>(); }
			catch (Exception exception) { Logger.Error($"Failed loading config. JSON file is corrupted and/or invalid.\n{exception.Message}"); }
		}

		Config = ConfigInstance.Config;

		if (!Data.Exists())
		{
			DataInstance = Activator.CreateInstance<D>();
			shouldSave = true;
		}
		else
		{
			try { DataInstance = Data.ReadObject<D>(); }
			catch (Exception exception) { Logger.Error($"Failed loading data. JSON file is corrupted and/or invalid.\n{exception.Message}"); }
		}

		if (PreLoadShouldSave()) shouldSave = true;

		if (shouldSave) Save();
	}
	public virtual bool PreLoadShouldSave()
	{
		return false;
	}
	public virtual void Save()
	{
		if (ConfigInstance == null)
		{
			ConfigInstance = new Configuration { Config = Activator.CreateInstance<C>() };
			Config = ConfigInstance.Config;
		}

		if (DataInstance == null)
		{
			DataInstance = Activator.CreateInstance<D>();
		}

		File.WriteObject(ConfigInstance);
		Data.WriteObject(DataInstance);
	}

	public void SetEnabled(bool enable)
	{
		if (ConfigInstance != null)
		{
			ConfigInstance.Enabled = enable;
			OnEnableStatus();
		}
	}
	public bool GetEnabled()
	{
		return ConfigInstance.Enabled;
	}

	public virtual void OnDisabled(bool initialized) { }
	public virtual void OnEnabled(bool initialized) { }

	public void OnEnableStatus()
	{
		try
		{
			if (ConfigInstance.Enabled) OnEnabled(Community.IsServerFullyInitialized); else OnDisabled(Community.IsServerFullyInitialized);
		}
		catch (Exception ex) { Logger.Error($"Failed {(ConfigInstance.Enabled ? "Enable" : "Disable")} initialization.", ex); }
	}

	private void OnServerInitialized()
	{
		if (GetEnabled()) OnEnableStatus();
	}

	public class Configuration : IModuleConfig
	{
		public bool Enabled { get; set; }
		public C Config { get; set; }
	}
}
