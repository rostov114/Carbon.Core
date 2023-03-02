using System;
using System.Collections.Generic;
using System.Reflection;
using Carbon.Contracts;
using Carbon.Plugins.Features;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Base.Interfaces;

public interface IHookableModule : IModule, IDisposable
{
	Dictionary<string, List<MethodInfo>> HookCache { get; }
}
