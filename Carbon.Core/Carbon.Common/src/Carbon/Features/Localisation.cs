﻿using System.Collections.Generic;
using System.IO;
using Carbon.Core;
using Carbon.Extensions;
using Carbon.Plugins;
using Newtonsoft.Json;

namespace Carbon.Features
{
	public class Localisation
	{
		public Dictionary<string, Dictionary<string, string>> Phrases { get; set; } = new Dictionary<string, Dictionary<string, string>>();

		public Localisation(Plugin plugin)
		{
			foreach (var directory in Directory.EnumerateDirectories(Defines.GetLangFolder()))
			{
				var lang = Path.GetFileName(directory);
				var messages = GetMessageFile(plugin.Name, lang);

				if (messages != null)
				{
					if (!Phrases.ContainsKey(lang)) Phrases.Add(lang, messages);
					else Phrases[lang] = messages;
				}
			}
		}

		public string GetLanguage(string userId)
		{
			if (!string.IsNullOrEmpty(userId) && Community.Runtime.CorePlugin.Permission.PlayerExists(userId, out var data))
			{
				return data.Language;
			}

			return Community.Runtime.Config.Language;
		}
		public string[] GetLanguages(Plugin plugin = null)
		{
			var list = Facepunch.Pool.GetList<string>();

			foreach (var text in Directory.GetDirectories(Defines.GetLangFolder () ))
			{
				if (Directory.GetFiles(text).Length != 0 && (plugin == null || (plugin != null && OsEx.File.Exists(Path.Combine(text, plugin.Name + ".json")))))
				{
					list.Add(text.Substring(Defines.GetLangFolder ().Length + 1));
				}
			}

			var result = list.ToArray();
			Facepunch.Pool.FreeList(ref list);
			return result;
		}
		public void SetLanguage(string lang, string userId)
		{
			if (string.IsNullOrEmpty(lang) || string.IsNullOrEmpty(userId)) return;

			if (Community.Runtime.CorePlugin.Permission.PlayerExists(userId, out var data))
			{
				data.Language = lang;
				Community.Runtime.CorePlugin.Permission.SaveData();
			}
		}
		public void SetServerLanguage(string lang)
		{
			if (string.IsNullOrEmpty(lang) || lang == Community.Runtime.Config.Language) return;

			Community.Runtime.Config.Language = lang;
			Community.Runtime.SaveConfig();
		}
		public string GetServerLanguage()
		{
			return Community.Runtime.Config.Language;
		}

		internal Dictionary<string, string> GetMessageFile(string plugin, string lang = "en")
		{
			if (string.IsNullOrEmpty(plugin)) return null;

			var invalidFileNameChars = Path.GetInvalidFileNameChars();

			foreach (char oldChar in invalidFileNameChars)
			{
				lang = lang.Replace(oldChar, '_');
			}

			var path = Path.Combine(Defines.GetLangFolder(), lang, $"{plugin}.json");

			if (!OsEx.File.Exists(path))
			{
				return null;
			}

			return JsonConvert.DeserializeObject<Dictionary<string, string>>(OsEx.File.ReadText(path));
		}
		internal void SaveMessageFile(string plugin, string lang = "en")
		{
			if (Phrases.TryGetValue(lang, out var messages))
			{
				var folder = Path.Combine(Defines.GetLangFolder(), lang);
				OsEx.Folder.Create(folder);

				OsEx.File.Create(Path.Combine(folder, $"{plugin}.json"), JsonConvert.SerializeObject(messages, Formatting.Indented)); ;
			}
		}

		public void RegisterMessages(Dictionary<string, string> newPhrases, Plugin plugin, string lang = "en")
		{
			if (!Phrases.TryGetValue(lang, out var phrases))
			{
				Phrases.Add(lang, phrases = newPhrases);
			}

			var save = false;

			foreach (var phrase in newPhrases)
			{
				if (!phrases.TryGetValue(phrase.Key, out var value))
				{
					phrases.Add(phrase.Key, phrase.Value);
					save = true;
				}
				else if (phrase.Value != value)
				{
					phrases[phrase.Key] = phrase.Value;
					save = true;
				}
			}

			if (newPhrases == phrases || save) SaveMessageFile(plugin.Name, lang);
		}

		public string GetMessage(string key, Plugin plugin, string player = null)
		{
			var lang = GetLanguage(player);

			if (Phrases.TryGetValue(lang, out var messages) && messages.TryGetValue(key, out var phrase))
			{
				return phrase;
			}
			else
			{
				messages = GetMessageFile(plugin.Name, lang);
				SaveMessageFile(plugin.Name, lang);

				if (messages.TryGetValue(key, out phrase))
				{
					return phrase;
				}
			}

			return key;
		}
		public Dictionary<string, string> GetMessages(string lang, Plugin plugin)
		{
			return GetMessageFile(plugin.Name, lang);
		}
	}
}
