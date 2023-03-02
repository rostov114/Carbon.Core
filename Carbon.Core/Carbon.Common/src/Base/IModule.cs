using System.Collections.Generic;
using System.Reflection;
using Carbon.Contracts;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Base.Interfaces;

public interface IModule : IInitializable, IPluginMetadata
{
	void Init();
	void InitEnd();
	void Save();

	void SetEnabled(bool enabled);
	bool GetEnabled();
	void OnEnableStatus();

	void OnEnabled(bool initialized);
	void OnDisabled(bool initialized);
}
