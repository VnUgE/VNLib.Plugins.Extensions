/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: DbExtensions.cs 
*
* DbExtensions.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Data is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Data is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Data;
using System.Reflection;
using System.Data.Common;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using VNLib.Utils;
using VNLib.Utils.Memory.Caching;

namespace VNLib.Plugins.Extensions.Data.SQL
{
    /// <summary>
    /// Provides basic extension methods for ADO.NET abstract classes
    /// for rapid development
    /// </summary>
    public static class DbExtensions
    {
        /*
         * Object rental for propery dictionaries used for custom result objects
         */
        private static ObjectRental<Dictionary<string, PropertyInfo>> DictStore { get; } = ObjectRental.Create<Dictionary<string, PropertyInfo>>(null, static dict => dict.Clear(), 20);
      

        /// <summary>
        /// Creates a new <see cref="DbParameter"/> configured for <see cref="ParameterDirection.Input"/> with the specified value
        /// and adds it to the command.
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="name">The parameter name</param>
        /// <param name="value">The value of the parameter</param>
        /// <param name="type">The <see cref="DbType"/> of the column</param>
        /// <param name="nullable">Are null types allowed in the value parameter</param>
        /// <returns>The created parameter</returns>
        public static DbParameter AddParameter<T>(this DbCommand cmd, string @name, T @value, DbType @type, bool @nullable = false)
        {
            //Create the new parameter from command
            DbParameter param = cmd.CreateParameter();
            //Set parameter variables
            param.ParameterName = name;
            param.Value = value;
            param.DbType = type;
            //Force non null mapping
            param.SourceColumnNullMapping = nullable;
            //Specify input parameter
            param.Direction = ParameterDirection.Input;
            //Add param to list
            cmd.Parameters.Add(param);
            return param;
        }
        /// <summary>
        /// Creates a new <see cref="DbParameter"/> configured for <see cref="ParameterDirection.Input"/> with the specified value
        /// and adds it to the command.
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="name">The parameter name</param>
        /// <param name="value">The value of the parameter</param>
        /// <param name="type">The <see cref="DbType"/> of the column</param>
        /// <param name="size">Size of the data value</param>
        /// <param name="nullable">Are null types allowed in the value parameter</param>
        /// <returns>The created parameter</returns>
        public static DbParameter AddParameter<T>(this DbCommand cmd, string @name, T @value, DbType @type, int @size, bool @nullable = false)
        {
            DbParameter param = AddParameter(cmd, name, value, type, nullable);
            //Set size parameter
            param.Size = size;
            return param;
        }
        /// <summary>
        /// Creates a new <see cref="DbParameter"/> configured for <see cref="ParameterDirection.Output"/> with the specified value
        /// and adds it to the command.
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="name">The parameter name</param>
        /// <param name="value">The value of the parameter</param>
        /// <param name="type">The <see cref="DbType"/> of the column</param>
        /// <param name="nullable">Are null types allowed in the value parameter</param>
        /// <returns>The created parameter</returns>
        public static DbParameter AddOutParameter<T>(this DbCommand cmd, string @name, T @value, DbType @type, bool @nullable = false)
        {
            //Create the new parameter from command
            DbParameter param = AddParameter(cmd, name, value, type, nullable);
            //Specify output parameter
            param.Direction = ParameterDirection.Output;
            return param;
        }
        /// <summary>
        /// Creates a new <see cref="DbParameter"/> configured for <see cref="ParameterDirection.Output"/> with the specified value
        /// and adds it to the command.
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="name">The parameter name</param>
        /// <param name="value">The value of the parameter</param>
        /// <param name="type">The <see cref="DbType"/> of the column</param>
        /// <param name="size">Size of the data value</param>
        /// <param name="nullable">Are null types allowed in the value parameter</param>
        /// <returns>The created parameter</returns>
        public static DbParameter AddOutParameter<T>(this DbCommand cmd, string @name, T @value, DbType @type, int @size, bool @nullable = false)
        {
            DbParameter param = AddOutParameter(cmd, name, value, type, nullable);
            //Set size parameter
            param.Size = size;
            return param;
        }

        /// <summary>
        /// Creates a new <see cref="DbCommand"/> for <see cref="CommandType.Text"/> with the specified command
        /// </summary>
        /// <param name="db"></param>
        /// <param name="cmdText">The command to run against the connection</param>
        /// <returns>The initalized <see cref="DbCommand"/></returns>
        public static DbCommand CreateTextCommand(this DbConnection db, string cmdText)
        {
            //Create the new command
            DbCommand cmd = db.CreateCommand();
            cmd.CommandText = cmdText;
            cmd.CommandType = CommandType.Text;      //Specify text command
            return cmd;
        }
        /// <summary>
        /// Creates a new <see cref="DbCommand"/> for <see cref="CommandType.StoredProcedure"/> with the specified procedure name
        /// </summary>
        /// <param name="db"></param>
        /// <param name="procedureName">The name of the stored proecedure to execute</param>
        /// <returns>The initalized <see cref="DbCommand"/></returns>
        public static DbCommand CreateProcedureCommand(this DbConnection db, string procedureName)
        {
            //Create the new command
            DbCommand cmd = db.CreateCommand();
            cmd.CommandText = procedureName;
            cmd.CommandType = CommandType.StoredProcedure;      //Specify stored procedure
            return cmd;
        }

        /// <summary>
        /// Creates a new <see cref="DbCommand"/> for <see cref="CommandType.Text"/> with the specified command 
        /// on a given transaction
        /// </summary>
        /// <param name="db"></param>
        /// <param name="cmdText">The command to run against the connection</param>
        /// <param name="transaction">The transaction to execute on</param>
        /// <returns>The initalized <see cref="DbCommand"/></returns>
        public static DbCommand CreateTextCommand(this DbConnection db, string cmdText, DbTransaction transaction)
        {
            return CreateCommand(db, transaction, CommandType.Text, cmdText);
        }
        /// <summary>
        /// Shortcut to create a command on a transaction with the specifed command type and command
        /// </summary>
        /// <param name="db"></param>
        /// <param name="transaction">The transaction to complete the operation on</param>
        /// <param name="type">The command type</param>
        /// <param name="command">The command to execute</param>
        /// <returns>The intialized db command</returns>
        public static DbCommand CreateCommand(this DbConnection db, DbTransaction transaction, CommandType type, string command)
        {
            //Create the new command
            DbCommand cmd = db.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = command;
            cmd.CommandType = type;
            return cmd;
        }
        /// <summary>
        /// Creates a new <see cref="DbCommand"/> for <see cref="CommandType.StoredProcedure"/> with the specified procedure name
        /// </summary>
        /// <param name="db"></param>
        /// <param name="procedureName">The name of the stored proecedure to execute</param>
        /// <param name="transaction">The transaction to execute on</param>
        /// <returns>The initalized <see cref="DbCommand"/></returns>
        public static DbCommand CreateProcedureCommand(this DbConnection db, string procedureName, DbTransaction transaction)
        {
            return CreateCommand(db, transaction, CommandType.StoredProcedure, procedureName);
        }

        /// <summary>
        /// Reads all available rows from the reader, adapts columns to public properties with <see cref="SqlColumnName"/>
        /// attributes, and adds them to the collection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <param name="container">The container to write created objects to</param>
        /// <returns>The number of objects created and written to the collection</returns>
        public static int GetAllObjects<T>(this DbDataReader reader, ICollection<T> container) where T : new()
        {
            //make sure its worth collecting object meta
            if (!reader.HasRows)
            {
                return 0;
            }
            Type objectType = typeof(T);
            //Rent a dict of properties that have the column attribute set so we can load the proper results
            Dictionary<string, PropertyInfo> avialbleProps = DictStore.Rent();
            //Itterate through public properties
            foreach (PropertyInfo prop in objectType.GetProperties())
            {
                //try to get the column name attribute of the propery
                SqlColumnNameAttribute? colAtt = prop.GetCustomAttribute<SqlColumnNameAttribute>(true);
                //Attribute is valid and coumn name is not empty
                if (!string.IsNullOrWhiteSpace(colAtt?.ColumnName))
                {
                    //Store the property for later
                    avialbleProps[colAtt.ColumnName] = prop;
                }
            }
            //Get the column schema
            ReadOnlyCollection<DbColumn> columns = reader.GetColumnSchema();
            int count = 0;
            //Read
            while (reader.Read())
            {
                //Create the new object
                T ret = new();
                //Iterate through columns
                foreach (DbColumn col in columns)
                {
                    //Get the propery if its specified by its column-name attribute
                    if (avialbleProps.TryGetValue(col.ColumnName, out PropertyInfo? prop))
                    {
                        //make sure the column has a value
                        if (col.ColumnOrdinal.HasValue)
                        {
                            //Get the object
                            object val = reader.GetValue(col.ColumnOrdinal.Value);
                            //Set check if the row is DB null, if so set it, otherwise set the value
                            prop.SetValue(ret, Convert.IsDBNull(val) ? null : val);
                        }
                    }
                }
                //Add the object to the collection
                container.Add(ret);
                //Increment count
                count++;
            }
            //return dict (if an error occurs, just let the dict go and create a new one next time, no stress setting up a try/finally block)
            DictStore.Return(avialbleProps);
            return count;
        }
        /// <summary>
        /// Reads all available rows from the reader, adapts columns to public properties with <see cref="SqlColumnName"/>
        /// attributes, and adds them to the collection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <param name="container">The container to write created objects to</param>
        /// <returns>The number of objects created and written to the collection</returns>
        public static async ValueTask<int> GetAllObjectsAsync<T>(this DbDataReader reader, ICollection<T> container) where T : new()
        {
            //make sure its worth collecting object meta
            if (!reader.HasRows)
            {
                return 0;
            }
            Type objectType = typeof(T);
            //Rent a dict of properties that have the column attribute set so we can load the proper results
            Dictionary<string, PropertyInfo> avialbleProps = DictStore.Rent();
            //Itterate through public properties
            foreach (PropertyInfo prop in objectType.GetProperties())
            {
                //try to get the column name attribute of the propery
                SqlColumnNameAttribute? colAtt = prop.GetCustomAttribute<SqlColumnNameAttribute>(true);
                //Attribute is valid and coumn name is not empty
                if (!string.IsNullOrWhiteSpace(colAtt?.ColumnName))
                {
                    //Store the property for later
                    avialbleProps[colAtt.ColumnName] = prop;
                }
            }
            //Get the column schema
            ReadOnlyCollection<DbColumn> columns = await reader.GetColumnSchemaAsync();
            int count = 0;
            //Read
            while (await reader.ReadAsync())
            {
                //Create the new object
                T ret = new();
                //Iterate through columns
                foreach (DbColumn col in columns)
                {
                    //Get the propery if its specified by its column-name attribute
                    if (avialbleProps.TryGetValue(col.ColumnName, out PropertyInfo? prop))
                    {
                        //make sure the column has a value
                        if (col.ColumnOrdinal.HasValue)
                        {
                            //Get the object
                            object val = reader.GetValue(col.ColumnOrdinal.Value);
                            //Set check if the row is DB null, if so set it, otherwise set the value
                            prop.SetValue(ret, Convert.IsDBNull(val) ? null : val);
                        }
                    }
                }
                //Add the object to the collection
                container.Add(ret);
                //Increment count
                count++;
            }
            //return dict (if an error occurs, just let the dict go and create a new one next time, no stress setting up a try/finally block)
            DictStore.Return(avialbleProps);
            return count;
        }
        /// <summary>
        /// Reads the first available row from the reader, adapts columns to public properties with <see cref="SqlColumnName"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <returns>The created object, or default if no rows are available</returns>
        public static T? GetFirstObject<T>(this DbDataReader reader) where T : new()
        {
            //make sure its worth collecting object meta
            if (!reader.HasRows)
            {
                return default;
            }
            //Get the object type
            Type objectType = typeof(T);
            //Get the column schema
            ReadOnlyCollection<DbColumn> columns = reader.GetColumnSchema();
            //Read
            if (reader.Read())
            {
                //Rent a dict of properties that have the column attribute set so we can load the proper results
                Dictionary<string, PropertyInfo> availbleProps = DictStore.Rent();
                //Itterate through public properties
                foreach (PropertyInfo prop in objectType.GetProperties())
                {
                    //try to get the column name attribute of the propery
                    SqlColumnNameAttribute? colAtt = prop.GetCustomAttribute<SqlColumnNameAttribute>(true);
                    //Attribute is valid and coumn name is not empty
                    if (colAtt != null && !string.IsNullOrWhiteSpace(colAtt.ColumnName))
                    {
                        //Store the property for later
                        availbleProps[colAtt.ColumnName] = prop;
                    }
                }
                //Create the new object
                T ret = new();
                //Iterate through columns
                foreach (DbColumn col in columns)
                {
                    //Get the propery if its specified by its column-name attribute
                    if (availbleProps.TryGetValue(col.ColumnName, out PropertyInfo? prop) && col.ColumnOrdinal.HasValue)
                    {
                        //Get the object
                        object val = reader.GetValue(col.ColumnOrdinal.Value);
                        //Set check if the row is DB null, if so set it, otherwise set the value
                        prop.SetValue(ret, Convert.IsDBNull(val) ? null : val);
                    }
                }
                //Return dict, no stress if error occurs, the goal is lower overhead
                DictStore.Return(availbleProps);
                //Return the new object
                return ret;
            }
            return default;
        }
        /// <summary>
        /// Reads the first available row from the reader, adapts columns to public properties with <see cref="SqlColumnName"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <returns>The created object, or default if no rows are available</returns>
        public static async Task<T?> GetFirstObjectAsync<T>(this DbDataReader reader) where T : new()
        {
            //Read
            if (await reader.ReadAsync())
            {
                //Get the object type
                Type objectType = typeof(T);
                //Get the column schema
                ReadOnlyCollection<DbColumn> columns = await reader.GetColumnSchemaAsync();
                //Rent a dict of properties that have the column attribute set so we can load the proper results
                Dictionary<string, PropertyInfo> availbleProps = DictStore.Rent();
                //Itterate through public properties
                foreach (PropertyInfo prop in objectType.GetProperties())
                {
                    //try to get the column name attribute of the propery
                    SqlColumnNameAttribute? colAtt = prop.GetCustomAttribute<SqlColumnNameAttribute>(true);
                    //Attribute is valid and coumn name is not empty
                    if (colAtt != null && !string.IsNullOrWhiteSpace(colAtt.ColumnName))
                    {
                        //Store the property for later
                        availbleProps[colAtt.ColumnName] = prop;
                    }
                }
                //Create the new object
                T ret = new();
                //Iterate through columns
                foreach (DbColumn col in columns)
                {
                    //Get the propery if its specified by its column-name attribute
                    if (availbleProps.TryGetValue(col.ColumnName, out PropertyInfo? prop) && col.ColumnOrdinal.HasValue)
                    {
                        //Get the object
                        object val = reader.GetValue(col.ColumnOrdinal.Value);
                        //Set check if the row is DB null, if so set it, otherwise set the value
                        prop.SetValue(ret, Convert.IsDBNull(val) ? null : val);
                    }
                }
                //Return dict, no stress if error occurs, the goal is lower overhead
                DictStore.Return(availbleProps);
                //Return the new object
                return ret;
            }
            return default;
        }
        /// <summary>
        /// Executes a nonquery operation with the specified command using the object properties set with the 
        /// <see cref="SqlVariableAttribute"/> attributes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cmd"></param>
        /// <param name="obj">The object containing the <see cref="SqlVariableAttribute"/> properties to write to command variables</param>
        /// <returns>The number of rows affected</returns>
        /// <exception cref="TypeLoadException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="AmbiguousMatchException"></exception>
        /// <exception cref="TargetInvocationException"></exception>
        public static ERRNO ExecuteNonQuery<T>(this DbCommand cmd, T obj) where T : notnull
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }
            //Get the objec type 
            Type objtype = typeof(T);
            //Itterate through public properties
            foreach (PropertyInfo prop in objtype.GetProperties())
            {
                //try to get the variable attribute of the propery
                SqlVariableAttribute varprops = prop.GetCustomAttribute<SqlVariableAttribute>(true);
                //This property is an sql variable, so lets add it
                if (varprops == null)
                {
                    continue;
                }
                //If the command type is text, then make sure the variable is actually in the command, if not, ignore it
                if (cmd.CommandType != CommandType.Text || cmd.CommandText.Contains(varprops.VariableName))
                {
                    //Add the parameter to the command list
                    cmd.AddParameter(varprops.VariableName, prop.GetValue(obj), varprops.DataType, varprops.Size, varprops.IsNullable).Direction = varprops.Direction;
                }
            }
            //Prepare the sql statement
            cmd.Prepare();
            //Exect the query and return the results
            return cmd.ExecuteNonQuery();
        }
        /// <summary>
        /// Executes a nonquery operation with the specified command using the object properties set with the 
        /// <see cref="SqlVariable"/> attributes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cmd"></param>
        /// <param name="obj">The object containing the <see cref="SqlVariable"/> properties to write to command variables</param>
        /// <returns>The number of rows affected</returns>
        /// <exception cref="TypeLoadException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="AmbiguousMatchException"></exception>
        /// <exception cref="TargetInvocationException"></exception>
        public static async Task<ERRNO> ExecuteNonQueryAsync<T>(this DbCommand cmd, T obj) where T : notnull
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }
            //Get the objec type 
            Type objtype = typeof(T);
            //Itterate through public properties
            foreach (PropertyInfo prop in objtype.GetProperties())
            {
                //try to get the variable attribute of the propery
                SqlVariableAttribute? varprops = prop.GetCustomAttribute<SqlVariableAttribute>(true);
                //This property is an sql variable, so lets add it
                if (varprops == null)
                {
                    continue;
                }
                //If the command type is text, then make sure the variable is actually in the command, if not, ignore it
                if (cmd.CommandType != CommandType.Text || cmd.CommandText.Contains(varprops.VariableName))
                {
                    //Add the parameter to the command list
                    cmd.AddParameter(varprops.VariableName, prop.GetValue(obj), varprops.DataType, varprops.Size, varprops.IsNullable).Direction = varprops.Direction;
                }
            }
            //Prepare the sql statement
            await cmd.PrepareAsync();
            //Exect the query and return the results
            return await cmd.ExecuteNonQueryAsync();
        }
    }
}