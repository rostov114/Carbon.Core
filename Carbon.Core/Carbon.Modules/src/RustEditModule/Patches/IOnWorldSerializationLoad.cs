﻿using API.Hooks;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Modules;

public partial class RustEditModule
{
	[HookAttribute.Patch("IOnWorldSerializationLoad", "IOnWorldSerializationLoad", typeof(WorldSerialization), "Load", new System.Type[] { typeof(string) })]
	[HookAttribute.Identifier("46e7586c847e42b392ec0c14b75b9451")]
	[HookAttribute.Options(HookFlags.Hidden)]

	// Called before and after WorldSerialization is loaded.

	public class World_WorldSerialization_Load_46e7586c847e42b392ec0c14b75b9451 : API.Hooks.Patch
	{
		public static void Prefix(string fileName, ref WorldSerialization __instance)
		{
			HookCaller.CallStaticHook("IOnWorldSerializationLoad", fileName, __instance);
		}

		public static void Postfix(string fileName, ref WorldSerialization __instance)
		{
			HookCaller.CallStaticHook("IOnWorldSerializationLoaded", fileName, __instance);
		}
	}
}
