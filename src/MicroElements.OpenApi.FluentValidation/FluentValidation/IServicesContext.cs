// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MicroElements.OpenApi.FluentValidation
{
    /// <summary>
    /// Services that can be injected with DI.
    /// </summary>
    public interface IServicesContext
    {
        /// <summary>
        /// Gets optional <see cref="INameResolver"/>.
        /// </summary>
        INameResolver? NameResolver { get; }
    }

    /// <summary>
    /// Services that can be injected with DI.
    /// </summary>
    public class ServicesContext : IServicesContext
    {
        /// <inheritdoc />
        public INameResolver? NameResolver { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServicesContext"/> class.
        /// </summary>
        /// <param name="nameResolver">Optional name resolver.</param>
        public ServicesContext(INameResolver? nameResolver = null)
        {
            NameResolver = nameResolver;
        }
    }
}