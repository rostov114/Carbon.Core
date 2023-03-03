using System;
using System.Linq;
using Carbon.Plugins;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Oxide.Plugins;

public class Plugins
{
	public bool Exists(string name)
	{
		return CarbonPlugin.Exists(name);
	}

	public Plugin Find(string name)
	{
		return CarbonPlugin.Find(name) as Plugin;
	}

	public Plugin[] GetAll()
	{
		var plugins = CarbonPlugin.GetAll();
		var result = plugins.Cast<Plugin>().ToArray();
		Array.Clear(plugins, 0, result.Length);

		return result;
	}
}
