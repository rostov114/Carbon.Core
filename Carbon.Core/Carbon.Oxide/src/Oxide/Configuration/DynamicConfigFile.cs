using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Carbon;
using Carbon.Features;
using Newtonsoft.Json;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Oxide.Core.Configuration;

public class DynamicConfigFile : JsonConfig, IEnumerable<KeyValuePair<string, object>>, IEnumerable
{
	public DynamicConfigFile(string fileName) : base(fileName) { }

	public string Filename { get => FileName; set => FileName = value; }

	public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
	{
		return _keyvalues.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return _keyvalues.GetEnumerator();
	}
}
