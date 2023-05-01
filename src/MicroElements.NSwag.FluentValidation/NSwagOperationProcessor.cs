using System;
using System.Linq;
using MicroElements.OpenApi.Core;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace MicroElements.NSwag.FluentValidation
{
    public class NSwagOperationProcessor : IOperationProcessor
    {
        /// <inheritdoc />
        public bool Process(OperationProcessorContext context)
        {
            var aspNetCoreOperationProcessorContext = context as AspNetCoreOperationProcessorContext;
            var apiDescription = aspNetCoreOperationProcessorContext?.ApiDescription;

            if (apiDescription is null)
                return true;

            var openApiOperation = context.OperationDescription.Operation;

            var openApiParameters = context.OperationDescription.Operation.Parameters.NotNull().ToArray();

            if (openApiParameters.Length > 0)
            {
                foreach (var openApiParameter in openApiParameters)
                {

                }
            }

            return true;
        }
    }
}