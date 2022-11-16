using System.Text.Json;
using System.Runtime.CompilerServices;

using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;

using VNLib.Plugins.Essentials.Users;
using VNLib.Plugins.Extensions.Loading.Sql;

namespace VNLib.Plugins.Extensions.Loading.Users
{
    /// <summary>
    /// Contains extension methods for plugins to load the "users" system
    /// </summary>
    public static class UserLoading
    {
        public const string USER_TABLE_KEY = "user_table";
        public const string USER_CUSTOM_ASSEMBLY = "user_custom_asm";        

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
                IUserManager man;

                //Try to load a custom user assembly for exporting IUserManager
                string? customAsm = pbase.PluginConfig.GetPropString(USER_CUSTOM_ASSEMBLY);
                //See if host config defined the path
                customAsm ??= pbase.HostConfig.GetPropString(USER_CUSTOM_ASSEMBLY);

                if (!string.IsNullOrWhiteSpace(customAsm))
                {
                    //Try to load a custom assembly
                    AssemblyLoader<IUserManager> loader = pbase.LoadAssembly<IUserManager>(customAsm);
                    try
                    {
                        //Return the loaded instance (may raise exception)
                        man = loader.Resource;
                    }
                    catch
                    {
                        loader.Dispose();
                        throw;
                    }
                    pbase.Log.Verbose("Loaded custom user managment assembly");
                }
                else
                {
                    //Default table name to
                    string? userTableName = "Users";
                    //Try to get the user-table element from plugin config
                    if (pbase.PluginConfig.TryGetProperty(USER_TABLE_KEY, out JsonElement userEl))
                    {
                        userTableName = userEl.GetString();
                    }
                    _ = userTableName ?? throw new KeyNotFoundException($"Missing required key '{USER_TABLE_KEY}' in config");
                    //Load user-manager
                    man = new UserManager(pbase.GetConnectionFactory(), userTableName);
                }
                return man;
            }
            return new Lazy<IUserManager>(LoadManager, LazyThreadSafetyMode.PublicationOnly);
        }
    }
}