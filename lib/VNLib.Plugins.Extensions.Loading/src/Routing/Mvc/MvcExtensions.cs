/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: RoutingExtensions.cs 
*
* RoutingExtensions.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Net.Http;
using VNLib.Utils;
using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Accounts;
using VNLib.Plugins.Essentials.Endpoints;
using VNLib.Plugins.Essentials.Sessions;


namespace VNLib.Plugins.Extensions.Loading.Routing.Mvc
{
    /// <summary>
    /// Provides extension and helper classes for routing using MVC architecture
    /// </summary>
    public static class MvcExtensions
    {
        /// <summary>
        /// Routes all endpoints for the specified controller
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="controller">The controller instance to route endpoints for</param>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static T Route<T>(this PluginBase plugin, T? controller) where T : IHttpController
        {
            //If a null controller is passed (normal case) then create a new instance
            controller ??= plugin.CreateService<T>();

            IEndpoint[] staticEndpoints = GetStaticEndpointsForController(plugin, controller);

            Array.ForEach(staticEndpoints, plugin.Route);

            return controller;
        }

        /// <summary>
        /// Routes all endpoints for the specified controller
        /// </summary>
        /// <param name="plugin"></param>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static T Route<T>(this PluginBase plugin) where T : IHttpController 
            => plugin.Route(default(T));

        private static IEndpoint[] GetStaticEndpointsForController<T>(PluginBase plugin, T controller)
          where T : IHttpController
        {
            IConfigScope? config = plugin.TryGetConfigForType<T>();
            ILogProvider logger = RoutingExtensions.ConfigureLogger<T>(plugin, config);

            StaticRouteHandler[] staticRoutes = GetStaticRoutes(controller, config);

            if(plugin.IsDebug())
            {
                (string, string, string)[] eps = staticRoutes
                    .Select(static p => (p.Path, p.Route.Method.ToString(), p.WorkFunc.GetMethodInfo().Name))
                    .ToArray();

                plugin.Log.Verbose("Routing static endpoints: {eps}", eps);
            }

            return BuildStaticRoutes(controller, logger, staticRoutes);
        }


        private static StaticRouteHandler[] GetStaticRoutes<T>(T controller, IConfigScope? config)
            where T : IHttpController
        {
            List<StaticRouteHandler> routes = [];

            foreach (MethodInfo method in typeof(T).GetMethods())
            {
                HttpStaticRouteAttribute? route = method.GetCustomAttribute<HttpStaticRouteAttribute>();
                HttpRouteProtectionAttribute? protection = method.GetCustomAttribute<HttpRouteProtectionAttribute>();

                if (route is null)
                {
                    continue;
                }

                routes.Add(new StaticRouteHandler
                {
                    Parent      = controller,
                    Route       = route,
                    Protection  = HttpProtectionHandler.Create(protection),
                    Path        = RoutingExtensions.SubsituteConfigStringValue(route.Path, config),   //Path may have config variables to substitute
                    WorkFunc    = CreateWorkFunc(controller, method)                //Extract the processor delegate from the method
                });
            }

            return [.. routes];

            static EndpointWorkFunc CreateWorkFunc(T controller, MethodInfo method)
            {
                //Create the delegate for the method
                EndpointWorkFunc? del = method.CreateDelegate<EndpointWorkFunc>(controller);

                return del ?? throw new InvalidOperationException($"Failed to create delegate for method {method.Name}");
            }
        }

        private static StaticEndpoint[] BuildStaticRoutes(IHttpController parent, ILogProvider logger, StaticRouteHandler[] routes)
        {
            //Group routes with the same path together
            IEnumerable<RoutesWithSamePathGroup> groups = routes
                .GroupBy(static p => p.Path)
                .Select(static p => new RoutesWithSamePathGroup(p.Key, [.. p]));

            //Get endpoints for all groups that share the same endpoint path
            return groups
                .Select(i => new StaticEndpoint(i.Routes, logger, parent, i.Path))
                .ToArray();
        }

        /*
         * A static endpoint maps functions from within http controllres labeled 
         * with the HttpStaticRouteAttribute to the IEndpoint interface that vnlib
         * needs to process virtual connections. 
         * 
         * This is an abstraction for architecture mapping. This endpoint will serve
         * a single path, but can server mutliple http methods.
         */
        private sealed class StaticEndpoint(IHttpController parent) : ResourceEndpointBase
        {
            /*
             * This array holds all the processor functions for each http method.
             * 
             * The array size is fixed for performance reasons, and for future compatibility
             * between the http library and this one. 32 positions shouldn't be that 
             * much memory to worry about as the handlers are reference types.
             */
            private readonly StaticRouteProcessor[] _processorFunctions = new StaticRouteProcessor[32];

            //Cache local copy incase the parent call creates too much overhead
            private readonly ProtectionSettings _protection = parent.GetProtectionSettings();

            /// <summary>
            /// <inheritdoc/>
            /// </summary>
            protected override ProtectionSettings EndpointProtectionSettings => _protection;

            internal StaticEndpoint(
                StaticRouteHandler[] routes,
                ILogProvider logger,
                IHttpController parent,
                string staticRoutePath
            )
                : this(parent)
            {
                //Ensure all routes have the same path, this is a developer error
                foreach (StaticRouteHandler route in routes)
                {
                    Debug.Assert(string.Equals(route.Path, staticRoutePath, StringComparison.OrdinalIgnoreCase));
                }

                InitPathAndLog(staticRoutePath, logger);

                InitProcessors(routes, _processorFunctions);
            }

            ///<inheritdoc/>
            protected override ERRNO PreProccess(HttpEntity entity)
            {
                return base.PreProccess(entity) && parent.PreProccess(entity);
            }

            ///<inheritdoc/>
            protected override ValueTask<VfReturnType> OnProcessAsync(HttpEntity entity)
            {
                StaticRouteProcessor handler = _processorFunctions[GetArrayOffsetForMethod(entity.Server.Method)];

                if (!handler.Protection.CheckProtection(entity))
                {
                    //Allow the protection handler to define a custom response code
                    entity.CloseResponse(handler.Protection.ErrorCode);
                    return new(VfReturnType.VirtualSkip);
                }

                return handler.WorkFunction(entity);
            }

            /*
             * This function will get an array offset that corresponds
             * to the bit position of the calling method. This is used to
             * get the processing function for the desired http method.
             */
            private static int GetArrayOffsetForMethod(HttpMethod method)
            {
                return BitOperations.TrailingZeroCount((long)method);
            }

            private static void InitProcessors(StaticRouteHandler[] routes, StaticRouteProcessor[] processors)
            {
                //Assign the default handler to all positions during initialization
                Array.Fill(processors, StaticRouteProcessor.DefaultProcessor);

                //Then assign each route to the correct position based on the method
                foreach (StaticRouteHandler route in routes)
                {
                    int offset = GetArrayOffsetForMethod(route.Route.Method);

                    processors[offset] = StaticRouteProcessor.FromRoute(route);
                }
            }

            private sealed class StaticRouteProcessor(
                EndpointWorkFunc workFunc,
                HttpProtectionHandler protection
            )
            {
                public readonly EndpointWorkFunc WorkFunction = workFunc;
                public readonly HttpProtectionHandler Protection = protection;

                /// <summary>
                /// Gets the default (not found) processor for static routes
                /// </summary>
                internal static readonly StaticRouteProcessor DefaultProcessor = new(
                    DefaultHandler,
                    HttpProtectionHandler.Create(null)
                );

                internal static StaticRouteProcessor FromRoute(StaticRouteHandler handler)
                    => new(handler.WorkFunc, handler.Protection);

                /*
                * This function acts as the default handler in case a route or 
                * http method is not defined
                */
                private static ValueTask<VfReturnType> DefaultHandler(HttpEntity _)
                    => new(VfReturnType.NotFound);
            }
        }


        private delegate ValueTask<VfReturnType> EndpointWorkFunc(HttpEntity entity);

        private sealed class HttpProtectionHandler
        {
            private static readonly HttpProtectionHandler _default = new();

            public readonly HttpStatusCode ErrorCode;

            private readonly bool _enabled;
            private readonly bool _allowNewSessions;
            private readonly SessionType _sesType;
            private readonly AuthorzationCheckLevel _authLevel;

            public HttpProtectionHandler(HttpRouteProtectionAttribute protectionSettings)
            {
                _enabled = true;
                _allowNewSessions = protectionSettings.AllowNewSession;
                _sesType = protectionSettings.SessionType;
                _authLevel = protectionSettings.AuthLevel;
                ErrorCode = protectionSettings.ErrorCode;
            }

            private HttpProtectionHandler()
            { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool CheckProtection(HttpEntity entity)
            {
                //If protection is disabled, always return true
                if (!_enabled)
                {
                    return true;
                }

                return entity.Session.IsSet
                    && _allowNewSessions || !entity.Session.IsNew   //May require reused sessions
                    && entity.Session.SessionType == _sesType
                    && entity.IsClientAuthorized(_authLevel);
            }

            public static HttpProtectionHandler Create(HttpRouteProtectionAttribute? attr)
            {
                return attr is null
                    ? _default
                    : new(attr);
            }
        }

        private sealed class StaticRouteHandler
        {
            public required IHttpController Parent;
            public required string Path;
            public required HttpStaticRouteAttribute Route;
            public required EndpointWorkFunc WorkFunc;
            public required HttpProtectionHandler Protection;
        }


        private record RoutesWithSamePathGroup(string Path, StaticRouteHandler[] Routes);
    }
}
