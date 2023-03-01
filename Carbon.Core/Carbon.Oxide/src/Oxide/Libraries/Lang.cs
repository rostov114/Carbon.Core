using System.Collections.Generic;
using System.IO;
using Carbon;
using Carbon.Extensions;
using Carbon.Core;
using Newtonsoft.Json;
using Oxide.Plugins;
using Plugin = Carbon.Plugin;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Oxide.Core.Libraries;

public class Lang : Library
{
	public Lang(Plugin plugin)
	{

	}

	public string GetLanguage(string userId)
	{

	}
	public string[] GetLanguages(Plugin plugin = null)
	{

	}
	public void SetLanguage(string lang, string userId)
	{

	}
	public void SetServerLanguage(string lang)
	{

	}
	public string GetServerLanguage()
	{

	}
	private Dictionary<string, string> GetMessageFile(string plugin, string lang = "en")
	{
	}
	private void SaveMessageFile(string plugin, string lang = "en")
	{

	}

	public void RegisterMessages(Dictionary<string, string> newPhrases, Plugin plugin, string lang = "en")
	{

	}

	public string GetMessage(string key, RustPlugin plugin, string player = null)
	{

	}
	public Dictionary<string, string> GetMessages(string lang, RustPlugin plugin)
	{
		
	}
}
