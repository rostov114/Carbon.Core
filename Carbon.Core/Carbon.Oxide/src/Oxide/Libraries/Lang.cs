using System.Collections.Generic;
using System.IO;
using Carbon;
using Carbon.Extensions;
using Carbon.Core;
using Newtonsoft.Json;
using Oxide.Plugins;
using Plugin = Carbon.Plugins.Plugin;
using Carbon.Features;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Oxide.Core.Libraries;

public class Lang : Localisation
{
	public Lang(Plugin plugin) : base(plugin) { }
}
