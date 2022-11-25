///
/// Copyright (c) 2022 Carbon Community 
/// All rights reserved
/// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Carbon.Base;
using Carbon.Core;
using Carbon.Hooks;
using Carbon.Processors;
using Facepunch;
using HarmonyLib;

namespace Carbon.Jobs
{
	public class HookProcessingThread : BaseThreadedJob
	{
		public string HookName;
		public bool DoRequires = true;
		public bool OnlyAlwaysPatchedHooks = false;
		public HookProcessor Processor;

		public override void ThreadFunction()
		{
			if (string.IsNullOrEmpty(HookName)) return;

			var hooks = Pool.GetList<Hook>();

			hooks.AddRange(Defines.CoreHooks);
			hooks.AddRange(Defines.DynamicHooks);

			foreach (var hook in hooks)
			{
				try
				{
					var parameters = hook.Type.GetCustomAttributes<Hook.Parameter>();
					var args = $"[{hook.Type.Name}]_";

					if (parameters != null)
					{
						foreach (var parameter in parameters)
						{
							args += $"_[{parameter.Type.Name}]{parameter.Name}";
						}
					}

					if (hook == null) continue;

					if (hook.Name == HookName)
					{
						var patchId = $"{hook.Name}{args}";
						var patch = hook.Type.GetCustomAttribute<Hook.Patch>();
						var hookInstance = (HookProcessor.HookInstance)null;

						if (!Processor.Patches.TryGetValue(HookName, out hookInstance))
						{
							Processor.Patches.Add(HookName, hookInstance = new HookProcessor.HookInstance
							{
								AlwaysPatched = hook.Type.GetCustomAttribute<Hook.AlwaysPatched>() != null
							});
						}

						if (hookInstance.AlwaysPatched && !OnlyAlwaysPatchedHooks) continue;

						if (hookInstance.Patches.Any(x => x != null && x is HarmonyLib.Harmony harmony && harmony.Id == patchId)) continue;

						if (DoRequires)
						{
							var requires = hook.Type.GetCustomAttributes<Hook.Require>();

							if (requires != null)
							{
								foreach (var require in requires)
								{
									if (require.Hook == HookName) continue;

									Processor.InstallHooks(require.Hook, false);
								}
							}
						}

						var originalParameters = new List<Type>();
						var prefix = hook.Type.GetMethod("Prefix");
						var postfix = hook.Type.GetMethod("Postfix");
						var transpiler = hook.Type.GetMethod("Transpiler");

						foreach (var param in (prefix ?? postfix ?? transpiler).GetParameters())
						{
							originalParameters.Add(param.ParameterType);
						}
						var originalParametersResult = originalParameters.ToArray();

						var matchedParameters = transpiler != null ? patch.Parameters : patch.UseProvidedParameters ? originalParametersResult : Processor.GetMatchedParameters(patch.Type, patch.Method, (prefix ?? postfix ?? transpiler).GetParameters());

						var instance = new HarmonyLib.Harmony(patchId);
						var originalMethod = patch.Type.GetMethod(patch.Method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, matchedParameters, default);

						instance.Patch(originalMethod,
							prefix: prefix == null ? null : new HarmonyLib.HarmonyMethod(prefix),
							postfix: postfix == null ? null : new HarmonyLib.HarmonyMethod(postfix),
							transpiler: transpiler == null ? null : new HarmonyLib.HarmonyMethod(transpiler));
						hookInstance.Patches.Add(instance);
						hookInstance.Id = patchId;
						hookInstance.success = true;

						Logger.Debug($" -> Patched '{hook.Name}' <- {patchId}", 2);

						Pool.Free(ref matchedParameters);
						Pool.Free(ref originalParametersResult);
						originalParameters.Clear();
						originalParameters = null;
					}
				}
#if DEBUG
				catch (HarmonyException e)
				{
					StringBuilder sb = new StringBuilder();
					sb.AppendLine($" Couldn't patch hook '{HookName}' ({e.GetType()}: {hook.Type.FullName})");
					sb.AppendLine($">> hook:{HookName} index:{e.GetErrorIndex()} offset:{e.GetErrorOffset()}");
					sb.AppendLine($">> IL instructions:");

					foreach (var q in e.GetInstructionsWithOffsets())
						sb.AppendLine($"\t{q.Key.ToString("X4")}: {q.Value}");

					Logger.Error(sb.ToString(), e);
					sb = default;
				}
#endif
				catch (System.Exception e)
				{
					Logger.Error($" Couldn't patch hook '{HookName}' ({e.GetType()}: {hook.Type.FullName})", e);
				}
			}

			Pool.FreeList(ref hooks);
		}
	}
}
