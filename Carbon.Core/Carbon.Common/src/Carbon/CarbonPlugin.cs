using Carbon.Components;
using Carbon.Core;
using Oxide.Core;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Plugins;

public class CarbonPlugin : Plugin
{
	public CUI.Handler CuiHandler { get; set; }

	public virtual void SetupMod(Loader.CarbonMod mod, string name, string author, VersionNumber version, string description)
	{
		_carbon = mod;
		Setup(name, author, version, description);
	}
	public virtual void Setup(string name, string author, VersionNumber version, string description)
	{
		Name = name;
		Version = version;
		Author = author;
		Description = description;

		Type = GetType();

		CuiHandler = new CUI.Handler();
	}

	#region CUI

	public CUI CreateCUI()
	{
		return new CUI(CuiHandler);
	}

	#endregion
}
