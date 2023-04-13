/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading.Sql
* File: DbBuilder.cs 
*
* DbBuilder.cs is part of VNLib.Plugins.Extensions.Loading.Sql which 
* is part of the larger VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading.Sql is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading.Sql is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using VNLib.Plugins.Extensions.Loading.Sql.DatabaseBuilder;

namespace VNLib.Plugins.Extensions.Loading.Sql
{
    internal sealed class DbBuilder : IDbContextBuilder
    {
        private readonly LinkedList<IDbTable> _tables = new();
        ///<inheritdoc/>
        public IDbTableBuilder<T> DefineTable<T>()
        {
            //Use the table attribute to specify the table name
            TableAttribute? tnA = typeof(T).GetCustomAttribute<TableAttribute>();

            return DefineTable<T>(tnA?.Name);
        }

        ///<inheritdoc/>
        public IDbTableBuilder<T> DefineTable<T>(string? tableName)
        {
            Type rtType = typeof(T);

            //Table name is the defined name, or the type name
            DataTable table = new(tableName ?? rtType.Name);

            //Create table with name
            TableBuilder<T> builder = new(table, rtType);

            //Store the new table builder
            _tables.AddLast(builder);

            return builder;
        }

        internal string[] BuildCreateCommand(IDBCommandGenerator cmdBuilder)
        {
            List<string> tableCommands = new();

            foreach (IDbTable table in _tables)
            {
                //Setup a new string builder for this table command
                StringBuilder sb = new();

                table.WriteCommand(sb, cmdBuilder);

                //build the command string and add to the list
                string cmd = sb.ToString();
                tableCommands.Add(cmd);
            }

            return tableCommands.ToArray();
        }

        private record class TableBuilder<T>(DataTable Table, Type RuntimeType) : IDbTable, IDbTableBuilder<T>
        {
            ///<inheritdoc/>
            public IDbColumnBuilder<T> WithColumn<TCol>(Expression<Func<T, TCol>> selector)
            {
                KeyValuePair<string, Type> selectorData;

                //recover the expression information to determine the selected property
                if (selector.Body is MemberExpression me)
                {
                    selectorData = new(me.Member.Name, (me.Member as PropertyInfo)!.PropertyType);
                }
                else if(selector.Body is UnaryExpression ue)
                {
                    //We need to get the property name from the operand
                    string name = ((MemberExpression)ue.Operand).Member.Name;

                    //We want to get the operand type if the user wants to cast the type, we want to capture the casted type
                    selectorData = new(name, ue.Type);
                }
                else
                {
                    throw new ArgumentException("The selector expression type is not supported", nameof(selector));
                }

                //try to see if an altername column name is defined on the type
                string? colNameAttr = GetPropertyColumnName(selectorData.Key);

                /*
                 * Create the new column with the name of the column attribute, or fallback to the propearty name
                 * 
                 * NOTE: I am recovering the column type from the expression type, not the model type. This allows
                 * the user to alter the type without having to alter the entity to 'fool' database type conversion
                 */
                DataColumn col = new(colNameAttr ?? selectorData.Key, selectorData.Value);

                //Check for maxLen property
                int? maxLen = GetPropertyMaxLen(col.ColumnName);

                if (maxLen.HasValue)
                {
                    col.MaxLength = maxLen.Value;
                }

                //Store the column
                Table.Columns.Add(col);

                //See if key is found, then add the colum to the primary key table
                bool? isKey = GetPropertyIsKey(selectorData.Key);
                if (isKey.HasValue && isKey.Value)
                {
                    col.AddToPrimaryKeys();
                }

                //Set the colum as timestamp
                bool? isRowVersion = GetPropertyIsRowVersion(selectorData.Key);
                if (isRowVersion.HasValue && isRowVersion.Value)
                {
                    col.SetTimeStamp();
                }

                //Init new column builder
                return new ColumnBuilder(col, this);
            }

            ///<inheritdoc/>
            public void WriteCommand(StringBuilder sb, IDBCommandGenerator commandBuilder) => commandBuilder.BuildCreateStatment(sb, Table);


            private int? GetPropertyMaxLen(string propertyName)
            {
                PropertyInfo? property = RuntimeType.GetProperties()
                                                .Where(p => propertyName.Equals(p.Name, StringComparison.OrdinalIgnoreCase))
                                                .FirstOrDefault();

                //Get the max-length attribute
                MaxLengthAttribute? mla = property?.GetCustomAttribute<MaxLengthAttribute>();

                return mla?.Length;
            }
            private string? GetPropertyColumnName(string propertyName)
            {
                PropertyInfo? property = RuntimeType.GetProperties()
                                                .Where(p => propertyName.Equals(p.Name, StringComparison.OrdinalIgnoreCase))
                                                .FirstOrDefault();

                ColumnAttribute? mla = property?.GetCustomAttribute<ColumnAttribute>();

                return mla?.Name;
            }
            private bool? GetPropertyIsKey(string propertyName)
            {
                PropertyInfo? property = RuntimeType.GetProperties()
                                                .Where(p => propertyName.Equals(p.Name, StringComparison.OrdinalIgnoreCase))
                                                .FirstOrDefault();

                //Get the propertie's key attribute
                KeyAttribute? ka = property?.GetCustomAttribute<KeyAttribute>();

                return ka == null ? null : true;
            }

            private bool? GetPropertyIsRowVersion(string propertyName)
            {
                PropertyInfo? property = RuntimeType.GetProperties()
                                                .Where(p => propertyName.Equals(p.Name, StringComparison.OrdinalIgnoreCase))
                                                .FirstOrDefault();

                //Get the properties' timestamp attribute
                TimestampAttribute? ts = property?.GetCustomAttribute<TimestampAttribute>();

                return ts == null ? null : true;
            }

            private record class ColumnBuilder(DataColumn Column, IDbTableBuilder<T> Table) : IDbColumnBuilder<T>
            {
                public IDbTableBuilder<T> Next() => Table;

                public IDbColumnBuilder<T> ConfigureColumn(Action<DataColumn> columnSetter)
                {
                    columnSetter(Column);
                    return this;
                }

                public IDbColumnBuilder<T> AutoIncrement(int seed = 1, int step = 1)
                {
                    Column.AutoIncrement = true;
                    Column.AutoIncrementSeed = seed;
                    Column.AutoIncrementStep = step;
                    return this;
                }
            }
        }
    }
}
