/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: MvcExtensions.cs 
*
* MvcExtensions.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
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
using System.Numerics;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Net.Http;
using VNLib.Utils;
using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Endpoints;
using VNLib.Plugins.Extensions.Loading.Configuration;

using static VNLib.Plugins.Extensions.Loading.Routing.RoutingExtensions;


namespace VNLib.Plugins.Extensions.Loading.Routing.Mvc
{
    /// <summary>
    /// Provides extension and helper classes for routing using MVC architecture.
    /// </summary>
    public static class MvcExtensions
    {
        /// <summary>
        /// Routes all endpoints for the specified controller instance. Creates a new instance if <paramref name="controller"/>
        /// is null using the plugin dependency manager.
        /// </summary>
        /// <param name="router">The endpoint router.</param>
        /// <param name="controller">The controller instance to route endpoints for.</param>
        /// <param name="guards">An optional array of additional controller guards to be added for pre-processing</param>
        /// <exception cref="ObjectDisposedException">The plugin or its dependencies have been disposed.</exception>
        /// <exception cref="InvalidOperationException">The controller or endpoint configuration is invalid.</exception>
        /// <remarks>
        /// If a <see langword="null" /> controller is passed, a new instance will be created by the plugin and routed.
        /// </remarks>
        public static T Add<T>(this in EndpointRouter router, T? controller, IHttpControllerGuard[] guards) where T : IHttpController
        {
            ArgumentNullException.ThrowIfNull(guards);

            //If a null controller is passed (normal case) then create a new instance
            controller ??= router.Plugin
                .Deps()
                .Create<T>();

            IEndpoint[] staticEndpoints = GetStaticEndpointsForController(router.Plugin, controller, guards);           

            foreach (IEndpoint endpoint in staticEndpoints)
            {
                router.Add(endpoint);
            }

            return controller;
        }

        /// <inheritdoc cref="Add{T}(in EndpointRouter, T?, IHttpControllerGuard[])"/>
        public static T Add<T>(this in EndpointRouter router, T? controller) where T : IHttpController 
            => Add(in router, controller, guards: []);  // pass empty guards array

        /// <inheritdoc cref="Add{T}(in EndpointRouter, T?, IHttpControllerGuard[])"/>
        public static T Add<T>(this in EndpointRouter router, IHttpControllerGuard[] guards) where T : IHttpController
            => Add<T>(in router, controller: default, guards);

        /// <summary>
        /// Routes all endpoints for the specified controller type, using a new instance of the controller created by the plugin.
        /// </summary>
        /// <param name="router">The endpoint router.</param>
        /// <returns>The routed controller.</returns>
        /// <exception cref="ObjectDisposedException">The plugin or its dependencies have been disposed.</exception>
        /// <exception cref="InvalidOperationException">The controller or endpoint configuration is invalid.</exception>
        public static T Add<T>(this in EndpointRouter router) where T : IHttpController
            => Add<T>(in router, controller: default);     

        /// <summary>
        /// Routes all endpoints for the specified controller using the provided instance.
        /// </summary>
        /// <typeparam name="T">The <see cref="IHttpController"/> type to route.</typeparam>
        /// <param name="plugin">The plugin for which to route the controller.</param>
        /// <param name="controller">The controller instance to route endpoints for.</param>
        /// <returns>The routed controller.</returns>
        [Obsolete("This method is deprecated, please use the Routes().Add() method.")]
        public static T Route<T>(this PluginBase plugin, T? controller) where T : IHttpController
        {
            return plugin.Host()
                .Routes()
                .Add(controller);
        }

        /// <summary>
        /// Routes all endpoints for the specified controller.
        /// </summary>
        /// <param name="plugin">The plugin for which to route the controller.</param>
        /// <exception cref="ObjectDisposedException">The plugin or its dependencies have been disposed.</exception>
        /// <exception cref="InvalidOperationException">The controller or endpoint configuration is invalid.</exception>
        [Obsolete("This method is deprecated, please use the Routes().Add() method.")]
        public static T Route<T>(this PluginBase plugin) where T : IHttpController
        {
            return plugin.Host()
              .Routes()
              .Add<T>();
        }

        private static IEndpoint[] GetStaticEndpointsForController<T>(PluginBase plugin, T controller, IHttpControllerGuard[] guards)
          where T : IHttpController
        {
            IConfigScope? config = plugin.Config().TryGetForType<T>();
            ILogProvider logger = RoutingExtensions.ConfigureLogger<T>(plugin, config);

            StaticRouteDefinition[] staticRoutes = GetStaticRoutes(controller, config);

            if (plugin.IsDebug())
            {
                (string, string, string)[] eps = staticRoutes
                    .Select(static p => (p.Path, p.Route.Method.ToString(), p.WorkFunc.GetMethodInfo().Name))
                    .ToArray();

                plugin.Log.Verbose("Routing static endpoints: {eps}", eps);
            }

            return BuildStaticRoutes(controller, logger, guards, staticRoutes);
        }

        private static StaticRouteDefinition[] GetStaticRoutes<T>(
            T controller,
            IConfigScope? config
        )
            where T : IHttpController
        {
            List<StaticRouteDefinition> routes = [];

            foreach (MethodInfo method in typeof(T).GetMethods())
            {
                HttpStaticRouteAttribute? route = method.GetCustomAttribute<HttpStaticRouteAttribute>();

                if (route is null)
                {
                    continue;
                }

                //Path may have config variables to substitute
                string? routePath = SubstituteConfigStringValue(config, route.Path, @default: null);
                Validate.NotNull(routePath, $"Route path for {method.Name} was null or undefined in configuration");               
                Validate.Matches(
                      routePath,
                      pattern: @"^\/\S*$",
                      message: $"Endpoint '{method.Name}' path '{routePath}' is not a valid path. It must start with a '/' and contain no whitespace."
                  );

                routes.Add(new StaticRouteDefinition
                {
                    Parent      = controller,
                    Route       = route,                   
                    Path        = routePath,
                    WorkFunc    = CreateHandlerDelegate(controller, method)        //Extract the processor delegate from the method
                });
            }

            return [.. routes];

            static EndpointWorkFunc CreateHandlerDelegate(T controller, MethodInfo method)
            {
                //Create the delegate for the method
                EndpointWorkFunc? del = method.CreateDelegate<EndpointWorkFunc>(controller);

                return del ?? throw new InvalidOperationException($"Failed to create delegate for method {method.Name}");
            }
        }

        private static StaticEndpoint[] BuildStaticRoutes(
            IHttpController parent, 
            ILogProvider logger, 
            IHttpControllerGuard[] routeGuards,
            StaticRouteDefinition[] routes
        )
        {
            //Group routes with the same path together
            IEnumerable<RoutesWithSamePathGroup> groups = routes
                .GroupBy(static p => p.Path)
                .Select(static p => new RoutesWithSamePathGroup(p.Key, [.. p]));          

            //Get endpoints for all groups that share the same endpoint path
            return groups
                .Select(i => new StaticEndpoint(new(i.Path, parent, routeGuards), logger, i.Routes))
                .ToArray();
        }

        /*
         * A static endpoint maps functions from within http controllers labeled 
         * with the HttpStaticRouteAttribute to the IEndpoint interface that vnlib
         * needs to process virtual connections. 
         * 
         * This is an abstraction for architecture mapping. This endpoint will serve
         * a single path, but can serve multiple http methods.
         */
        private sealed class StaticEndpoint(StaticRouteControlInfo info) : ResourceEndpointBase
        {
            /*
             * This array holds all the processor functions for each http method.
             * 
             * The array size is fixed for performance reasons, and for future compatibility
             * between the http library and this one. 33 positions shouldn't be that 
             * much memory to worry about as the handlers are reference types.
             */
            private readonly StaticRouteProcessor[] _processorFunctions = new StaticRouteProcessor[33];

            //Cache local copy incase the parent call creates too much overhead
            private readonly ProtectionSettings _protection = info.ParentController.GetProtectionSettings();          

            /// <inheritdoc/>
            protected override ProtectionSettings EndpointProtectionSettings => _protection;

            internal StaticEndpoint(
                StaticRouteControlInfo info,
                ILogProvider logger,
                StaticRouteDefinition[] routes               
            )
                : this(info)
            {
                //Ensure all routes have the same path, this is a developer error
                foreach (StaticRouteDefinition route in routes)
                {
                    Debug.Assert(string.Equals(route.Path, info.RoutePath, StringComparison.OrdinalIgnoreCase));
                }

                InitEndpoint(info.RoutePath, logger);

                InitProcessors(routes, _processorFunctions);
            }

            ///<inheritdoc/>
            protected override ERRNO PreProcess(HttpEntity entity)
            {
                // Preserve return code from base pre-processes before running controller's pre-process method
                ERRNO baseResult = base.PreProcess(entity);          
                if (baseResult <= 0)
                {
                    return baseResult;
                }

                if (!info.ParentController.PreProcess(entity))
                {
                    return ERRNO.E_FAIL;
                }

                //Evaluate controller-level guards in order, short-circuit on first rejection
                foreach (IHttpControllerGuard guard in info.RouteGuards)
                {
                    if (!guard.PreProcess(entity))
                    {
                        return ERRNO.E_FAIL;
                    }
                }

                return true;
            }

            ///<inheritdoc/>
            protected override ValueTask<VfReturnType> OnProcessAsync(HttpEntity entity)
            {
                int methodOffset = GetArrayOffsetForMethod(entity.Server.Method);

                StaticRouteProcessor handler = _processorFunctions[methodOffset];

                return handler.WorkFunction(entity);
            }

            /*
             * This function will get an array offset that corresponds
             * to the bit position of the calling method. This is used to
             * get the processing function for the desired http method.
             */
            private static int GetArrayOffsetForMethod(HttpMethod method)
            {
                return BitOperations.TrailingZeroCount((int)method);
            }

            private static void InitProcessors(StaticRouteDefinition[] routes, StaticRouteProcessor[] processors)
            {
                //Assign the default handler to all positions during initialization
                Array.Fill(processors, StaticRouteProcessor.DefaultProcessor);

                //Then assign each route to the correct position based on the method
                foreach (StaticRouteDefinition route in routes)
                {
                    int offset = GetArrayOffsetForMethod(route.Route.Method);

                    processors[offset] = StaticRouteProcessor.FromRoute(route);
                }
            }

            private sealed class StaticRouteProcessor(EndpointWorkFunc workFunc)
            {
                public readonly EndpointWorkFunc WorkFunction = workFunc;

                /// <summary>
                /// Gets the default processor for static routes that returns a not-found result.
                /// </summary>
                internal static readonly StaticRouteProcessor DefaultProcessor = new(DefaultHandler);

                internal static StaticRouteProcessor FromRoute(StaticRouteDefinition handler)
                    => new(handler.WorkFunc);

                /*
                * This function acts as the default handler in case a route or 
                * http method is not defined
                */
                private static ValueTask<VfReturnType> DefaultHandler(HttpEntity _)
                    => new(VfReturnType.NotFound);
            }
        }


        private delegate ValueTask<VfReturnType> EndpointWorkFunc(HttpEntity entity);
    
        private readonly record struct StaticRouteControlInfo(
            string RoutePath,
            IHttpController ParentController,
            IHttpControllerGuard[] RouteGuards
        );

        private sealed class StaticRouteDefinition
        {
            public required IHttpController Parent;
            public required string Path;
            public required HttpStaticRouteAttribute Route;
            public required EndpointWorkFunc WorkFunc;
        }

        private record RoutesWithSamePathGroup(string Path, StaticRouteDefinition[] Routes);
    }
}
