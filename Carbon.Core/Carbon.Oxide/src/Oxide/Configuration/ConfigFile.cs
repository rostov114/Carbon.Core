using System;
using System.IO;
using Newtonsoft.Json;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Oxide.Core.Configuration;

public abstract class ConfigFile : Carbon.Plugins.Features.Config
{
	protected ConfigFile(string name) : base(name) { }

	[JsonIgnore]
	public string Filename { get; private set; }

	public static T Load<T>(string filename) where T : ConfigFile
	{
		var t = (T)Activator.CreateInstance(typeof(T), filename);
		t.Load(null);
		return t;
	}

	public virtual void Load(string filename = null)
	{
		JsonConvert.PopulateObject(File.ReadAllText(filename ?? this.Filename), this);
	}

	public virtual void Save(string filename = null)
	{
		string contents = JsonConvert.SerializeObject(this, Formatting.Indented);
		File.WriteAllText(filename ?? Filename, contents);
	}
}
