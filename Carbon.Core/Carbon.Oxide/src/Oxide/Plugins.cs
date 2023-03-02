using System.Linq;
using Carbon;
using Carbon.Core;
using Carbon.Plugins;
using Facepunch;

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

	public CarbonPlugin Find(string name)
	{
		return CarbonPlugin.Find(name);
	}

	public CarbonPlugin[] GetAll()
	{
		return CarbonPlugin.GetAll();
	}
}
