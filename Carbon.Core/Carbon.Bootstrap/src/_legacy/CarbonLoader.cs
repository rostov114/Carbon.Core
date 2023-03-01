﻿using System;
using System.IO;
using System.Reflection;
using API.Events;
using Components;
using Contracts;
using Legacy.ASM;
using Legacy.Harmony;
using Utility;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Legacy;

internal sealed class Loader : Singleton<Loader>, IDisposable
{
	private static readonly string identifier;
	private static readonly string assemblyName;

	private HarmonyLib.Harmony _harmonyInstance;
	private UnityEngine.GameObject _gameObject;


	internal string Name
	{ get => assemblyName; }

	internal HarmonyLib.Harmony Harmony
	{ get => _harmonyInstance; }

	internal DownloadManager Downloader
	{ get => _gameObject.GetComponent<DownloadManager>(); }

	internal EventManager Events
	{ get => _gameObject.GetComponent<EventManager>(); }

	internal AnalyticsManager Identity
	{ get => _gameObject.GetComponent<AnalyticsManager>(); }

	static Loader()
	{
		identifier = $"{Guid.NewGuid():N}";
		Logger.Warn($"Using '{identifier}' as runtime namespace");
		assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
		AssemblyManager.GetInstance().Register(AppDomain.CurrentDomain);
	}

	internal Loader()
	{
		_harmonyInstance = new HarmonyLib.Harmony(identifier);

#if DEBUG
		HarmonyLib.Harmony.DEBUG = true;
		Environment.SetEnvironmentVariable("HARMONY_LOG_FILE", Path.Combine(Context.CarbonLogs, "harmony.log"));
		typeof(HarmonyLib.FileLog).GetField("_logPathInited", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
#endif

		_gameObject = new UnityEngine.GameObject("Carbon");
		UnityEngine.Object.DontDestroyOnLoad(_gameObject);

		_gameObject.AddComponent<DownloadManager>();
		_gameObject.AddComponent<EventManager>();
		_gameObject.AddComponent<AnalyticsManager>();

		Events.Subscribe(CarbonEvent.StartupShared, x =>
		{
			HarmonyLoaderEx.GetInstance().Load("Carbon.dll");
		});

		Events.Subscribe(CarbonEvent.CarbonStartupComplete, x =>
		{
			HarmonyLoaderEx.GetInstance().Load("Carbon.Modules.dll");
			HarmonyLoaderEx.GetInstance().Load("Carbon.Oxide.dll");
		});
	}

	internal void Initialize()
	{
		try
		{
			Logger.Log("Patching Facepunch's harmony loader");
			Harmony.PatchAll(Assembly.GetExecutingAssembly());
		}
		catch (Exception e)
		{
			Logger.Error("Unable to apply all Harmony patches", e);
		}
	}

	public void Dispose()
	{
		try
		{
			Harmony.UnpatchAll(identifier);
			Logger.Log("Removed all Harmony patches");
		}
		catch (Exception e)
		{
			Logger.Error("Unable to remove all Harmony patches", e);
		}

		_harmonyInstance = default;
		_gameObject = default;
	}
}
