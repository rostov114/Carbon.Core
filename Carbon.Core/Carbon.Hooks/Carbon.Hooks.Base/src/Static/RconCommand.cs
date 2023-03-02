﻿using System;
using System.Linq;
using API.Hooks;
using Carbon.Components;
using Carbon.Extensions;
using Facepunch;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Hooks;

public partial class Category_Static
{
	public partial class Static_RCon
	{
		[HookAttribute.Patch("OnRconCommand", typeof(RCon), "OnCommand", new System.Type[] { typeof(Facepunch.RCon.Command) })]
		[HookAttribute.Identifier("ccce0832a0eb4c28bc2372f5e0812c7e")]
		[HookAttribute.Options(HookFlags.Static | HookFlags.IgnoreChecksum)]

		// Called when an RCON command is run.

		public class Static_RCon_ccce0832a0eb4c28bc2372f5e0812c7e : Patch
		{
			public static bool Prefix(RCon.Command cmd)
			{
				if (Community.Runtime == null) return true;

				try
				{
					var split = cmd.Message.Split(ConsoleArgEx.CommandSpacing, StringSplitOptions.RemoveEmptyEntries);
					var command = split[0].Trim();

					var arguments = Pool.GetList<string>();
					foreach (var arg in split.Skip(1)) arguments.Add(arg.Trim());
					var args2 = arguments.ToArray();
					Pool.FreeList(ref arguments);

					if (HookCaller.CallStaticHook("OnRconCommand", cmd.Ip, command, args2) != null)
					{
						return false;
					}

					foreach (var carbonCommand in Community.Runtime.AllConsoleCommands)
					{
						if (carbonCommand.Name == command)
						{
							try
							{
								Command.FromRcon = true;
								carbonCommand.Callback?.Invoke(null, command, args2);
								return !carbonCommand.SkipOriginal;
							}
							catch (Exception ex)
							{
								Logger.Error("RconCommand_OnCommand", ex);
							}

							break;
						}
					}
				}
				catch { }

				return true;
			}
		}
	}
}
