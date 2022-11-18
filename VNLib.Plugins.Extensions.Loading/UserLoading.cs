using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.Users;

namespace VNLib.Plugins.Extensions.Loading.Users
{
    /// <summary>
    /// Contains extension methods for plugins to load the "users" system
    /// </summary>
    public static class UserLoading
    {
        public const string USER_CUSTOM_ASSEMBLY = "user_custom_asm";
        public const string DEFAULT_USER_ASM = "VNLib.Plugins.Essentials.Users.dll";
        public const string ONLOAD_METHOD_NAME = "OnPluginLoading";

        private static readonly ConditionalWeakTable<PluginBase, Lazy<IUserManager>> UsersTable = new();

        /// <summary>
        /// Gets or loads the plugin's ambient <see cref="IUserManager"/>, with the specified user-table name,
        /// or the default table name
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>The ambient <see cref="IUserManager"/> for the current plugin</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static IUserManager GetUserManager(this PluginBase plugin)
        {
            plugin.ThrowIfUnloaded();
            //Get stored or load
            return UsersTable.GetValue(plugin, LoadUsers).Value;
        }

        private static Lazy<IUserManager> LoadUsers(PluginBase pbase)
        {
            //lazy callack
            IUserManager LoadManager()
            {
                //Try to load a custom user assembly for exporting IUserManager
                string? customAsm = pbase.PluginConfig.GetPropString(USER_CUSTOM_ASSEMBLY);
                //See if host config defined the path
                customAsm ??= pbase.HostConfig.GetPropString(USER_CUSTOM_ASSEMBLY);
                //Finally default
                customAsm ??= DEFAULT_USER_ASM;

                //Try to load a custom assembly
                AssemblyLoader<IUserManager> loader = pbase.LoadAssembly<IUserManager>(customAsm);
                try
                {
                    //Get the runtime type
                    Type runtimeType = loader.Resource.GetType();

                    //Get the onplugin load method
                    Action<object>? onLoadMethod = runtimeType.GetMethods()
                        .Where(static p => p.IsPublic && !p.IsAbstract && ONLOAD_METHOD_NAME.Equals(p.Name))
                        .Select(p => p.CreateDelegate<Action<object>>(loader.Resource))
                        .FirstOrDefault();

                    //Call the onplugin load method
                    onLoadMethod?.Invoke(pbase);

                    if (pbase.IsDebug())
                    {
                        pbase.Log.Verbose("Loading user manager from assembly {name}", runtimeType.AssemblyQualifiedName);
                    }

                    //Return the loaded instance (may raise exception)
                    return loader.Resource;
                }
                catch
                {
                    loader.Dispose();
                    throw;
                }
            }
            return new Lazy<IUserManager>(LoadManager, LazyThreadSafetyMode.PublicationOnly);
        }
    }
}