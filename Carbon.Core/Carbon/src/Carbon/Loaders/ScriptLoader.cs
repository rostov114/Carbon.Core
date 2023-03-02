﻿// #define DISABLE_ASYNC_LOADING

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Carbon.Base;
using Carbon.Contracts;
using Carbon.Core;
using Carbon.Extensions;
using Carbon.Jobs;
using Carbon.Plugins;
using Facepunch;
using Oxide.Core;
using UnityEngine;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Processors;

public class ScriptLoader : IDisposable, IScriptLoader
{
	public List<IScript> Scripts { get; set; } = new List<IScript>();

	public string File { get; set; }
	public string Source { get; set; }
	public bool IsCore { get; set; }

	public bool HasFinished { get; set; }
	public bool HasRequires { get; set; }

	public IBaseProcessor.IInstance Instance { get; set; }
	public Loader.CarbonMod Mod { get; set; }
	public IBaseProcessor.IParser Parser { get; set; }
	public ScriptCompilationThread AsyncLoader { get; set; } = new ScriptCompilationThread();

	internal WaitForSeconds _serverExhale = new WaitForSeconds(0.1f);

	public void Load()
	{
		try
		{
			if (!string.IsNullOrEmpty(File) && OsEx.File.Exists(File)) Source = OsEx.File.ReadText(File);

			if (Parser != null)
			{
				Parser.Process(Source, out var newSource);

				if (!string.IsNullOrEmpty(newSource))
				{
					Source = newSource;
				}
			}

			Community.Runtime.ScriptProcessor.StartCoroutine(Compile());
		}
		catch (Exception exception)
		{
			Logger.Error($"Failed loading script;", exception);
		}
	}

	public static void LoadAll()
	{
		var files = OsEx.Folder.GetFilesWithExtension(Defines.GetScriptFolder(), "cs");

		Community.Runtime.ScriptProcessor.Clear();
		Community.Runtime.ScriptProcessor.IgnoreList.Clear();

		foreach (var file in files)
		{
			var plugin = new ScriptProcessor.Script { File = file };
			Community.Runtime.ScriptProcessor.InstanceBuffer.Add(Path.GetFileNameWithoutExtension(file), plugin);
		}

		foreach (var plugin in Community.Runtime.ScriptProcessor.InstanceBuffer)
		{
			plugin.Value.SetDirty();
		}
	}

	public void Clear()
	{
		AsyncLoader?.Abort();
		AsyncLoader = null;

		for (int i = 0; i < Scripts.Count; i++)
		{
			var plugin = Scripts[i];
			if (plugin.IsCore) continue;

			Community.Runtime.Plugins.Plugins.Remove(plugin.Instance);

			if (plugin.Instance != null)
			{
				try
				{
					Loader.UninitializePlugin(plugin.Instance);
				}
				catch (Exception ex) { Logger.Error($"Failed unloading '{plugin.Instance}'", ex); }
			}

			plugin?.Dispose();
		}

		if (Scripts.Count > 0)
		{
			Scripts.RemoveAll(x => !x.IsCore);
		}
	}

	public IEnumerator Compile()
	{
		if (string.IsNullOrEmpty(Source))
		{
			HasFinished = true;
			Logger.Warn("Attempted to compile an empty string of source code.");
			yield break;
		}

		var lines = Source.Split('\n');
		var resultReferences = Pool.GetList<string>();
		foreach (var reference in lines)
		{
			try
			{
				if (reference.StartsWith("// Reference:") || reference.StartsWith("//Reference:"))
				{
					var @ref = $"{reference.Replace("// Reference:", "").Replace("//Reference:", "")}".Trim();
					resultReferences.Add(@ref);
					Logger.Log($" Added reference: {@ref}");
				}
			}
			catch { }
		}

		var resultRequires = Pool.GetList<string>();
		foreach (var require in lines)
		{
			try
			{
				if (require.StartsWith("// Requires:") || require.StartsWith("//Requires:"))
				{

					var @ref = $"{require.Replace("// Requires:", "").Replace("//Requires:", "")}".Trim();
					resultRequires.Add(@ref);
					Logger.Log($" Added required plugin: {@ref}");
				}
			}
			catch { }
		}

		Pool.Free(ref lines);
		AsyncLoader.FilePath = File;
		AsyncLoader.Source = Source;
		AsyncLoader.References = resultReferences?.ToArray();
		AsyncLoader.Requires = resultRequires?.ToArray();
		Pool.FreeList(ref resultReferences);
		Pool.FreeList(ref resultRequires);

		HasRequires = AsyncLoader.Requires.Length > 0;

		while (HasRequires && !Community.Runtime.ScriptProcessor.AllNonRequiresScriptsComplete())
		{
			yield return _serverExhale;
			yield return null;
		}

		var requires = Pool.GetList<Plugin>();
		var noRequiresFound = false;
		foreach (var require in AsyncLoader.Requires)
		{
			var plugin = CarbonPlugin.Find(require);
			if (plugin == null)
			{
				Logger.Warn($"Couldn't find required plugin '{require}' for '{(!string.IsNullOrEmpty(File) ? Path.GetFileNameWithoutExtension(File) : "<unknown>")}'");
				noRequiresFound = true;
			}
			else requires.Add(plugin);
		}

		if (noRequiresFound)
		{
			HasFinished = true;
			Pool.FreeList(ref requires);
			yield break;
		}

		Carbon.Components.Report.OnPluginAdded?.Invoke(AsyncLoader.FilePath);

		var requiresResult = requires.ToArray();

#if DISABLE_ASYNC_LOADING
		AsyncLoader.ThreadFunction();
		AsyncLoader.IsDone = true;
#else
		AsyncLoader.Start();
#endif

		while (AsyncLoader != null && !AsyncLoader.IsDone) { yield return null; }

		if (AsyncLoader == null)
		{
			HasFinished = true;
			yield break;
		}

		if (AsyncLoader.Assembly == null || AsyncLoader.Exceptions.Count != 0)
		{
			var errors = Pool.GetList<string>();
			Logger.Error($"Failed compiling '{AsyncLoader.FilePath}':");
			for (int i = 0; i < AsyncLoader.Exceptions.Count; i++)
			{
				var error = AsyncLoader.Exceptions[i];
				var print = $"{error.Error.ErrorText}\n     ({error.Error.FileName} {error.Error.Column} line {error.Error.Line})";
				Logger.Error($"  {i + 1:n0}. {print}");
				errors.Add(print);
			}

			Loader.FailedMods.Add(new Loader.FailedMod { File = File, Errors = errors.ToArray() });

			Pool.FreeList(ref errors);
			AsyncLoader.Exceptions.Clear();
			AsyncLoader.Exceptions = null;
			HasFinished = true;
			yield break;
		}

		Logger.Warn($" Compiling '{(!string.IsNullOrEmpty(File) ? Path.GetFileNameWithoutExtension(File) : "<unknown>")}' took {AsyncLoader.CompileTime:0}ms...");

		Loader.AssemblyCache.Add(AsyncLoader.Assembly);

		var assembly = AsyncLoader.Assembly;

		foreach (var type in assembly.GetTypes())
		{
			try
			{
				if (string.IsNullOrEmpty(type.Namespace) ||
					!(type.Namespace.Equals("Oxide.Plugins") || type.Namespace.Equals("Carbon.Plugins"))) continue;

				if (Community.Runtime.Config.HookValidation)
				{
					var unsupportedHooksString = new StringBuilder();
					var counter = 0;
					foreach (var hook in AsyncLoader.UnsupportedHooks[type])
					{
						unsupportedHooksString.Append($"{hook}, ");
						counter++;
					}

					if (counter > 0)
					{
						Logger.Warn($"Plugin '{type.Name}' uses {counter:n0} hooks that are not supported: {unsupportedHooksString}and will not work as expected.");
					}

					unsupportedHooksString.Clear();
					unsupportedHooksString = null;
				}

				var info = type.GetCustomAttribute(typeof(InfoAttribute), true) as InfoAttribute;
				if (info == null) continue;

				if (requires.Any(x => x.Name == info.Title)) continue;

				var description = type.GetCustomAttribute(typeof(DescriptionAttribute), true) as DescriptionAttribute;
				var plugin = Script.Create(Source, assembly, type);

				plugin.Name = info.Title;
				plugin.Author = info.Author;
				plugin.Version = info.Version;
				plugin.Description = description?.Description;

				if (Loader.InitializePlugin(type, out CarbonPlugin rustPlugin, Mod, preInit: p =>
					{
						p._processor_instance = Instance;

						p.Hooks = AsyncLoader.Hooks[type];
						p.HookMethods = AsyncLoader.HookMethods[type];
						p.PluginReferences = AsyncLoader.PluginReferences[type];

						p.Requires = requiresResult;
						p.SetProcessor(Community.Runtime.ScriptProcessor);
						p.CompileTime = AsyncLoader.CompileTime;

						p.FilePath = AsyncLoader.FilePath;
						p.FileName = AsyncLoader.FileName;
					}))
				{
					rustPlugin.HasConditionals = Source.Contains("#if ");

					plugin.Instance = rustPlugin;
					plugin.IsCore = IsCore;

					Loader.AppendAssembly(plugin.Name, AsyncLoader.Assembly);
					Scripts.Add(plugin);

					Carbon.Components.Report.OnPluginCompiled?.Invoke(plugin.Instance, AsyncLoader.UnsupportedHooks[type]);
				}
			}
			catch (Exception exception)
			{
				HasFinished = true;
				Logger.Error($"Failed to compile: ", exception);
			}

			yield return _serverExhale;
		}

		foreach (var uhList in AsyncLoader.UnsupportedHooks)
		{
			uhList.Value.Clear();
		}

		AsyncLoader.Hooks.Clear();
		AsyncLoader.UnsupportedHooks.Clear();
		AsyncLoader.HookMethods.Clear();
		AsyncLoader.PluginReferences.Clear();

		AsyncLoader.Hooks = null;
		AsyncLoader.UnsupportedHooks = null;
		AsyncLoader.HookMethods = null;
		AsyncLoader.PluginReferences = null;

		HasFinished = true;

		if (Community.Runtime.ScriptProcessor.AllPendingScriptsComplete())
		{
			Loader.OnPluginProcessFinished();
		}

		Pool.FreeList(ref requires);
		yield return null;
	}

	public void Dispose()
	{

	}

	[Serializable]
	public class Script : IDisposable, IScript
	{
		public Assembly Assembly { get; set; }
		public Type Type { get; set; }

		public string Name { get; set; }
		public string Author { get; set; }
		public VersionNumber Version { get; set; }
		public string Description { get; set; }
		public string Source { get; set; }
		public IScriptLoader Loader { get; set; }
		public CarbonPlugin Instance { get; set; }
		public bool IsCore { get; set; }

		public static Script Create(string source, Assembly assembly, Type type)
		{
			return new Script
			{
				Source = source,
				Assembly = assembly,
				Type = type,

				Name = null,
				Author = null,
				Version = new VersionNumber(1, 0, 0),
				Description = null,
			};
		}

		public void Dispose()
		{
			Assembly = null;
			Type = null;
		}

		public override string ToString()
		{
			return $"{Name} v{Version}";
		}
	}
}
