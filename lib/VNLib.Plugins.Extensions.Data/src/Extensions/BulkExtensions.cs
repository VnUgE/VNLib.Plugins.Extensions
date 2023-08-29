﻿/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Data
* File: Extensions.cs 
*
* Extensions.cs is part of VNLib.Plugins.Extensions.Data which is part of the larger 
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
using System.Collections.Generic;


namespace VNLib.Plugins.Extensions.Data.Extensions
{
    public static class BulkExtensions
    {

        public static int GetPageOrDefault(this IReadOnlyDictionary<string, string> queryArgs, int @default, int minClamp = 0, int maxClamp = int.MaxValue)
        {
            return queryArgs.TryGetValue("page", out string? pageStr) && int.TryParse(pageStr, out int page) ? Math.Clamp(page, minClamp, maxClamp) : @default;
        }

        public static int GetLimitOrDefault(this IReadOnlyDictionary<string, string> queryArgs, int @default, int minClamp = 0, int maxClamp = int.MaxValue)
        {
            return queryArgs.TryGetValue("limit", out string? limitStr) && int.TryParse(limitStr, out int limit) ? Math.Clamp(limit, minClamp, maxClamp) : @default;
        }
    }
}
