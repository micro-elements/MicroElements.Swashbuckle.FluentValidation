// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Helper for resolving [AsParameters] container types when ModelMetadata.ContainerType is null.
    /// This occurs on .NET 8 where EndpointMetadataApiDescriptionProvider does not populate ContainerType.
    /// </summary>
    internal static class AsParametersHelper
    {
        /// <summary>
        /// Resolves the container type for a parameter by inspecting [AsParameters] on MethodInfo.
        /// Returns null if no [AsParameters] parameter contains a property matching parameterName.
        /// </summary>
        internal static Type? ResolveContainerType(string parameterName, MethodInfo? methodInfo)
        {
            if (methodInfo == null)
                return null;

            foreach (var param in methodInfo.GetParameters())
            {
                if (param.GetCustomAttribute<AsParametersAttribute>() == null)
                    continue;

                var paramType = param.ParameterType;
                var property = paramType.GetProperties()
                    .FirstOrDefault(p => p.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));

                if (property != null)
                    return paramType;
            }

            return null;
        }

        /// <summary>
        /// Extracts MethodInfo from an ApiDescription via ActionDescriptor.EndpointMetadata.
        /// Used by DocumentFilter which does not have direct MethodInfo access.
        /// </summary>
        internal static MethodInfo? GetMethodInfo(ApiDescription apiDescription)
        {
            return apiDescription.ActionDescriptor?.EndpointMetadata?
                .OfType<MethodInfo>()
                .FirstOrDefault();
        }
    }
}
