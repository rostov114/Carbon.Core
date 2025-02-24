﻿using System;
using System.Collections;
using System.IO;
using Carbon.Base;
using Carbon.Components;
using Carbon.Contracts;
using Carbon.Core;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Processors;

public class ScriptProcessor : BaseProcessor, IScriptProcessor
{
	public override string Name => "Script Processor";
	public override bool EnableWatcher => Community.IsConfigReady ? Community.Runtime.Config.ScriptWatchers : true;
	public override string Folder => Defines.GetScriptFolder();
	public override string Extension => ".cs";
	public override Type IndexedType => typeof(Script);

	public bool AllPendingScriptsComplete()
	{
		foreach (var instance in InstanceBuffer)
		{
			if (instance.Value is Script script)
			{
				if (script.Loader != null && !script.Loader.HasFinished) return false;
			}
		}

		return true;
	}
	public bool AllNonRequiresScriptsComplete()
	{
		foreach (var instance in InstanceBuffer)
		{
			if (instance.Value is Script script)
			{
				if (script.Loader != null && !script.Loader.HasRequires && !script.Loader.HasFinished) return false;
			}
		}

		return true;
	}
	public bool AllExtensionsComplete()
	{
		foreach (var instance in InstanceBuffer)
		{
			if (instance.Value is Script script)
			{
				if (script.Loader != null && !script.Loader.IsExtension && !script.Loader.HasFinished) return false;
			}
		}

		return true;
	}

	void IScriptProcessor.StartCoroutine(IEnumerator coroutine)
	{
		StartCoroutine(coroutine);
	}

	public class Script : Instance, IScriptProcessor.IScript
	{
		public IScriptLoader Loader { get; set; }

		public override IBaseProcessor.IParser Parser => new ScriptParser();

		public override void Dispose()
		{
			try
			{
				Loader?.Clear();
			}
			catch (Exception ex)
			{
				Logger.Error($"Error disposing {File}", ex);
			}

			Loader = null;
		}
		public override void Execute()
		{
			try
			{
				Carbon.Core.Loader.FailedMods.RemoveAll(x => x.File == File);

				Loader = new ScriptLoader
				{
					Parser = Parser,
					File = File,
					Mod = Community.Runtime.Plugins,
					Instance = this
				};
				Loader.Load();
			}
			catch (Exception ex)
			{
				Logger.Warn($"Failed processing {Path.GetFileNameWithoutExtension(File)}:\n{ex}");
			}
		}
	}

	public class ScriptParser : Parser, IBaseProcessor.IParser
	{
		public bool IsLineValid(string line)
		{
			return true;
		}

		public override void Process(string input, out string output)
		{
			using (TimeMeasure.New("ScriptParser.Process"))
			{
				try
				{
					output = input
						.Replace("PluginTimers", "Timers");

					var newOutput = string.Empty;
					var split = output.Split('\n');

					foreach (var line in split)
					{
						if (!IsLineValid(line)) continue;

						newOutput += line + "\n";
					}

					output = newOutput;
					Array.Clear(split, 0, split.Length);
					split = null;
				}
				catch
				{
					output = input;
				}
			}
		}
	}
}
