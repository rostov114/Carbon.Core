// <auto-generated/>
#pragma warning disable
#nullable enable annotations

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Indicates the type of the async method builder that should be used by a language compiler to
    /// build the attributed async method or to build the attributed type when used as the return type
    /// of an async method.
    /// </summary>
    [global::System.AttributeUsage(
        global::System.AttributeTargets.Class |
        global::System.AttributeTargets.Struct |
        global::System.AttributeTargets.Interface |
        global::System.AttributeTargets.Delegate |
        global::System.AttributeTargets.Enum |
        global::System.AttributeTargets.Method,
        Inherited = false, AllowMultiple = false)]
    public sealed class AsyncMethodBuilderAttribute : global::System.Attribute
    {
        /// <summary>Initializes the <see cref="global::System.Runtime.CompilerServices.AsyncMethodBuilderAttribute"/>.</summary>
        /// <param name="builderType">The <see cref="global::System.Type"/> of the associated builder.</param>
        public AsyncMethodBuilderAttribute(global::System.Type builderType) => BuilderType = builderType;

        /// <summary>Gets the <see cref="global::System.Type"/> of the associated builder.</summary>
        public global::System.Type BuilderType { get; }
    }
}
