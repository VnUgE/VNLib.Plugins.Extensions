/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: BlobExtensions.cs 
*
* BlobExtensions.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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

using VNLib.Utils;

namespace VNLib.Plugins.Extensions.Data.Storage
{
    public static class BlobExtensions
    {
        public const string USER_ID_ENTRY = "__.uid";
        public const string VERSION_ENTRY = "__.vers";

        private const string FILE_SIZE = "__.size";
        private const string FILE_NAME = "__.name";
        private const string ERROR_FLAG = "__.err";

        public static string GetUserId(this Blob blob) => blob[USER_ID_ENTRY];
        /// <summary>
        /// Gets the <see cref="Version"/> stored in the current <see cref="Blob"/>
        /// </summary>
        /// <returns>The sored version if previously set, thows otherwise</returns>
        /// <exception cref="FormatException"></exception>
        public static Version GetVersion(this Blob blob) => Version.Parse(blob[VERSION_ENTRY]);
        /// <summary>
        /// Sets a <see cref="Version"/> for the current <see cref="Blob"/>
        /// </summary>
        /// <param name="blob"></param>
        /// <param name="version">The <see cref="Version"/> of the <see cref="Blob"/></param>
        public static void SetVersion(this Blob blob, Version version) => blob[VERSION_ENTRY] = version.ToString();

        /// <summary>
        /// Gets a value indicating if the last operation left the <see cref="Blob"/> in an undefined state
        /// </summary>
        /// <returns>True if the <see cref="Blob"/> state is undefined, false otherwise</returns>
        public static bool IsError(this Blob blob) => bool.TrueString.Equals(blob[ERROR_FLAG]);
        internal static void IsError(this LWStorageDescriptor blob, bool value) => blob[ERROR_FLAG] = value ? bool.TrueString : null;

        internal static long GetLength(this LWStorageDescriptor blob) => (blob as IObjectStorage).GetObject<long>(FILE_SIZE);
        internal static void SetLength(this LWStorageDescriptor blob, long length) => (blob as IObjectStorage).SetObject(FILE_SIZE, length);

        internal static string GetName(this LWStorageDescriptor blob) => blob[FILE_NAME];
        internal static string SetName(this LWStorageDescriptor blob, string filename) => blob[FILE_NAME] = filename;
    }
}