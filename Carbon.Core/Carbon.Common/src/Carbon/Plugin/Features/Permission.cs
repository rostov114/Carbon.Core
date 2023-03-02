using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Carbon.Extensions;
using Carbon.Plugins;
using Epic.OnlineServices.UI;
using Steamworks;

namespace Carbon.Features
{
	public class Permission
	{
		public enum SerializationMode
		{
			Protobuf,
			SQL
		}

		internal static char[] Star = new char[] { '*' };
		internal static string[] EmptyStringArray = new string[0];

		protected readonly Dictionary<Plugin, HashSet<string>> _permissions;
		protected Dictionary<string, Player> _players = new();
		protected Dictionary<string, Group> _groups = new();
		protected Func<string, bool> _validator;
		protected bool _isLoaded;

		public Permission()
		{
			_permissions = new();
			_validator = value => ulong.TryParse(value, out var output) && ((output == 0UL) ? 1 : ((int)Math.Floor(Math.Log10(output) + 1.0))) >= 17;

			LoadFromDatafile();
			CleanUp();
		}

		public virtual void LoadFromDatafile()
		{
			var needsUserSave = false;
			var needsGroupSave = false;

			_players = (ProtoEx.Load<Dictionary<string, Player>>("oxide.users") ?? new Dictionary<string, Player>());
			{
				var validatedUsers = new Dictionary<string, Player>(StringComparer.OrdinalIgnoreCase);
				var groupSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				var permissionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				foreach (var data in _players)
				{
					var value = data.Value;

					permissionSet.Clear();
					groupSet.Clear();

					foreach (string item in value.Perms)
					{
						permissionSet.Add(item);
					}

					value.Perms = new HashSet<string>(permissionSet, StringComparer.OrdinalIgnoreCase);

					foreach (string item2 in value.Groups)
					{
						groupSet.Add(item2);
					}

					value.Groups = new HashSet<string>(groupSet, StringComparer.OrdinalIgnoreCase);
					if (validatedUsers.TryGetValue(data.Key, out var player))
					{
						player.Perms.UnionWith(value.Perms);
						player.Groups.UnionWith(value.Groups);
						needsUserSave = true;
					}
					else
					{
						validatedUsers.Add(data.Key, value);
					}
				}

				permissionSet.Clear();
				groupSet.Clear();
				_players.Clear();
				_players = null;
				_players = validatedUsers;
			}

			_groups = (ProtoEx.Load<Dictionary<string, Group>>("oxide.groups") ?? new Dictionary<string, Group>());
			{
				var validatedGroups = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);
				var permissionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				foreach (var data in _groups)
				{
					var value = data.Value;
					permissionSet.Clear();
					foreach (var item in value.Perms)
					{
						permissionSet.Add(item);
					}
					value.Perms = new HashSet<string>(permissionSet, StringComparer.OrdinalIgnoreCase);
					if (validatedGroups.ContainsKey(data.Key))
					{
						validatedGroups[data.Key].Perms.UnionWith(value.Perms);
						needsGroupSave = true;
					}
					else
					{
						validatedGroups.Add(data.Key, value);
					}
				}

				foreach (var data in _groups)
				{
					if (!string.IsNullOrEmpty(data.Value.ParentGroup) && HasCircularParent(data.Key, data.Value.ParentGroup))
					{
						Logger.Warn("Detected circular parent group for '{keyValuePair.Key}'! Removing parent '{keyValuePair.Value.ParentGroup}'");
						data.Value.ParentGroup = null;
						needsGroupSave = true;
					}
				}

				permissionSet.Clear();
				_groups.Clear();
				_groups = null;
				_groups = validatedGroups;
			}

			if (!GroupExists("default")) CreateGroup("default", "default", 0);
			if (!GroupExists("admin")) CreateGroup("admin", "admin", 1);

			_isLoaded = true;

			if (needsUserSave)
			{
				SaveUsers();
			}

			if (needsGroupSave)
			{
				SaveGroups();
			}
		}


		public virtual void Export(string prefix = "auth")
		{
			if (!_isLoaded) return;

			ProtoEx.Save(_groups, prefix + ".groups");
			ProtoEx.Save(_players, prefix + ".players");
		}

		public virtual void SaveData()
		{
			SaveUsers();
			SaveGroups();
		}
		public virtual void SaveUsers()
		{
			ProtoEx.Save(_players, "oxide.users");
		}
		public virtual void SaveGroups()
		{
			ProtoEx.Save(_groups, "oxide.groups");
		}

		public virtual void RegisterValidate(Func<string, bool> val)
		{
			_validator = val;
		}

		public virtual void CleanUp()
		{
			if (!_isLoaded || _validator == null) return;

			var array = (from k in _players.Keys
						 where !_validator(k)
						 select k).ToArray();

			if (array.Length == 0) return;

			foreach (string key in array)
			{
				_players.Remove(key);
			}

			Array.Clear(array, 0, array.Length);
			array = null;
		}

		public virtual void MigrateGroup(string oldGroup, string newGroup)
		{
			if (!_isLoaded) return;

			if (GroupExists(oldGroup))
			{
				var fileDataPath = ProtoEx.GetFileDataPath("oxide.groups.data");

				OsEx.File.Copy(fileDataPath, fileDataPath + ".old", true);

				foreach (string perm in GetGroupPermissions(oldGroup, false))
				{
					GrantGroupPermission(newGroup, perm, null);
				}

				if (GetPlayersInGroup(oldGroup).Length == 0)
				{
					RemoveGroup(oldGroup);
				}
			}
		}

		public virtual void RegisterPermission(string name, Plugin owner)
		{
			if (string.IsNullOrEmpty(name)) return;

			name = name.ToLower();
			if (PermissionExists(name, null))
			{
				Logger.Warn($"Duplicate permission registered '{name}' (by plugin '{owner.Name}')");
				return;
			}

			if (!_permissions.TryGetValue(owner, out var hashSet))
			{
				hashSet = new HashSet<string>();
				_permissions.Add(owner, hashSet);
			}
			hashSet.Add(name);
			HookCaller.CallStaticHook("OnPermissionRegistered", name, owner);
		}

		public virtual void UnregisterPermissions(Plugin owner)
		{
			if (owner == null) return;

			if (_permissions.TryGetValue(owner, out var hashSet))
			{
				hashSet.Clear();
				_permissions.Remove(owner);
				HookCaller.CallStaticHook("OnPermissionsUnregistered", owner);
			}
		}

		public virtual bool PermissionExists(string name, Plugin owner = null)
		{
			if (string.IsNullOrEmpty(name))
			{
				return false;
			}
			name = name.ToLower();
			if (owner == null)
			{
				if (_permissions.Count > 0)
				{
					if (name.Equals("*"))
					{
						return true;
					}
					if (name.EndsWith("*"))
					{
						name = name.TrimEnd(Star);
						return _permissions.Values.SelectMany((HashSet<string> v) => v).Any((string p) => p.StartsWith(name));
					}
				}
				return _permissions.Values.Any((HashSet<string> v) => v.Contains(name));
			}

			if (!_permissions.TryGetValue(owner, out var hashSet)) return false;

			if (hashSet.Count > 0)
			{
				if (name.Equals("*"))
				{
					return true;
				}
				if (name.EndsWith("*"))
				{
					name = name.TrimEnd(Star);
					return hashSet.Any((string p) => p.StartsWith(name));
				}
			}
			return hashSet.Contains(name);
		}

		public virtual bool PlayerIdValid(string id)
		{
			return _validator == null || _validator(id);
		}

		public virtual bool PlayerExists(string id)
		{
			return _players.ContainsKey(id);
		}

		public virtual bool PlayerExists(string id, out Player data)
		{
			return _players.TryGetValue(id, out data);
		}

		public virtual Player GetPlayer(string id)
		{
			if (!_players.TryGetValue(id, out var result))
			{
				_players.Add(id, result = new());
			}

			return result;
		}

		public virtual KeyValuePair<string, Features.Player> FindPlayer(string id)
		{
			id = id.ToLower().Trim();

			foreach (var user in _players)
			{
				if (user.Value != null && user.Key == id || (!string.IsNullOrEmpty(user.Value.LastSeenNickname) && user.Value.LastSeenNickname.ToLower().Trim().Contains(id))) return new KeyValuePair<string, Features.Player>(user.Key, user.Value);
			}

			return default;
		}

		public virtual void RefreshPlayer(BasePlayer player)
		{
			if (player == null) return;

			var user = GetPlayer(player.UserIDString);
			user.LastSeenNickname = player.displayName;

			if (player.net != null && player.net.connection != null && player.net.connection.info != null)
				user.Language = player.net.connection.info.GetString("global.language", "en");
			else user.Language = "en";

			AddPlayerGroup(player.UserIDString, "default");

			if (player.IsAdmin)
			{
				AddPlayerGroup(player.UserIDString, "admin");
			}
			else if (PlayerHasGroup(player.UserIDString, "admin"))
			{
				RemovePlayerGroup(player.UserIDString, "admin");
			}
		}
		public virtual void UpdateNickname(string id, string nickname)
		{
			if (PlayerExists(id))
			{
				var player = GetPlayer(id);
				var lastSeenNickname = player.LastSeenNickname;
				var obj = nickname.Sanitize();
				player.LastSeenNickname = nickname.Sanitize();
				HookCaller.CallStaticHook("OnUserNameUpdated", id, lastSeenNickname, obj);
			}
		}

		public virtual bool PlayerHasAnyGroup(string id)
		{
			return PlayerExists(id) && GetPlayer(id).Groups.Count > 0;
		}
		public virtual bool GroupsHavePermission(HashSet<string> groups, string perm)
		{
			return groups.Any((string group) => GroupHasPermission(group, perm));
		}
		public virtual bool GroupHasPermission(string name, string perm)
		{
			return GroupExists(name) && !string.IsNullOrEmpty(perm) && _groups.TryGetValue(name.ToLower(), out var groupData) && (groupData.Perms.Contains(perm.ToLower()) || GroupHasPermission(groupData.ParentGroup, perm));
		}
		public virtual bool PlayerHasPermission(string id, string perm)
		{
			if (string.IsNullOrEmpty(perm)) return false;
			if (id.Equals("server_console")) return true;

			perm = perm.ToLower();
			var player = GetPlayer(id);
			return player.Perms.Contains(perm) || GroupsHavePermission(player.Groups, perm);
		}

		public virtual string[] GetPlayerGroups(string id)
		{
			return GetPlayer(id).Groups.ToArray();
		}
		public virtual string[] GetPlayerPermissions(string id)
		{
			var player = GetPlayer(id);
			var list = player.Perms.ToList();
			foreach (string name in player.Groups)
			{
				list.AddRange(GetGroupPermissions(name, false));
			}
			return new HashSet<string>(list).ToArray();
		}
		public virtual string[] GetGroupPermissions(string name, bool parents = false)
		{
			if (!GroupExists(name))
			{
				return EmptyStringArray;
			}

			if (!_groups.TryGetValue(name.ToLower(), out var groupData))
			{
				return EmptyStringArray;
			}

			var list = groupData.Perms.ToList();
			if (parents)
			{
				list.AddRange(GetGroupPermissions(groupData.ParentGroup, false));
			}
			return new HashSet<string>(list).ToArray();
		}
		public virtual string[] GetPermissions()
		{
			return new HashSet<string>(_permissions.Values.SelectMany((HashSet<string> v) => v)).ToArray();
		}
		public virtual string[] GetPermissionPlayers(string perm)
		{
			if (string.IsNullOrEmpty(perm)) return EmptyStringArray;

			perm = perm.ToLower();
			var hashSet = new HashSet<string>();

			foreach (var keyValuePair in _players)
			{
				if (keyValuePair.Value.Perms.Contains(perm))
				{
					hashSet.Add(keyValuePair.Key + "(" + keyValuePair.Value.LastSeenNickname + ")");
				}
			}
			return hashSet.ToArray();
		}
		public virtual string[] GetPermissionGroups(string perm)
		{
			if (string.IsNullOrEmpty(perm)) return EmptyStringArray;

			perm = perm.ToLower();
			var hashSet = new HashSet<string>();

			foreach (var keyValuePair in _groups)
			{
				if (keyValuePair.Value.Perms.Contains(perm))
				{
					hashSet.Add(keyValuePair.Key);
				}
			}
			return hashSet.ToArray();
		}

		public virtual void AddPlayerGroup(string id, string name)
		{
			if (!GroupExists(name)) return;
			if (!GetPlayer(id).Groups.Add(name.ToLower())) return;

			HookCaller.CallStaticHook("OnUserGroupAdded", id, name);
		}
		public virtual void RemovePlayerGroup(string id, string name)
		{
			if (!GroupExists(name)) return;

			var player = GetPlayer(id);
			if (name.Equals("*"))
			{
				if (player.Groups.Count <= 0) return;

				player.Groups.Clear();
				return;
			}
			else
			{
				if (!player.Groups.Remove(name.ToLower())) return;

				HookCaller.CallStaticHook("OnUserGroupRemoved", id, name);
				return;
			}
		}
		public virtual bool PlayerHasGroup(string id, string name)
		{
			return GroupExists(name) && GetPlayer(id).Groups.Contains(name.ToLower());
		}
		public virtual bool GroupExists(string group)
		{
			return !string.IsNullOrEmpty(group) && (group.Equals("*") || _groups.ContainsKey(group.ToLower()));
		}

		public virtual string[] GetGroups()
		{
			return _groups.Keys.ToArray();
		}
		public virtual string[] GetPlayersInGroup(string group)
		{
			if (!GroupExists(group)) return EmptyStringArray;

			group = group.ToLower();
			return (from u in _players
					where u.Value.Groups.Contains(@group)
					select u.Key + " (" + u.Value.LastSeenNickname + ")").ToArray();
		}

		public virtual string GetGroupTitle(string group)
		{
			if (!GroupExists(group)) return string.Empty;

			if (!_groups.TryGetValue(group.ToLower(), out var groupData))
			{
				return string.Empty;
			}
			return groupData.Title;
		}
		public virtual int GetGroupRank(string group)
		{
			if (!GroupExists(group)) return 0;
			if (!_groups.TryGetValue(group.ToLower(), out var groupData)) return 0;

			return groupData.Rank;
		}

		public virtual bool GrantPlayerPermission(string id, string perm, Plugin owner)
		{
			if (!PermissionExists(perm, owner)) return false;

			var data = GetPlayer(id);
			perm = perm.ToLower();
			if (perm.EndsWith("*"))
			{
				HashSet<string> source;
				if (owner == null)
				{
					source = new HashSet<string>(_permissions.Values.SelectMany((HashSet<string> v) => v));
				}
				else if (!_permissions.TryGetValue(owner, out source))
				{
					return false;
				}
				if (perm.Equals("*"))
				{
					source.Aggregate(false, (bool c, string s) => c | data.Perms.Add(s));
					return true;
				}
				perm = perm.TrimEnd(Star);
				(from s in source
				 where s.StartsWith(perm)
				 select s).Aggregate(false, (bool c, string s) => c | data.Perms.Add(s));
				return true;
			}
			else
			{
				if (!data.Perms.Add(perm)) return false;

				HookCaller.CallStaticHook("OnUserPermissionGranted", id, perm);
				return true;
			}
		}
		public virtual bool RevokePlayerPermission(string id, string perm)
		{
			if (string.IsNullOrEmpty(perm)) return false;

			var player = GetPlayer(id);
			perm = perm.ToLower();
			if (perm.EndsWith("*"))
			{
				if (!perm.Equals("*"))
				{
					perm = perm.TrimEnd(Star);
					return player.Perms.RemoveWhere((string s) => s.StartsWith(perm)) > 0;
				}
				if (player.Perms.Count <= 0) return false;

				player.Perms.Clear();
				return true;
			}
			else
			{
				if (!player.Perms.Remove(perm)) return false;

				HookCaller.CallStaticHook("OnUserPermissionRevoked", id, perm);
				return true;
			}
		}
		public virtual bool GrantGroupPermission(string name, string perm, Plugin owner)
		{
			if (!PermissionExists(perm, owner) || !GroupExists(name)) return false;

			if (!_groups.TryGetValue(name.ToLower(), out var data)) return false;
			perm = perm.ToLower();

			if (perm.EndsWith("*"))
			{
				HashSet<string> source;
				if (owner == null)
				{
					source = new HashSet<string>(_permissions.Values.SelectMany((HashSet<string> v) => v));
				}
				else if (!_permissions.TryGetValue(owner, out source))
				{
					return false;
				}
				if (perm.Equals("*"))
				{
					source.Aggregate(false, (bool c, string s) => c | data.Perms.Add(s));
					return true;
				}
				perm = perm.TrimEnd(Star).ToLower();
				(from s in source
				 where s.StartsWith(perm)
				 select s).Aggregate(false, (bool c, string s) => c | data.Perms.Add(s));
				return true;
			}
			else
			{
				if (!data.Perms.Add(perm)) return false;

				HookCaller.CallStaticHook("OnGroupPermissionGranted", name, perm);
				return true;
			}
		}
		public virtual bool RevokeGroupPermission(string name, string perm)
		{
			if (!GroupExists(name) || string.IsNullOrEmpty(perm)) return false;
			if (!_groups.TryGetValue(name.ToLower(), out var groupData)) return false;

			perm = perm.ToLower();
			if (perm.EndsWith("*"))
			{
				if (!perm.Equals("*"))
				{
					perm = perm.TrimEnd(Star).ToLower();
					return groupData.Perms.RemoveWhere((string s) => s.StartsWith(perm)) > 0;
				}
				if (groupData.Perms.Count <= 0) return false;
				groupData.Perms.Clear();
				return true;
			}
			else
			{
				if (!groupData.Perms.Remove(perm)) return false;

				HookCaller.CallStaticHook("OnGroupPermissionRevoked", name, perm);
				return true;
			}
		}

		public virtual bool CreateGroup(string group, string title, int rank)
		{
			if (GroupExists(group) || string.IsNullOrEmpty(group)) return false;

			var value = new Group
			{
				Title = title,
				Rank = rank
			};
			group = group.ToLower();
			_groups.Add(group, value);
			HookCaller.CallStaticHook("OnGroupCreated", group, title, rank);
			return true;
		}
		public virtual bool RemoveGroup(string group)
		{
			if (!GroupExists(group)) return false;

			group = group.ToLower();
			var flag = _groups.Remove(group);
			if (flag)
			{
				foreach (var groupData in _groups.Values)
				{
					if (groupData.ParentGroup != group) continue;

					groupData.ParentGroup = string.Empty;
				}
			}
			if (_players.Values.Aggregate(false, (bool current, Features.Player player) => current | player.Groups.Remove(group)))
			{
				SaveUsers();
			}
			if (flag)
			{
				HookCaller.CallStaticHook("OnGroupDeleted", group);
			}
			return true;
		}

		public virtual bool SetGroupTitle(string group, string title)
		{
			if (!GroupExists(group)) return false;
			group = group.ToLower();

			if (!_groups.TryGetValue(group, out var groupData)) return false;
			if (groupData.Title == title) return true;
			groupData.Title = title;
			HookCaller.CallStaticHook("OnGroupTitleSet", group, title);
			return true;
		}
		public virtual bool SetGroupRank(string group, int rank)
		{
			if (!GroupExists(group)) return false;
			group = group.ToLower();
			if (!_groups.TryGetValue(group, out var groupData)) return false;
			if (groupData.Rank == rank) return true;
			groupData.Rank = rank;
			HookCaller.CallStaticHook("OnGroupRankSet", group, rank);
			return true;
		}

		public virtual string GetGroupParent(string group)
		{
			if (!GroupExists(group)) return string.Empty;
			group = group.ToLower();
			if (_groups.TryGetValue(group, out var groupData))
			{
				return groupData.ParentGroup;
			}
			return string.Empty;
		}
		public virtual bool SetGroupParent(string group, string parent)
		{
			if (!GroupExists(group)) return false;
			group = group.ToLower();

			if (!_groups.TryGetValue(group, out var groupData)) return false;

			if (string.IsNullOrEmpty(parent))
			{
				groupData.ParentGroup = null;
				return true;
			}
			if (!GroupExists(parent) || group.Equals(parent.ToLower())) return false;

			parent = parent.ToLower();

			if (!string.IsNullOrEmpty(groupData.ParentGroup) && groupData.ParentGroup.Equals(parent)) return true;
			if (HasCircularParent(group, parent)) return false;

			groupData.ParentGroup = parent;
			HookCaller.CallStaticHook("OnGroupParentSet", group, parent);
			return true;
		}
		public virtual bool HasCircularParent(string group, string parent)
		{
			if (!_groups.TryGetValue(parent, out var groupData))
			{
				return false;
			}
			var hashSet = new HashSet<string>
		{
			group,
			parent
		};
			while (!string.IsNullOrEmpty(groupData.ParentGroup))
			{
				if (!hashSet.Add(groupData.ParentGroup)) return true;
				if (!_groups.TryGetValue(groupData.ParentGroup, out groupData)) return false;
			}
			return false;
		}
	}
}
