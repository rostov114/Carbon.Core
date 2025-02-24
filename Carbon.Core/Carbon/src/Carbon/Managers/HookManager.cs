﻿#pragma warning disable IDE0051

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using API.Events;
using API.Hooks;
using Carbon.Contracts;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Hooks;

public sealed class HookManager : FacepunchBehaviour, IHookManager, IDisposable
{
	internal List<HookEx> _patches { get; set; }
	internal List<HookEx> _staticHooks { get; set; }
	internal List<HookEx> _dynamicHooks { get; set; }

	// TODO --------------------------------------------------------------------
	// Allows patch uninstallation on the correct order and should be replaced
	// with Harmony After/Before mechanics. To be able to do that we need to
	// modify the autogen tool to take into consideration the offset created by
	// the first applied patch when dealing with the InjectionIndex of the
	// second patch.
	internal List<HookEx> _installed { get; set; }

#if DEBUG
	// Number of patches applied by update cycle
	private readonly int PatchLimitPerCycle = 50;
#else
	// Number of patches applied by update cycle
	private readonly int PatchLimitPerCycle = 25;
#endif

	private Stopwatch sw;
	private bool _doReload;
	private Queue<string> _workQueue;
	//private Hashtable _cachedChecksums;
	private List<Subscription> _subscribers;
	private static readonly string[] Files =
	{
		"Carbon.Hooks.Base.dll",
		"Carbon.Hooks.Extra.dll",
	};

	private void Awake()
	{
		Logger.Log($"Initializing {this}..");

		// 
		sw = new Stopwatch();

		//_cachedChecksums = new Hashtable();
		_dynamicHooks = new List<HookEx>();
		_installed = new List<HookEx>();
		_patches = new List<HookEx>();
		_staticHooks = new List<HookEx>();
		_subscribers = new List<Subscription>();
		_workQueue = new Queue<string>();

		if (Community.Runtime.Config.AutoUpdate)
		{
			Logger.Log("Updating hooks...");
			enabled = false;

			Updater.DoUpdate((bool result) =>
			{
				if (!result)
					Logger.Error($"Unable to update the hooks at this time, please try again later");

				enabled = true;
			});
		}
	}

	private void OnEnable()
	{
		_patches.Clear();
		_staticHooks.Clear();
		_dynamicHooks.Clear();

		foreach (string file in Files)
		{

			//FIXMENOW
			//string path = Path.Combine(Defines.GetManagedFolder(), file);

			//if (Supervisor.ASM.IsLoaded(Path.GetFileName(path)))
			//	Supervisor.ASM.UnloadModule(path, false);

			LoadHooksFromFile(file);
		}

		if (_patches.Count > 0)
		{
			Logger.Log($" - Installing patches");
			// I don't like this, patching stuff that may not be used but for the
			// sake of time I will let it go for now but this needs to be reviewed.
			foreach (HookEx hook in _patches.Where(x => !x.IsInstalled && !x.HasDependencies()))
				Subscribe(hook.Identifier, "Carbon.Core");
		}

		if (_staticHooks.Count > 0)
		{
			Logger.Log($" - Installing static hooks");
			foreach (HookEx hook in _staticHooks.Where(x => !x.IsInstalled))
				Subscribe(hook.Identifier, "Carbon.Core");
		}

		// if (_dynamicHooks.Count > 0)
		// {
		// 	Logger.Log($" - Installing dynamic hooks");
		// 	foreach (HookEx hook in _dynamicHooks.Where(x => HookHasSubscribers(x.Identifier)))
		// 		_workQueue.Enqueue(item: new Payload(hook.HookName, null, "Carbon.Core"));
		// }

		Community.Runtime.Events.Trigger(CarbonEvent.HooksInstalled, EventArgs.Empty);
	}

	private void OnDisable()
	{
		Logger.Log("Stopping hook processor...");

		foreach (HookEx item in _installed.AsEnumerable().Reverse())
		{
			if (!item.RemovePatch())
				throw new Exception($"Uninstallation failed for '{item}'");
			_installed.Remove(item);
		}

		if (!_doReload) return;

		Logger.Log("Reloading hook processor...");
		_doReload = false;
		enabled = true;
	}

	private void OnDestroy()
	{
		Logger.Log("Destroying hook processor...");

		// make sure all patches are removed
		List<HookEx> hooks = _staticHooks.Concat(_dynamicHooks).Concat(_patches).ToList();
		foreach (HookEx hook in hooks) hook.Dispose();

		_workQueue = default;
		_subscribers = default;

		_patches = default;
		_staticHooks = default;
		_dynamicHooks = default;
	}

	private void Update()
	{
		// get the fuck out as fast as possible
		if (_workQueue.Count == 0) return;

		try
		{
			int limit = PatchLimitPerCycle;
			while (_workQueue.Count > 0 && limit-- > 0)
			{
				string identifier = _workQueue.Dequeue();

				HookEx hook = GetHookById(identifier);

				bool hasSubscribers = HookHasSubscribers(hook.Identifier);
				bool isInstalled = hook.IsInstalled;

				//bool hasValidChecksum = true;
				//
				// if (!isInstalled)
				// {
				// 	string id = $"{hook.TargetType}|{hook.TargetMethod}|{hook.TargetMethodArgs.Count()}";
				// 	string checksum = null;

				// 	// if (_cachedChecksums.ContainsKey(id))
				// 	// 	checksum = (string)_cachedChecksums[id];
				// 	checksum ??= hook.GetTargetMethodChecksum();

				// 	hasValidChecksum = (hook.IsChecksumIgnored || hook.Checksum == string.Empty)
				// 		|| checksum.Equals(hook.Checksum, StringComparison.InvariantCultureIgnoreCase);
				// }

				switch (hasSubscribers)
				{
					// Not installed but has subs, install
					case true when !isInstalled:
						if (!hook.ApplyPatch())
							throw new ApplicationException($"A general error occured while installing '{hook}'");
						Logger.Debug($"Installed hook '{hook}'", 1);
						_installed.Add(hook);
						break;
					// Installed but no subs found, uninstall
					case false when isInstalled:
						if (!hook.RemovePatch())
							throw new ApplicationException($"A general error occured while uninstalling '{hook}'");
						Logger.Debug($"Uninstalled hook '{hook}'", 1);
						_installed.Remove(hook);
						break;
				}

				// if (!hasValidChecksum)
				// {
				// 	Logger.Warn($"Checksum validation failed for '{hook.TargetType}.{hook.TargetMethod}'");
				// 	hook.SetStatus(HookState.Warning);
				// }
			}
		}
		catch (System.ApplicationException e)
		{
			Logger.Error(e.Message);
		}
		catch (System.Exception e)
		{
			Logger.Error("HookManager.Update() failed", e);
		}
	}

	private void LoadHooksFromFile(string fileName)
	{
		// delegates asm loading to Carbon.Loader 
		Assembly hooks = Community.Runtime.AssemblyEx.LoadHook(fileName, "HookManager.LoadHooksFromFile");

		if (hooks == null)
		{
			Logger.Error($"Error while loading hooks from '{fileName}'.");
			Logger.Error($"Either the file is corrupt or has an unsuported format/version.");
			return;
		}

		Type @base = hooks.GetType("API.Hooks.Patch")
			?? typeof(API.Hooks.Patch);

		Type attr = hooks.GetType("API.Hooks.HookAttribute.Patch")
			?? typeof(HookAttribute.Patch);

		IEnumerable<TypeInfo> types = hooks.DefinedTypes
			.Where(type => @base.IsAssignableFrom(type) && Attribute.IsDefined(type, attr)).ToList();

		TaskStatus stats = LoadHooks(types);
		if (stats.Total == 0) return;

		Logger.Log($"- Loaded {stats.Total} hooks ({stats.Patch}/{stats.Static}/{stats.Dynamic})"
			+ $" from file '{Path.GetFileName(fileName)}' in {sw.ElapsedMilliseconds}ms");
	}

	public void LoadHooksFromType(Type type)
	{
		Type @base = typeof(API.Hooks.Patch);
		Type attr = typeof(HookAttribute.Patch);

		IEnumerable<TypeInfo> types = type.GetNestedTypes()
			.Where(type => @base.IsAssignableFrom(type) && Attribute.IsDefined(type, attr))
			.Select(x => x.GetTypeInfo()).ToList();

		TaskStatus stats = LoadHooks(types);
		if (stats.Total == 0) return;

		Logger.Log($"- Loaded {stats.Total} hooks ({stats.Patch}/{stats.Static}/{stats.Dynamic})"
			+ $" from type '{type}' in {sw.ElapsedMilliseconds}ms");
	}

	private TaskStatus LoadHooks(IEnumerable<TypeInfo> types)
	{
		TaskStatus retvar = new();
		sw.Restart();

		foreach (TypeInfo type in types)
		{
			try
			{
				HookEx hook = new HookEx(type)
					?? throw new Exception($"Hook is null, this is a bug");

				if (IsHookLoaded(hook))
					throw new Exception($"Found duplicated hook '{hook}'");

				if (hook.IsPatch)
				{
					retvar.Patch++;
					_patches.Add(hook);
					Logger.Debug($"Loaded patch '{hook}'", 4);
				}
				else if (hook.IsStaticHook)
				{
					retvar.Static++;
					_staticHooks.Add(hook);
					Logger.Debug($"Loaded static hook '{hook}'", 4);
				}
				else
				{
					retvar.Dynamic++;
					_dynamicHooks.Add(hook);
					Logger.Debug($"Loaded dynamic hook '{hook}'", 4);
				}
			}
			catch (System.Exception e)
			{
				Logger.Error($"Error while parsing '{type.Name}'", e);
			}
		}

		sw.Stop();
		return retvar;
	}

	private List<HookEx> GetHookDependencyTree(HookEx hook)
	{
		List<HookEx> dependencies = new List<HookEx>();

		foreach (string dependency in hook.Dependencies)
		{
			List<HookEx> list = GetHookByFullName(dependency).ToList();

			if (list.Count < 1)
			{
				Logger.Error($"Dependency '{dependency}' for '{hook}' not loaded, this is a bug");
				continue;
			}

			foreach (HookEx item in list)
			{
				dependencies = dependencies.Concat(GetHookDependencyTree(item)).ToList();
				dependencies.Add(item);
			}
		}
		return dependencies.Distinct().ToList();
	}

	private List<HookEx> GetHookDependantTree(HookEx hook)
	{
		List<HookEx> dependants = new List<HookEx>();
		List<HookEx> list = _patches.Where(x => x.Dependencies.Contains(hook.HookFullName)).ToList();

		foreach (HookEx item in list)
		{
			dependants = dependants.Concat(GetHookDependantTree(item)).ToList();
			dependants.Add(item);
		}
		return dependants.Distinct().ToList();
	}

	private IEnumerable<HookEx> LoadedHooks
	{ get => _patches.Concat(_dynamicHooks).Concat(_staticHooks).Where(x => x.IsLoaded); }

	private IEnumerable<HookEx> GetHookByName(string name)
		=> LoadedHooks.Where(x => x.HookName == name) ?? null;

	private IEnumerable<HookEx> GetHookByFullName(string name)
		=> LoadedHooks.Where(x => x.HookFullName == name) ?? null;

	private HookEx GetHookById(string identifier)
		=> LoadedHooks.FirstOrDefault(x => x.Identifier == identifier) ?? null;


	internal bool IsHookLoaded(HookEx hook)
		=> LoadedHooks.Any(x => x.PatchMethodName == hook.PatchMethodName);

	public bool IsHookLoaded(string hookName)
	{
		List<HookEx> hooks = GetHookByName(hookName).ToList();
		return hooks.Count != 0 && hooks.Any(IsHookLoaded);
	}


	public IEnumerable<IHook> LoadedPatches
	{ get => _patches; }

	public IEnumerable<IHook> InstalledPatches
	{ get => _patches.Where(x => x.IsInstalled); }

	public IEnumerable<IHook> LoadedStaticHooks
	{ get => _staticHooks; }

	public IEnumerable<IHook> InstalledStaticHooks
	{ get => _staticHooks.Where(x => x.IsInstalled); }

	public IEnumerable<IHook> LoadedDynamicHooks
	{ get => _dynamicHooks; }

	public IEnumerable<IHook> InstalledDynamicHooks
	{ get => _dynamicHooks.Where(x => x.IsInstalled); }


	private bool HookIsSubscribedBy(string identifier, string subscriber)
		=> _subscribers?.Where(x => x.Identifier == identifier).Any(x => x.Subscriber == subscriber) ?? false;

	private bool HookHasSubscribers(string identifier)
		=> _subscribers?.Any(x => x.Identifier == identifier) ?? false;

	public int GetHookSubscriberCount(string identifier)
		=> _subscribers.Where(x => x.Identifier == identifier).ToList().Count;

	private void AddSubscriber(string identifier, string subscriber)
		=> _subscribers.Add(item: new Subscription { Identifier = identifier, Subscriber = subscriber });

	private void RemoveSubscriber(string identifier, string subscriber)
		=> _subscribers.RemoveAll(x => x.Identifier == identifier && x.Subscriber == subscriber);


	public void Subscribe(string hookName, string requester)
	{
		try
		{
			HookEx single = GetHookById(hookName);

			if (single != null && !HookIsSubscribedBy(single.Identifier, requester))
			{
				Subscribe(single, requester);
				return;
			}

			List<HookEx> hooks = GetHookByName(hookName).ToList();

			if (hooks.Count == 0)
			{
				Logger.Debug($"Failed to subscribe '{hookName}' by '{requester}', hook not found");
				return;
			};

			IEnumerable<HookEx> list = hooks
				.Where(hook => !HookIsSubscribedBy(hook.Identifier, requester));

			foreach (HookEx hook in list)
				Subscribe(hook, requester);

			list = default;
			hooks = default;
		}
		catch (Exception e)
		{
			Logger.Error($"Error while subscribing hook '{hookName}'", e);
			return;
		}
	}

	private void Subscribe(HookEx hook, string requester)
	{
		try
		{
			Logger.Debug($"Subscribe to '{hook}' by '{requester}'");

			foreach (HookEx dependency in GetHookDependencyTree(hook))
			{
				Logger.Debug($"Subscribe dependency '{dependency}' for '{hook}'", 1);
				AddSubscriber(dependency.Identifier, requester);
				_workQueue.Enqueue(dependency.Identifier);
			}

			AddSubscriber(hook.Identifier, requester);
			_workQueue.Enqueue(hook.Identifier);

			foreach (HookEx dependant in GetHookDependantTree(hook))
			{
				Logger.Debug($"Subscribe dependant '{dependant}' for '{hook}'", 1);
				AddSubscriber(dependant.Identifier, requester);
				_workQueue.Enqueue(dependant.Identifier);
			}
		}
		catch (Exception e)
		{
			Logger.Error($"Error while subscribing hook '{hook}'", e);
			return;
		}
	}

	public void Unsubscribe(string hookName, string requester)
	{
		try
		{
			HookEx single = GetHookById(hookName);

			if (single != null && !HookIsSubscribedBy(single.Identifier, requester))
			{
				Subscribe(single, requester);
				return;
			}

			List<HookEx> hooks = GetHookByName(hookName).ToList();

			if (hooks.Count == 0)
			{
				Logger.Debug($"Failure to subscribe to '{hookName}' by '{requester}', no hook found");
				return;
			};

			IEnumerable<HookEx> list = hooks
				.Where(hook => HookIsSubscribedBy(hook.Identifier, requester));

			foreach (HookEx hook in list)
				Unsubscribe(hook, requester);

			hooks = default;
		}
		catch (Exception e)
		{
			Logger.Error($"Error while unsubscribing hook '{hookName}'", e);
			return;
		}
	}

	private void Unsubscribe(HookEx hook, string requester)
	{
		try
		{
			foreach (HookEx dependant in GetHookDependantTree(hook))
			{
				Logger.Debug($"Unsubscribe dependant '{dependant}' for '{hook}'", 1);
				RemoveSubscriber(dependant.Identifier, requester);
				_workQueue.Enqueue(dependant.Identifier);
			}

			RemoveSubscriber(hook.Identifier, requester);
			_workQueue.Enqueue(hook.Identifier);

			foreach (HookEx dependency in GetHookDependencyTree(hook))
			{
				Logger.Debug($"Unsubscribe dependency '{dependency}' for '{hook}'", 1);
				RemoveSubscriber(dependency.Identifier, requester);
				_workQueue.Enqueue(dependency.Identifier);
			}
		}
		catch (Exception e)
		{
			Logger.Error($"Error while unsubscribing hook '{hook}'", e);
			return;
		}
	}


	private bool _disposing;

	internal void Dispose(bool disposing)
	{
		if (!_disposing)
		{
			if (disposing)
			{
				foreach (HookEx item in _dynamicHooks) item.Dispose();
				_dynamicHooks.Clear();

				foreach (HookEx item in _installed) item.Dispose();
				_installed.Clear();

				foreach (HookEx item in _patches) item.Dispose();
				_patches.Clear();

				foreach (HookEx item in _staticHooks) item.Dispose();
				_staticHooks.Clear();
			}

			// no unmanaged resources
			_disposing = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
