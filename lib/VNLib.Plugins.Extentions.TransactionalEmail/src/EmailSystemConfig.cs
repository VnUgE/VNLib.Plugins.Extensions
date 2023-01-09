/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extentions.TransactionalEmail
* File: EmailSystemConfig.cs 
*
* EmailSystemConfig.cs is part of VNLib.Plugins.Extentions.TransactionalEmail which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extentions.TransactionalEmail is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extentions.TransactionalEmail is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using RestSharp;

using Emails.Transactional.Client;
using VNLib.Net.Rest.Client;


namespace VNLib.Plugins.Extentions.TransactionalEmail
{
    /// <summary>
    /// An extended <see cref="TransactionalEmailConfig"/> configuration 
    /// object that contains a <see cref="Net.Rest.Client.RestClientPool"/> pool for making 
    /// transactions
    /// </summary>
    internal sealed class EmailSystemConfig : TransactionalEmailConfig
    {
        /// <summary>
        /// A shared <see cref="Net.Rest.Client.RestClientPool"/> for renting configuraed 
        /// <see cref="RestClient"/>
        /// </summary>
        public RestClientPool RestClientPool { get; init; }
        /// <summary>
        /// A global from email address name
        /// </summary>
        public string EmailFromName { get; init;  }
        /// <summary>
        /// A global from email address
        /// </summary>
        public string EmailFromAddress { get; init; }
    }
}