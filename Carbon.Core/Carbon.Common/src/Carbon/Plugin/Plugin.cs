﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Carbon;
using Carbon.Base;
using Carbon.Components;
using Carbon.Contracts;
using Carbon.Core;
using Carbon.Plugins.Features;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Plugins
{
	[JsonObject(MemberSerialization.OptIn)]
	public class Plugin : BaseHookable, IDisposable, IPlugin
	{
		public bool IsCorePlugin { get; set; }
		public bool HasConditionals { get; set; }

		#region Metadata

		[JsonProperty]
		public string Title { get; set; } = "Rust";
		[JsonProperty]
		public string Description { get; set; }
		[JsonProperty]
		public string Author { get; set; }

		[JsonProperty]
		public double CompileTime { get; set; }

		public Persistence Persistence { get; set; }

		public string FilePath { get; set; }
		public string FileName { get; set; }

		#endregion

		#region Install

		public virtual void SetupMod(Loader.CarbonMod mod, string name, string author, VersionNumber version, string description) { }
		public virtual void Setup(string name, string author, VersionNumber version, string description) { }

		#endregion

		#region Profiling

		public override void TrackStart()
		{
			if (IsCorePlugin) return;

			base.TrackStart();
		}
		public override void TrackEnd()
		{
			if (IsCorePlugin) return;

			base.TrackEnd();
		}

		#endregion

		public IPlugin[] Requires { get; set; }
		internal Loader.CarbonMod CarbonMod { get; set; }
		public IBaseProcessor Processor { get; set; }
		public IBaseProcessor.IInstance ProcessorInstance { get; set; }

		public static implicit operator bool(Plugin other)
		{
			return other != null;
		}

		public virtual void IInit()
		{
			if (HookMethods != null)
			{
				using (TimeMeasure.New($"Processing HookMethods on '{this}'"))
				{
					foreach (var attribute in HookMethods)
					{
						var method = attribute.Method;
						var name = (string.IsNullOrEmpty(attribute.Name) ? method.Name : attribute.Name) + method.GetParameters().Length;
						if (!HookMethodAttributeCache.TryGetValue(name, out var list))
						{
							HookMethodAttributeCache.Add(name, new List<MethodInfo>() { method });
						}
						else list.Add(method);
					}
				}
				Logger.Debug(Name, "Installed hook method attributes");
			}

			using (TimeMeasure.New($"Processing PluginReferences on '{this}'"))
			{
				ApplyPluginReferences();
			}
			Logger.Debug(Name, "Assigned plugin references");

			if (Hooks != null)
			{
				string requester = FileName is not default(string) ? FileName : $"{this}";
				using (TimeMeasure.New($"Processing Hooks on '{this}'"))
				{
					foreach (var hook in Hooks)
						Community.Runtime.HookManager.Subscribe(hook, requester);
				}
				Logger.Debug(Name, "Processed hooks");
			}

			CallHook("Init");
		}
		public virtual void Load()
		{
			using (TimeMeasure.New($"Load on '{this}'"))
			{
				IsLoaded = true;
				CallHook("OnLoaded");
				CallHook("Loaded");
			}

			using (TimeMeasure.New($"Load.PendingRequirees on '{this}'"))
			{
				var requirees = Loader.GetRequirees(this);

				if (requirees != null)
				{
					foreach (var requiree in requirees)
					{
						Logger.Warn($" [{Name}] Loading '{Path.GetFileNameWithoutExtension(requiree)}' to parent's request: '{ToString()}'");
						Community.Runtime.ScriptProcessor.Prepare(requiree);
					}

					Loader.ClearPendingRequirees(this);
				}
			}
		}
		public virtual void Unload() { }
		public virtual void IUnload()
		{
			using (TimeMeasure.New($"IUnload.UnprocessHooks on '{this}'"))
			{
				foreach (var hook in Hooks)
					Community.Runtime.HookManager.Unsubscribe(hook, FileName);
				Logger.Debug(Name, $"Unprocessed hooks");
			}

			using (TimeMeasure.New($"IUnload.Disposal on '{this}'"))
			{
				IgnoredHooks.Clear();
				HookCache.Clear();
				Hooks.Clear();
				HookMethods.Clear();
				PluginReferences.Clear();
				HookMethodAttributeCache.Clear();

				IgnoredHooks = null;
				HookCache = null;
				Hooks = null;
				HookMethods = null;
				PluginReferences = null;
				HookMethodAttributeCache = null;
			}

			using (TimeMeasure.New($"IUnload.UnloadRequirees on '{this}'"))
			{
				var mods = Pool.GetList<Loader.CarbonMod>();
				mods.AddRange(Loader.LoadedMods);
				var plugins = Pool.GetList<IPlugin>();

				foreach (var mod in Loader.LoadedMods)
				{
					plugins.Clear();
					plugins.AddRange(mod.Plugins);

					foreach (var plugin in plugins)
					{
						if (plugin.Requires != null && plugin.Requires.Contains(this))
						{
							switch (plugin.Processor)
							{
								case IScriptProcessor script:
									Logger.Warn($" [{Name}] Unloading '{plugin.ToString()}' because parent '{ToString()}' has been unloaded.");
									Loader.AddPendingRequiree(this, plugin);
									plugin.Processor.Get<IScriptProcessor.IScript>(plugin.FileName).Dispose();
									break;
							}
						}
					}
				}

				Pool.FreeList(ref mods);
				Pool.FreeList(ref plugins);
			}
		}
		public virtual void ILoadConfig() { }
		public virtual void ILoadDefaultMessages() { }

		public void ApplyPluginReferences()
		{
			if (PluginReferences == null) return;

			foreach (var reference in PluginReferences)
			{
				var field = reference.Field;
				var attribute = field.GetCustomAttribute<PluginReferenceAttribute>();
				if (attribute == null) continue;

				var name = string.IsNullOrEmpty(attribute.Name) ? field.Name : attribute.Name;

				var plugin = (IPlugin)null;
				if (field.FieldType.Name != nameof(Plugin) &&
					field.FieldType.Name != nameof(CarbonPlugin) &&
					field.FieldType.Name != "RustPlugin")
				{
					var info = field.FieldType.GetCustomAttribute<InfoAttribute>();
					if (info == null)
					{
						Logger.Warn($"You're trying to reference a non-plugin instance: {name}[{field.FieldType.Name}]");
						continue;
					}

					plugin = CarbonPlugin.Find(info.Title);
				}
				else plugin = CarbonPlugin.Find(name);

				if (plugin != null) field.SetValue(this, plugin);
			}
		}

		public void SetProcessor(IBaseProcessor processor)
		{
			Processor = processor;
		}

		#region Calls

		public T Call<T>(string hook)
		{
			return HookCaller.CallHook<T>(this, hook);
		}
		public T Call<T>(string hook, object arg1)
		{
			return HookCaller.CallHook<T>(this, hook, arg1);
		}
		public T Call<T>(string hook, object arg1, object arg2)
		{
			return HookCaller.CallHook<T>(this, hook, arg1, arg2);
		}
		public T Call<T>(string hook, object arg1, object arg2, object arg3)
		{
			return HookCaller.CallHook<T>(this, hook, arg1, arg2, arg3);
		}
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4)
		{
			return HookCaller.CallHook<T>(this, hook, arg1, arg2, arg3, arg4);
		}
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5)
		{
			return HookCaller.CallHook<T>(this, hook, arg1, arg2, arg3, arg4, arg5);
		}
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6)
		{
			return HookCaller.CallHook<T>(this, hook, arg1, arg2, arg3, arg4, arg5, arg6);
		}
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7)
		{
			return HookCaller.CallHook<T>(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
		}
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8)
		{
			return HookCaller.CallHook<T>(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
		}
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8, object arg9)
		{
			return HookCaller.CallHook<T>(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
		}

		public object Call(string hook)
		{
			return HookCaller.CallHook(this, hook);
		}
		public object Call(string hook, object arg1)
		{
			return HookCaller.CallHook(this, hook, arg1);
		}
		public object Call(string hook, object arg1, object arg2)
		{
			return HookCaller.CallHook(this, hook, arg1, arg2);
		}
		public object Call(string hook, object arg1, object arg2, object arg3)
		{
			return HookCaller.CallHook(this, hook, arg1, arg2, arg3);
		}
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4)
		{
			return HookCaller.CallHook(this, hook, arg1, arg2, arg3, arg4);
		}
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4, object arg5)
		{
			return HookCaller.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5);
		}
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6)
		{
			return HookCaller.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6);
		}
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7)
		{
			return HookCaller.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
		}
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8)
		{
			return HookCaller.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
		}
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8, object arg9)
		{
			return HookCaller.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
		}

		public T CallHook<T>(string hook)
		{
			return HookCaller.CallHook<T>(this, hook);
		}
		public T CallHook<T>(string hook, object arg1)
		{
			return HookCaller.CallHook<T>(this, hook, arg1);
		}
		public T CallHook<T>(string hook, object arg1, object arg2)
		{
			return HookCaller.CallHook<T>(this, hook, arg1, arg2);
		}
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3)
		{
			return HookCaller.CallHook<T>(this, hook, arg1, arg2, arg3);
		}
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4)
		{
			return HookCaller.CallHook<T>(this, hook, arg1, arg2, arg3, arg4);
		}
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5)
		{
			return HookCaller.CallHook<T>(this, hook, arg1, arg2, arg3, arg4, arg5);
		}
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6)
		{
			return HookCaller.CallHook<T>(this, hook, arg1, arg2, arg3, arg4, arg5, arg6);
		}
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7)
		{
			return HookCaller.CallHook<T>(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
		}
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8)
		{
			return HookCaller.CallHook<T>(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
		}
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8, object arg9)
		{
			return HookCaller.CallHook<T>(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
		}

		public object CallHook(string hook)
		{
			return HookCaller.CallHook(this, hook);
		}
		public object CallHook(string hook, object arg1)
		{
			return HookCaller.CallHook(this, hook, arg1);
		}
		public object CallHook(string hook, object arg1, object arg2)
		{
			return HookCaller.CallHook(this, hook, arg1, arg2);
		}
		public object CallHook(string hook, object arg1, object arg2, object arg3)
		{
			return HookCaller.CallHook(this, hook, arg1, arg2, arg3);
		}
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4)
		{
			return HookCaller.CallHook(this, hook, arg1, arg2, arg3, arg4);
		}
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4, object arg5)
		{
			return HookCaller.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5);
		}
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6)
		{
			return HookCaller.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6);
		}
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7)
		{
			return HookCaller.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
		}
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8)
		{
			return HookCaller.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
		}
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8, object arg9)
		{
			return HookCaller.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
		}

		#endregion

		#region Logging

		public void Log(object message) => Logger.Log($"[{Name}] {message}");
		public void LogWarning(object message) => Logger.Warn($"[{Name}] {message}");
		public void LogError(object message, Exception ex) => Logger.Error($"[{Name}] {message}", ex);
		public void LogError(object message) => Logger.Error($"[{Name}] {message}", null);

		#endregion

		public void NextTick(Action callback)
		{
			Community.Runtime.CarbonProcessor.OnFrameQueue.Enqueue(callback);
		}
		public void NextFrame(Action callback)
		{
			Community.Runtime.CarbonProcessor.OnFrameQueue.Enqueue(callback);
		}

		public bool IsLoaded { get; set; }

		public new string ToString()
		{
			return GetType().Name;
		}
		public virtual void Dispose()
		{
			IsLoaded = false;
		}
	}
}
