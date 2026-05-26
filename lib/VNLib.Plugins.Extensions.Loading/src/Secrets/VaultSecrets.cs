/*
* Copyright (c) 2026 Vaughn Nugent
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
    /// Adds loading extensions for secure/centralized configuration secrets.
    /// </summary>
    public static class PluginSecretLoading
    {
        /// <summary>
        /// Gets a wrapper for the secret store for the current plugin.
        /// </summary>
        /// <param name="plugin">The plugin instance to get secrets from.</param>
        /// <returns>The secret store for the current plugin.</returns>
        public static PluginSecretStore Secrets(this PluginBase plugin) => new(plugin);

        /// <summary>
        /// Gets a secret from the "secrets" element.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Secrets elements are merged from the host config and plugin local config 'secrets' element
        /// before searching. The plugin config takes precedence over the host config.
        /// </para>
        /// </remarks>
        /// <param name="plugin">The plugin instance to get secrets from.</param>
        /// <param name="secretName">The name of the secret property to get.</param>
        /// <returns>The secret result with the given name.</returns>
        /// <exception cref="KeyNotFoundException">The specified secret was not found.</exception>
        /// <exception cref="ObjectDisposedException">The plugin has been disposed.</exception>
        [Obsolete("Use PluginSecretStore.GetSecretAsync instead")]
        public static async Task<ISecretResult> GetSecretAsync(this PluginBase plugin, string secretName)
        {
            ISecretResult? res = await TryGetSecretAsync(plugin, secretName).ConfigureAwait(false);
            return res ?? throw new KeyNotFoundException($"Missing required secret {secretName}");
        }

        /// <summary>
        /// Gets a secret from the "secrets" element.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Secrets elements are merged from the host config and plugin local config 'secrets' element
        /// before searching. The plugin config takes precedence over the host config.
        /// </para>
        /// </remarks>
        /// <param name="plugin">The plugin instance to get secrets from.</param>
        /// <param name="secretName">The name of the secret property to get.</param>
        /// <returns>The secret result with the given name, or <see langword="null" /> if the configuration or property does not exist.</returns>
        /// <exception cref="KeyNotFoundException">The specified secret was not found.</exception>
        /// <exception cref="ObjectDisposedException">The plugin has been disposed.</exception>
        [Obsolete("Use PluginSecretStore.TryGetSecretAsync instead")]
        public static Task<ISecretResult?> TryGetSecretAsync(this PluginBase plugin, string secretName)
        {
            return plugin
                .Secrets()
                .TryGetAsync(secretName);
        }
      

        /// <summary>
        /// Gets the base64-decoded secret value as a byte array.
        /// </summary>
        /// <param name="secret">The secret result to decode.</param>
        /// <returns>The base64-decoded secret as a byte array.</returns>
        /// <exception cref="ArgumentNullException">The secret value is null.</exception>
        /// <exception cref="InternalBufferTooSmallException">The base64-encoded secret is invalid or the buffer is too small.</exception>
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
        /// Recovers a certificate from a PEM-encoded secret.
        /// </summary>
        /// <param name="secret">The secret result containing the PEM-encoded certificate.</param>
        /// <returns>The <see cref="X509Certificate2"/> parsed from the PEM encoded data.</returns>
        /// <exception cref="ArgumentNullException">The secret is null.</exception>
        public static X509Certificate2 GetCertificate(this ISecretResult secret)
        {
            ArgumentNullException.ThrowIfNull(secret, nameof(secret));
            return X509Certificate2.CreateFromPem(secret.Result);
        }

        /// <summary>
        /// Gets the secret value as a <see cref="JsonDocument"/>.
        /// </summary>
        /// <param name="secret">The secret result to parse.</param>
        /// <returns>A <see cref="JsonDocument"/> parsed from the secret value.</returns>
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
        /// Gets an SPKI-encoded public key from a secret.
        /// </summary>
        /// <param name="secret">The secret result containing the SPKI-encoded public key.</param>
        /// <returns>The <see cref="PublicKey"/> parsed from the SPKI public key.</returns>
        /// <exception cref="ArgumentNullException">The secret is null.</exception>
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
        /// Gets a <see cref="ReadOnlyJsonWebKey"/> from a secret value.
        /// </summary>
        /// <param name="secret">The secret result containing the JSON Web Key data.</param>
        /// <returns>The <see cref="ReadOnlyJsonWebKey"/> from the result.</returns>
        /// <exception cref="JsonException">The secret value is not valid JSON.</exception>
        /// <exception cref="ArgumentException">The secret value is not a valid JSON Web Key.</exception>
        /// <exception cref="ArgumentNullException">The secret is null.</exception>
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
        /// Gets the base64-decoded secret as a byte array from the secret result task.
        /// </summary>
        /// <param name="secret">The task that produces the secret result.</param>
        /// <returns>A task whose result is the base64-decoded secret as a byte array.</returns>
        /// <exception cref="ArgumentNullException">The secret task is null.</exception>
        /// <exception cref="InternalBufferTooSmallException">The base64-encoded secret is invalid or the buffer is too small.</exception>
        public static async Task<byte[]> ToBase64Bytes(this Task<ISecretResult> secret)
        {
            ArgumentNullException.ThrowIfNull(secret);

            using ISecretResult sec = await secret.ConfigureAwait(false);

            return sec?.GetFromBase64();
        }

        /// <summary>
        /// Gets a <see cref="ReadOnlyJsonWebKey"/> from the secret result task.
        /// </summary>
        /// <param name="secret">The task that produces the secret result.</param>
        /// <returns>The <see cref="ReadOnlyJsonWebKey"/> parsed from the secret, or <see langword="null" /> if the secret was not found.</returns>
        /// <exception cref="ArgumentNullException">The secret task is null.</exception>
        public static async Task<ReadOnlyJsonWebKey> ToJsonWebKey(this Task<ISecretResult> secret) 
        {
            ArgumentNullException.ThrowIfNull(secret);

            using ISecretResult sec = await secret.ConfigureAwait(false);

            return sec?.GetJsonWebKey();
        }

        /// <summary>
        /// Gets a <see cref="ReadOnlyJsonWebKey"/> from the secret result task.
        /// </summary>
        /// <param name="secret">The task that produces the secret result.</param>
        /// <param name="required">
        /// A value that indicates whether the key is required; <see langword="true" /> to throw <see cref="KeyNotFoundException"/> if the key is not found; otherwise, <see langword="false" />.
        /// </param>
        /// <returns>The <see cref="ReadOnlyJsonWebKey"/> parsed from the secret, or <see langword="null" /> if the secret was not found.</returns>
        /// <exception cref="ArgumentNullException">The secret task is null.</exception>
        /// <exception cref="KeyNotFoundException">A required secret was not found.</exception>
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
        /// Converts a <see cref="SecretResult"/> async operation to a lazy result that transforms the result to the desired type.
        /// </summary>
        /// <typeparam name="TResult">The type of the transformed result.</typeparam>
        /// <param name="result">The task that produces the secret result.</param>
        /// <param name="transformer">The function to transform the secret result.</param>
        /// <returns>An <see cref="IAsyncLazy{T}"/> that produces the transformed result.</returns>
        /// <exception cref="ArgumentNullException">The result task or transformer is null.</exception>
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
        /// Converts a <see cref="SecretResult"/> async operation to a lazy result that asynchronously transforms the result to the desired type.
        /// </summary>
        /// <typeparam name="TResult">The type of the transformed result.</typeparam>
        /// <param name="result">The task that produces the secret result.</param>
        /// <param name="transformer">The function to asynchronously transform the secret result.</param>
        /// <returns>An <see cref="IAsyncLazy{T}"/> that produces the transformed result.</returns>
        /// <exception cref="ArgumentNullException">The result task or transformer is null.</exception>
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
