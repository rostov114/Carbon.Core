using System;
using System.IO;
using Carbon.Features;
using Newtonsoft.Json;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Oxide.Core.Configuration;

public abstract class ConfigFile : JsonConfig
{
	protected ConfigFile(string fileName) : base(fileName)
	{

	}
}
