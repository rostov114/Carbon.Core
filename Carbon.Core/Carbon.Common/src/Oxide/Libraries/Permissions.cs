﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Carbon;
using Oxide.Game.Rust.Libraries.Covalence;
using Oxide.Plugins;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Oxide.Core.Libraries;

public class Permission : Library
{
	public enum SerializationMode
	{
		Protobuf,
		SQL
	}

	public bool IsGlobal
	{
		get
		{
			return false;
		}
	}

	public bool IsLoaded { get; private set; }

	public Permission()
	{
		permset = new Dictionary<Plugin, HashSet<string>>();

		RegisterValidate(delegate (string value)
		{
			return ulong.TryParse(value, out var output) && ((output == 0UL) ? 1 : ((int)Math.Floor(Math.Log10(output) + 1.0))) >= 17;
		});

		LoadFromDatafile();
		CleanUp();
	}

	public static char[] Star = new char[] { '*' };
	public static string[] EmptyStringArray = new string[0];

	private readonly Dictionary<Plugin, HashSet<string>> permset;
	private Dictionary<string, UserData> userdata = new();
	private Dictionary<string, GroupData> groupdata = new();
	private Func<string, bool> validate;

	private static FieldInfo _iPlayerFieldCache;
	public static FieldInfo iPlayerField => _iPlayerFieldCache ??= typeof(BasePlayer).GetField("IPlayer", BindingFlags.Public | BindingFlags.Instance);

	public virtual void LoadFromDatafile()
	{
		Utility.DatafileToProto<Dictionary<string, UserData>>("oxide.users", true);
		Utility.DatafileToProto<Dictionary<string, GroupData>>("oxide.groups", true);

		var needsUserSave = false;
		var needsGroupSave = false;

		userdata = (ProtoStorage.Load<Dictionary<string, UserData>>("oxide.users") ?? new Dictionary<string, UserData>());
		{
			var validatedUsers = new Dictionary<string, UserData>(StringComparer.OrdinalIgnoreCase);
			var groupSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var permissionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var data in userdata)
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
				if (validatedUsers.TryGetValue(data.Key, out var userData))
				{
					userData.Perms.UnionWith(value.Perms);
					userData.Groups.UnionWith(value.Groups);
					needsUserSave = true;
				}
				else
				{
					validatedUsers.Add(data.Key, value);
				}
			}

			permissionSet.Clear();
			groupSet.Clear();
			userdata.Clear();
			userdata = null;
			userdata = validatedUsers;
		}

		groupdata = (ProtoStorage.Load<Dictionary<string, GroupData>>("oxide.groups") ?? new Dictionary<string, GroupData>());
		{
			var validatedGroups = new Dictionary<string, GroupData>(StringComparer.OrdinalIgnoreCase);
			var permissionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var data in groupdata)
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

			foreach (var data in groupdata)
			{
				if (!string.IsNullOrEmpty(data.Value.ParentGroup) && HasCircularParent(data.Key, data.Value.ParentGroup))
				{
					Logger.Warn("Detected circular parent group for '{keyValuePair.Key}'! Removing parent '{keyValuePair.Value.ParentGroup}'");
					data.Value.ParentGroup = null;
					needsGroupSave = true;
				}
			}

			permissionSet.Clear();
			groupdata.Clear();
			groupdata = null;
			groupdata = validatedGroups;
		}

		if (!GroupExists("default")) CreateGroup("default", "default", 0);
		if (!GroupExists("admin")) CreateGroup("admin", "admin", 1);

		IsLoaded = true;

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
		if (!IsLoaded) return;

		Interface.Oxide.DataFileSystem.WriteObject(prefix + ".groups", groupdata, false);
		Interface.Oxide.DataFileSystem.WriteObject(prefix + ".users", userdata, false);
	}

	public virtual void SaveData()
	{
		SaveUsers();
		SaveGroups();
	}
	public virtual void SaveUsers()
	{
		ProtoStorage.Save(userdata, "oxide.users");
	}
	public virtual void SaveGroups()
	{
		ProtoStorage.Save(groupdata, "oxide.groups");
	}

	public virtual void RegisterValidate(Func<string, bool> val)
	{
		validate = val;
	}

	public virtual void CleanUp()
	{
		if (!IsLoaded || validate == null) return;

		var array = (from k in userdata.Keys
					 where !validate(k)
					 select k).ToArray();

		if (array.Length == 0) return;

		foreach (string key in array)
		{
			userdata.Remove(key);
		}

		Array.Clear(array, 0, array.Length);
		array = null;
	}

	public virtual void MigrateGroup(string oldGroup, string newGroup)
	{
		if (!IsLoaded) return;

		if (GroupExists(oldGroup))
		{
			var fileDataPath = ProtoStorage.GetFileDataPath("oxide.groups.data");

			File.Copy(fileDataPath, fileDataPath + ".old", true);
			foreach (string perm in GetGroupPermissions(oldGroup, false))
			{
				GrantGroupPermission(newGroup, perm, null);
			}

			if (GetUsersInGroup(oldGroup).Length == 0)
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

		if (!permset.TryGetValue(owner, out var hashSet))
		{
			hashSet = new HashSet<string>();
			permset.Add(owner, hashSet);
		}
		hashSet.Add(name);
		Interface.CallHook("OnPermissionRegistered", name, owner);
	}

	public virtual void UnregisterPermissions(Plugin owner)
	{
		if (owner == null) return;

		if (permset.TryGetValue(owner, out var hashSet))
		{
			hashSet.Clear();
			permset.Remove(owner);
			Interface.CallHook("OnPermissionsUnregistered", owner);
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
			if (permset.Count > 0)
			{
				if (name.Equals("*"))
				{
					return true;
				}
				if (name.EndsWith("*"))
				{
					name = name.TrimEnd(Star);
					return permset.Values.SelectMany((HashSet<string> v) => v).Any((string p) => p.StartsWith(name));
				}
			}
			return permset.Values.Any((HashSet<string> v) => v.Contains(name));
		}

		if (!permset.TryGetValue(owner, out var hashSet)) return false;

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

	public virtual bool UserIdValid(string id)
	{
		return validate == null || validate(id);
	}

	public virtual bool UserExists(string id)
	{
		return userdata.ContainsKey(id);
	}

	public virtual bool UserExists(string id, out UserData data)
	{
		return userdata.TryGetValue(id, out data);
	}

	public virtual UserData GetUserData(string id)
	{
		if (!userdata.TryGetValue(id, out var result))
		{
			userdata.Add(id, result = new UserData());
		}

		return result;
	}

	public virtual KeyValuePair<string, UserData> FindUser(string id)
	{
		id = id.ToLower().Trim();

		if (id.IsSteamId()) GetUserData(id);

		foreach (var user in userdata)
		{
			if (user.Value != null && user.Key == id || (!string.IsNullOrEmpty(user.Value.LastSeenNickname) && user.Value.LastSeenNickname.ToLower().Trim().Contains(id)))
				return new KeyValuePair<string, UserData>(user.Key, user.Value);
		}

		return default;
	}

	public virtual void RefreshUser(BasePlayer player)
	{
		if (player == null) return;

		var user = GetUserData(player.UserIDString);
		user.LastSeenNickname = player.displayName;

		if (player.net != null && player.net.connection != null && player.net.connection.info != null)
			user.Language = player.net.connection.info.GetString("global.language", "en");
		else user.Language = "en";

		AddUserGroup(player.UserIDString, "default");

		if (player.IsAdmin)
		{
			AddUserGroup(player.UserIDString, "admin");
		}
		else if (UserHasGroup(player.UserIDString, "admin"))
		{
			RemoveUserGroup(player.UserIDString, "admin");
		}

		if (iPlayerField.GetValue(player) == null) iPlayerField.SetValue(player, new RustPlayer(player));
	}
	public virtual void UpdateNickname(string id, string nickname)
	{
		if (UserExists(id))
		{
			var userData = GetUserData(id);
			var lastSeenNickname = userData.LastSeenNickname;
			var obj = nickname.Sanitize();
			userData.LastSeenNickname = nickname.Sanitize();
			Interface.CallHook("OnUserNameUpdated", id, lastSeenNickname, obj);
		}
	}

	public virtual bool UserHasAnyGroup(string id)
	{
		return UserExists(id) && GetUserData(id).Groups.Count > 0;
	}
	public virtual bool GroupsHavePermission(HashSet<string> groups, string perm)
	{
		return groups.Any((string group) => GroupHasPermission(group, perm));
	}
	public virtual bool GroupHasPermission(string name, string perm)
	{
		return GroupExists(name) && !string.IsNullOrEmpty(perm) && groupdata.TryGetValue(name.ToLower(), out var groupData) && (groupData.Perms.Contains(perm.ToLower()) || GroupHasPermission(groupData.ParentGroup, perm));
	}
	public virtual bool UserHasPermission(string id, string perm)
	{
		if (string.IsNullOrEmpty(perm)) return false;
		if (id.Equals("server_console")) return true;

		perm = perm.ToLower();
		var userData = GetUserData(id);
		return userData.Perms.Contains(perm) || GroupsHavePermission(userData.Groups, perm);
	}

	public virtual string[] GetUserGroups(string id)
	{
		return GetUserData(id).Groups.ToArray();
	}
	public virtual string[] GetUserPermissions(string id)
	{
		var userData = GetUserData(id);
		var list = userData.Perms.ToList();
		foreach (string name in userData.Groups)
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

		if (!groupdata.TryGetValue(name.ToLower(), out var groupData))
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
		return new HashSet<string>(permset.Values.SelectMany((HashSet<string> v) => v)).ToArray();
	}
	public virtual string[] GetPermissionUsers(string perm)
	{
		if (string.IsNullOrEmpty(perm)) return EmptyStringArray;

		perm = perm.ToLower();
		var hashSet = new HashSet<string>();
		foreach (KeyValuePair<string, UserData> keyValuePair in userdata)
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
		foreach (KeyValuePair<string, GroupData> keyValuePair in groupdata)
		{
			if (keyValuePair.Value.Perms.Contains(perm))
			{
				hashSet.Add(keyValuePair.Key);
			}
		}
		return hashSet.ToArray();
	}

	public virtual void AddUserGroup(string id, string name)
	{
		if (!GroupExists(name)) return;
		if (!GetUserData(id).Groups.Add(name.ToLower())) return;

		HookCaller.CallStaticHook("OnUserGroupAdded", id, name);
	}
	public virtual void RemoveUserGroup(string id, string name)
	{
		if (!GroupExists(name)) return;

		var userData = GetUserData(id);
		if (name.Equals("*"))
		{
			if (userData.Groups.Count <= 0) return;

			userData.Groups.Clear();
			return;
		}
		else
		{
			if (!userData.Groups.Remove(name.ToLower())) return;

			HookCaller.CallStaticHook("OnUserGroupRemoved", id, name);
			return;
		}
	}
	public virtual bool UserHasGroup(string id, string name)
	{
		return GroupExists(name) && GetUserData(id).Groups.Contains(name.ToLower());
	}
	public virtual bool GroupExists(string group)
	{
		return !string.IsNullOrEmpty(group) && (group.Equals("*") || groupdata.ContainsKey(group.ToLower()));
	}

	public virtual string[] GetGroups()
	{
		return groupdata.Keys.ToArray();
	}
	public virtual string[] GetUsersInGroup(string group)
	{
		if (!GroupExists(group)) return EmptyStringArray;

		group = group.ToLower();
		return (from u in userdata
				where u.Value.Groups.Contains(@group)
				select u.Key + " (" + u.Value.LastSeenNickname + ")").ToArray();
	}

	public virtual string GetGroupTitle(string group)
	{
		if (!GroupExists(group)) return string.Empty;

		if (!groupdata.TryGetValue(group.ToLower(), out var groupData))
		{
			return string.Empty;
		}
		return groupData.Title;
	}
	public virtual int GetGroupRank(string group)
	{
		if (!GroupExists(group)) return 0;
		if (!groupdata.TryGetValue(group.ToLower(), out var groupData)) return 0;

		return groupData.Rank;
	}

	public virtual bool GrantUserPermission(string id, string perm, Plugin owner)
	{
		if (!PermissionExists(perm, owner)) return false;

		var data = GetUserData(id);
		perm = perm.ToLower();
		if (perm.EndsWith("*"))
		{
			HashSet<string> source;
			if (owner == null)
			{
				source = new HashSet<string>(permset.Values.SelectMany((HashSet<string> v) => v));
			}
			else if (!permset.TryGetValue(owner, out source))
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
	public virtual bool RevokeUserPermission(string id, string perm)
	{
		if (string.IsNullOrEmpty(perm)) return false;

		var userData = GetUserData(id);
		perm = perm.ToLower();
		if (perm.EndsWith("*"))
		{
			if (!perm.Equals("*"))
			{
				perm = perm.TrimEnd(Star);
				return userData.Perms.RemoveWhere((string s) => s.StartsWith(perm)) > 0;
			}
			if (userData.Perms.Count <= 0) return false;

			userData.Perms.Clear();
			return true;
		}
		else
		{
			if (!userData.Perms.Remove(perm)) return false;

			HookCaller.CallStaticHook("OnUserPermissionRevoked", id, perm);
			return true;
		}
	}
	public virtual bool GrantGroupPermission(string name, string perm, Plugin owner)
	{
		if (!PermissionExists(perm, owner) || !GroupExists(name)) return false;

		if (!groupdata.TryGetValue(name.ToLower(), out var data)) return false;
		perm = perm.ToLower();

		if (perm.EndsWith("*"))
		{
			HashSet<string> source;
			if (owner == null)
			{
				source = new HashSet<string>(permset.Values.SelectMany((HashSet<string> v) => v));
			}
			else if (!permset.TryGetValue(owner, out source))
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
		if (!groupdata.TryGetValue(name.ToLower(), out var groupData)) return false;

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

		var value = new GroupData
		{
			Title = title,
			Rank = rank
		};
		group = group.ToLower();
		groupdata.Add(group, value);
		Interface.CallHook("OnGroupCreated", group, title, rank);
		return true;
	}
	public virtual bool RemoveGroup(string group)
	{
		if (!GroupExists(group)) return false;

		group = group.ToLower();
		var flag = groupdata.Remove(group);
		if (flag)
		{
			foreach (var groupData in groupdata.Values)
			{
				if (groupData.ParentGroup != group) continue;

				groupData.ParentGroup = string.Empty;
			}
		}
		if (userdata.Values.Aggregate(false, (bool current, UserData userData) => current | userData.Groups.Remove(group)))
		{
			SaveUsers();
		}
		if (flag)
		{
			Interface.CallHook("OnGroupDeleted", group);
		}
		return true;
	}

	public virtual bool SetGroupTitle(string group, string title)
	{
		if (!GroupExists(group)) return false;
		group = group.ToLower();

		if (!groupdata.TryGetValue(group, out var groupData)) return false;
		if (groupData.Title == title) return true;
		groupData.Title = title;
		Interface.CallHook("OnGroupTitleSet", group, title);
		return true;
	}
	public virtual bool SetGroupRank(string group, int rank)
	{
		if (!GroupExists(group)) return false;
		group = group.ToLower();
		if (!groupdata.TryGetValue(group, out var groupData)) return false;
		if (groupData.Rank == rank) return true;
		groupData.Rank = rank;
		Interface.CallHook("OnGroupRankSet", group, rank);
		return true;
	}

	public virtual string GetGroupParent(string group)
	{
		if (!GroupExists(group)) return string.Empty;
		group = group.ToLower();
		if (groupdata.TryGetValue(group, out var groupData))
		{
			return groupData.ParentGroup;
		}
		return string.Empty;
	}
	public virtual bool SetGroupParent(string group, string parent)
	{
		if (!GroupExists(group)) return false;
		group = group.ToLower();

		if (!groupdata.TryGetValue(group, out var groupData)) return false;

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
		Interface.CallHook("OnGroupParentSet", group, parent);
		return true;
	}
	public virtual bool HasCircularParent(string group, string parent)
	{
		if (!groupdata.TryGetValue(parent, out var groupData))
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
			if (!groupdata.TryGetValue(groupData.ParentGroup, out groupData)) return false;
		}
		return false;
	}
}
