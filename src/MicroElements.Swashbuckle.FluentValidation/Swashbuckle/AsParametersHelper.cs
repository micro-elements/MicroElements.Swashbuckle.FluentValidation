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
        /// Finds the action parameter type that exposes <paramref name="firstSegment"/> as a property — the
        /// root container of a flattened nested [FromQuery] parameter. Unlike <see cref="ResolveContainerType"/>
        /// this does not require an [AsParameters] attribute (MVC [FromQuery] types are plain parameters).
        /// Returns null when no parameter exposes such a property. Issue #209/#211.
        /// </summary>
        internal static Type? ResolveRootType(string firstSegment, MethodInfo? methodInfo)
        {
            if (methodInfo == null)
                return null;

            foreach (var parameter in methodInfo.GetParameters())
            {
                if (parameter.ParameterType.GetProperty(firstSegment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) != null)
                    return parameter.ParameterType;
            }

            return null;
        }

        /// <summary>
        /// Extracts MethodInfo from an ApiDescription. For MVC controllers the action method lives on
        /// <see cref="Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor"/>; for minimal APIs
        /// it is exposed via EndpointMetadata. Used by DocumentFilter which has no direct MethodInfo access.
        /// </summary>
        internal static MethodInfo? GetMethodInfo(ApiDescription apiDescription)
        {
            if (apiDescription.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor controllerActionDescriptor)
                return controllerActionDescriptor.MethodInfo;

            return apiDescription.ActionDescriptor?.EndpointMetadata?
                .OfType<MethodInfo>()
                .FirstOrDefault();
        }
    }
}
