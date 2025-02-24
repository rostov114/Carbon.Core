﻿using System;
using System.Linq;
using System.Runtime.Serialization;
using API.Hooks;
using Carbon.Extensions;
using ConVar;
using Facepunch;
using Facepunch.Extend;
using Oxide.Game.Rust.Libraries;
using static ConsoleSystem;
using Command = Oxide.Game.Rust.Libraries.Command;
using Pool = Facepunch.Pool;

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
		[HookAttribute.Patch("OnRconCommand", "OnRconCommand", typeof(RCon), "OnCommand", new System.Type[] { typeof(Facepunch.RCon.Command) })]
		[HookAttribute.Identifier("ccce0832a0eb4c28bc2372f5e0812c7e")]
		[HookAttribute.Options(HookFlags.Static | HookFlags.IgnoreChecksum)]

		// Called when an RCON command is run.

		public class Static_RCon_ccce0832a0eb4c28bc2372f5e0812c7e : Patch
		{
			internal static string[] EmptyArgs = new string[0];

			public static bool Prefix(RCon.Command cmd)
			{
				if (Community.Runtime == null) return true;

				try
				{
					var split = cmd.Message.Split(ConsoleArgEx.CommandSpacing, StringSplitOptions.RemoveEmptyEntries);
					var command = split[0].Trim();

					var arguments = split.Length > 1 ? cmd.Message.Substring(command.Length + 1).SplitQuotesStrings() : EmptyArgs;
					var consoleArg = FormatterServices.GetUninitializedObject(typeof(Arg)) as Arg;
					consoleArg.Option = Option.Unrestricted;
					consoleArg.FullString = cmd.Message;
					consoleArg.Args = arguments;
				
					if (HookCaller.CallStaticHook("OnRconCommand", cmd.Ip, command, arguments) != null)
					{
						return false;
					}

					foreach (var carbonCommand in Community.Runtime.AllConsoleCommands)
					{
						if (carbonCommand.Command == command)
						{
							try
							{
								Command.FromRcon = true;
								carbonCommand.Callback?.Invoke(null, command, arguments);
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
