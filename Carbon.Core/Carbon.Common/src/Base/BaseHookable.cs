﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Newtonsoft.Json;
using Oxide.Core;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Base;

public class BaseHookable
{
	public List<string> Hooks { get; set; }
	public List<HookMethodAttribute> HookMethods { get; set; }
	public List<PluginReferenceAttribute> PluginReferences { get; set; }

	public Dictionary<string, List<MethodInfo>> HookCache { get; set; } = new();
	public Dictionary<string, List<MethodInfo>> HookMethodAttributeCache { get; set; } = new();
	public List<string> IgnoredHooks { get; set; } = new List<string>();

	[JsonProperty]
	public string Name { get; set; }

	[JsonProperty]
	public VersionNumber Version { get; set; }

	public bool HasInitialized { get; set; }

	#region Tracking

	internal Stopwatch _trackStopwatch = new Stopwatch();

	[JsonProperty]
	public double TotalHookTime { get; set; }

	public virtual void TrackStart()
	{
		if (!Community.IsServerFullyInitialized)
		{
			return;
		}

		var stopwatch = _trackStopwatch;
		if (stopwatch.IsRunning)
		{
			return;
		}
		stopwatch.Start();
	}
	public virtual void TrackEnd()
	{
		if (!Community.IsServerFullyInitialized)
		{
			return;
		}

		var stopwatch = _trackStopwatch;
		if (!stopwatch.IsRunning)
		{
			return;
		}
		stopwatch.Stop();
		TotalHookTime += stopwatch.Elapsed.TotalSeconds;
		stopwatch.Reset();
	}

	#endregion

	public void Unsubscribe(string hook)
	{
		if (IgnoredHooks.Contains(hook)) return;

		IgnoredHooks.Add(hook);
	}
	public void Subscribe(string hook)
	{
		if (!IgnoredHooks.Contains(hook)) return;

		IgnoredHooks.Remove(hook);
	}
	public bool IsHookIgnored(string hook)
	{
		return IgnoredHooks == null || IgnoredHooks.Contains(hook);
	}

	public T To<T>()
	{
		if (this is T result)
		{
			return result;
		}

		return default;
	}
}
