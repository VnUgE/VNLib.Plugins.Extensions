/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: PluginSecretLoading.cs 
*
* PluginSecretLoading.cs is part of VNLib.Plugins.Extensions.Loading which 
* is part of the larger VNLib collection of libraries and utilities.
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
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Hashing.IdentityUtility;

namespace VNLib.Plugins.Extensions.Loading
{

    /// <summary>
    /// Adds loading extensions for secure/centralized configuration secrets
    /// </summary>
    public static class PluginSecretLoading
    {
        /// <summary>
        /// Gets a wrapper for the secret store for the current plugin
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns>The secret store structure</returns>
        public static PluginSecretStore Secrets(this PluginBase plugin) => new(plugin);

        /// <summary>
        /// <para>
        /// Gets a secret from the "secrets" element. 
        /// </para>
        /// <para>
        /// Secrets elements are merged from the host config and plugin local config 'secrets' element.
        /// before searching. The plugin config takes precedence over the host config.
        /// </para>
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="secretName">The name of the secret property to get</param>
        /// <returns>The element from the configuration file with the given name, raises an exception if the secret does not exist</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        [Obsolete("Use PluginSecretStore.GetSecretAsync instead")]
        public static async Task<ISecretResult> GetSecretAsync(this PluginBase plugin, string secretName)
        {
            ISecretResult? res = await TryGetSecretAsync(plugin, secretName).ConfigureAwait(false);
            return res ?? throw new KeyNotFoundException($"Missing required secret {secretName}");
        }

        /// <summary>
        /// <para>
        /// Gets a secret from the "secrets" element. 
        /// </para>
        /// <para>
        /// Secrets elements are merged from the host config and plugin local config 'secrets' element.
        /// before searching. The plugin config takes precedence over the host config.
        /// </para>
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="secretName">The name of the secret propery to get</param>
        /// <returns>The element from the configuration file with the given name, or null if the configuration or property does not exist</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        [Obsolete("Use PluginSecretStore.TryGetSecretAsync instead")]
        public static Task<ISecretResult?> TryGetSecretAsync(this PluginBase plugin, string secretName)
        {
            return plugin
                .Secrets()
                .TryGetAsync(secretName);
        }
      

        /// <summary>
        /// Gets the Secret value as a byte buffer
        /// </summary>
        /// <param name="secret"></param>
        /// <returns>The base64 decoded secret as a byte[]</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InternalBufferTooSmallException"></exception>
        public static byte[] GetFromBase64(this ISecretResult secret)
        {
            ArgumentNullException.ThrowIfNull(secret);
            
            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc(secret.Result.Length);
            
            //Get base64
            if(!Convert.TryFromBase64Chars(secret.Result, buffer.Span, out int count))
            {
                throw new InternalBufferTooSmallException("internal buffer too small");
            }

            //Copy to array
            byte[] value = buffer.Span[..count].ToArray();

            //Clear block before returning
            MemoryUtil.InitializeBlock(buffer.Span);

            return value;
        }

        /// <summary>
        /// Recovers a certificate from a PEM encoded secret
        /// </summary>
        /// <param name="secret"></param>
        /// <returns>The <see cref="X509Certificate2"/> parsed from the PEM encoded data</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static X509Certificate2 GetCertificate(this ISecretResult secret)
        {
            ArgumentNullException.ThrowIfNull(secret, nameof(secret));
            return X509Certificate2.CreateFromPem(secret.Result);
        }

        /// <summary>
        /// Gets the secret value as a secret result
        /// </summary>
        /// <param name="secret"></param>
        /// <returns>The document parsed from the secret value</returns>
        public static JsonDocument GetJsonDocument(this ISecretResult secret)
        {
            ArgumentNullException.ThrowIfNull(secret, nameof(secret));

            //Alloc buffer, utf8 so 1 byte per char
            using IMemoryHandle<byte> buffer = MemoryUtil.SafeAlloc<byte>(secret.Result.Length);

            //Get utf8 bytes
            int count = Encoding.UTF8.GetBytes(secret.Result, buffer.Span);
            
            //Reader and parse
            Utf8JsonReader reader = new(buffer.Span[..count]);
            
            return JsonDocument.ParseValue(ref reader);
        }

        /// <summary>
        /// Gets a SPKI encoded public key from a secret
        /// </summary>
        /// <param name="secret"></param>
        /// <returns>The <see cref="PublicKey"/> parsed from the SPKI public key</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static PublicKey GetPublicKey(this ISecretResult secret)
        {          
            ArgumentNullException.ThrowIfNull(secret, nameof(secret));
            
            //Alloc buffer, base64 is larger than binary value so char len is large enough
            using IMemoryHandle<byte> buffer = MemoryUtil.SafeAlloc<byte>(secret.Result.Length);
            
            //Get base64 bytes
            ERRNO count = VnEncoding.TryFromBase64Chars(secret.Result, buffer.Span);
            
            //Parse the SPKI from base64
            return PublicKey.CreateFromSubjectPublicKeyInfo(buffer.Span[..(int)count], out _);
        }

        /// <summary>
        /// Gets a <see cref="ReadOnlyJsonWebKey"/> from a secret value
        /// </summary>
        /// <param name="secret"></param>
        /// <returns>The <see cref="ReadOnlyJsonWebKey"/> from the result</returns>
        /// <exception cref="JsonException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static ReadOnlyJsonWebKey GetJsonWebKey(this ISecretResult secret)
        {
            ArgumentNullException.ThrowIfNull(secret);

            //Alloc buffer, utf8 so 1 byte per char
            using IMemoryHandle<byte> buffer = MemoryUtil.SafeAlloc<byte>(secret.Result.Length);
            
            //Get utf8 bytes
            int count = Encoding.UTF8.GetBytes(secret.Result, buffer.Span);

            return ReadOnlyJsonWebKey.FromUtf8Bytes(buffer.Span[..count]);
        }

#nullable disable

        /// <summary>
        /// Converts the secret recovery task to return the base64 decoded secret as a byte[]
        /// </summary>
        /// <param name="secret"></param>
        /// <returns>A task whos result the base64 decoded secret as a byte[]</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InternalBufferTooSmallException"></exception>
        public static async Task<byte[]> ToBase64Bytes(this Task<ISecretResult> secret)
        {
            ArgumentNullException.ThrowIfNull(secret);

            using ISecretResult sec = await secret.ConfigureAwait(false);

            return sec?.GetFromBase64();
        }

        /// <summary>
        /// Gets a task that resolves a <see cref="ReadOnlyJsonWebKey"/>
        /// from a <see cref="SecretResult"/> task
        /// </summary>
        /// <param name="secret"></param>
        /// <returns>The <see cref="ReadOnlyJsonWebKey"/> from the secret, or null if the secret was not found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static async Task<ReadOnlyJsonWebKey> ToJsonWebKey(this Task<ISecretResult> secret) 
        {
            ArgumentNullException.ThrowIfNull(secret);

            using ISecretResult sec = await secret.ConfigureAwait(false);

            return sec?.GetJsonWebKey();
        }

        /// <summary>
        /// Gets a task that resolves a <see cref="ReadOnlyJsonWebKey"/>
        /// from a <see cref="SecretResult"/> task
        /// </summary>
        /// <param name="secret"></param>
        /// <param name="required">
        /// A value that inidcates that a value is required from the result, 
        /// or a <see cref="KeyNotFoundException"/> is raised
        /// </param>
        /// <returns>The <see cref="ReadOnlyJsonWebKey"/> from the secret, or throws <see cref="KeyNotFoundException"/> if the key was not found</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="KeyNotFoundException"></exception>
        public static async Task<ReadOnlyJsonWebKey> ToJsonWebKey(this Task<ISecretResult> secret, bool required)
        {
            ArgumentNullException.ThrowIfNull(secret);
            
            using ISecretResult sec = await secret.ConfigureAwait(false);
            
            //If required is true and result is null, raise an exception
            return required && sec == null 
                ? throw new KeyNotFoundException("A required secret was missing") 
                : (sec?.GetJsonWebKey()!);
        }

        /// <summary>
        /// Converts a <see cref="SecretResult"/> async operation to a lazy result that can be awaited, that transforms the result
        /// to your desired type. If the result is null, the default value of <typeparamref name="TResult"/> is returned
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="result"></param>
        /// <param name="transformer">Your function to transform the secret to its output form</param>
        /// <returns>A <see cref="IAsyncLazy{T}"/> </returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static IAsyncLazy<TResult> ToLazy<TResult>(this Task<ISecretResult> result, Func<ISecretResult, TResult> transformer)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(transformer);

            //standard secret transformer
            static async Task<TResult> Run(Task<ISecretResult> tr, Func<ISecretResult, TResult> transformer)
            {
                using ISecretResult res = await tr.ConfigureAwait(false);
                return res == null ? default : transformer(res); 
            }

            return Run(result, transformer).AsLazy();
        }

        /// <summary>
        /// Converts a <see cref="SecretResult"/> async operation to a lazy result that can be awaited, that transforms the result
        /// to your desired type. If the result is null, the default value of <typeparamref name="TResult"/> is returned
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="result"></param>
        /// <param name="transformer">Your function to transform the secret to its output form</param>
        /// <returns>A <see cref="IAsyncLazy{T}"/> </returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static IAsyncLazy<TResult> ToLazy<TResult>(this Task<ISecretResult> result, Func<ISecretResult, Task<TResult>> transformer)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(transformer);

            // Suppress nullable reference warning for the default(TResult) return in the lambda
            // The lambda correctly handles null case by returning default when ISecretResult is null
#pragma warning disable CS8632 // Nullable annotation used in non-nullable context
            static async Task<TResult> Run(Task<ISecretResult?> tr, Func<ISecretResult, Task<TResult>> transformer)
            {
                using ISecretResult res = await tr.ConfigureAwait(false);
                return res == null ? default : await transformer(res).ConfigureAwait(false);
            }
#pragma warning restore CS8632

            return Run(result, transformer).AsLazy();
        }

#nullable enable
      
    }
}
