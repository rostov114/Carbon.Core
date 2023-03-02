using API.Events;
using System;
using API.Hooks;
using Oxide.Core;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Hooks;

public partial class Category_Static
{
	public partial class Static_ServerMgr
	{
		[HookAttribute.Patch("OnServerShutdown", typeof(ServerMgr), "Shutdown", new System.Type[] { })]
		[HookAttribute.Identifier("8a0574c7d2d9420580a5ee90a37de357")]
		[HookAttribute.Options(HookFlags.Static | HookFlags.IgnoreChecksum)]

		// Useful for saving something on server shutdown.

		public class Static_ServerMgr_8a0574c7d2d9420580a5ee90a37de357 : Patch
		{
			public static void Prefix()
			{
				Logger.Log($"Saving plugin configuration and data..");
				HookCaller.CallStaticHook("OnServerSave");
				HookCaller.CallStaticHook("OnServerShutdown");

				Logger.Log($"Saving Carbon state..");
				Events.Trigger(CarbonEvent.OnServerSave, EventArgs.Empty);

				Logger.Log($"Shutting down Carbon..");
				Community.Runtime.ScriptProcessor.Clear();
			}
		}
	}
}
