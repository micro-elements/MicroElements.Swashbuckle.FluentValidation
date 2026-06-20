// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using FluentValidation;

namespace MicroElements.OpenApi.FluentValidation.FileUpload
{
    /// <summary>
    /// Reads file-upload metadata from a validator using the SAME filtered rule traversal that the schema
    /// pipeline uses (<see cref="FluentValidationExtensions.GetValidationRules"/> /
    /// <see cref="FluentValidationExtensions.GetValidators"/>). This guarantees that a conditional
    /// <c>.FileContentType(...).When(...)</c> rule is included/excluded identically to the size rules — keeping
    /// the emitted encoding consistent with the rest of the document (e.g. <see cref="ConditionalRulesMode"/>).
    /// </summary>
    public static class FileUploadIntrospection
    {
        /// <summary>
        /// Enumerates <see cref="IFileContentTypeValidator"/> instances declared on the given validator, paired
        /// with the resolved member name they are attached to (e.g. <c>File</c>).
        /// </summary>
        /// <param name="validator">Validator of the form container type.</param>
        /// <param name="schemaType">Form container type.</param>
        /// <param name="options">Schema generation options (drives the rule/component filtering).</param>
        /// <returns>Member name and content-type metadata pairs.</returns>
        public static IEnumerable<(string MemberName, IFileContentTypeValidator Meta)> GetFileContentTypeValidators(
            IValidator validator,
            Type schemaType,
            ISchemaGenerationOptions options)
        {
            var typeContext = new TypeContext(schemaType, options);
            var validatorContext = new ValidatorContext(typeContext, validator);

            foreach (var ruleContext in validatorContext.GetValidationRules())
            {
                var memberName = ruleContext.ValidationRule.PropertyName;
                if (string.IsNullOrEmpty(memberName))
                    continue;

                foreach (var propertyValidator in ruleContext.GetValidators())
                {
                    if (propertyValidator is IFileContentTypeValidator meta)
                        yield return (memberName!, meta);
                }
            }
        }
    }
}
