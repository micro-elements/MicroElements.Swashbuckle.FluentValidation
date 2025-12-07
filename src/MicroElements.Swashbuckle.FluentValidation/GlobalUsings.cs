// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// OpenApi 2.x uses Microsoft.OpenApi namespace for models (no .Models sub-namespace)
// OpenApi 1.x uses Microsoft.OpenApi.Models namespace
#if OPENAPI_V2
global using Microsoft.OpenApi;
#else
global using Microsoft.OpenApi.Models;
#endif
