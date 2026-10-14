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
using VNLib.Utils.Async;
using VNLib.Utils.Memory;
using VNLib.Hashing.IdentityUtility;
using VNLib.Utils.Extensions;

namespace VNLib.Plugins.Extensions.Loading.Secrets
{

    /// <summary>
    /// Adds loading extensions for secure/centralized configuration secrets
    /// </summary>
    public static class PluginSecretLoading
    {
        /// <summary>
        /// Gets a wrapper for the secret store for the current plugin
        /// </summary>
        /// <param name="plugin">The plugin to resolve secrets for.</param>
        /// <returns>The secret store structure</returns>
        public static PluginSecretStore Secrets(this PluginBase plugin) => new(plugin);

        /// <summary>
        /// <para>
        /// Gets a secret from the "secrets" element. 
        /// </para>
        /// <para>
        /// Secrets elements are merged from the host config and plugin local config 'secrets' element
        /// before searching. The plugin config takes precedence over the host config.
        /// </para>
        /// </summary>
        /// <param name="plugin">The plugin to resolve secrets for.</param>
        /// <param name="secretName">The name of the secret property to get</param>
        /// <returns>The element from the configuration file with the given name, raises an exception if the secret does not exist</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the secret is not found in the plugin configuration.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the plugin has been disposed.</exception>
        [Obsolete("Use plugin.Secrets().GetAsync() instead")]
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
        /// Secrets elements are merged from the host config and plugin local config 'secrets' element
        /// before searching. The plugin config takes precedence over the host config.
        /// </para>
        /// </summary>
        /// <param name="plugin">The plugin to resolve secrets for.</param>
        /// <param name="secretName">The name of the secret property to get</param>
        /// <returns>The element from the configuration file with the given name, or null if the configuration or property does not exist</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the plugin has been disposed.</exception>
        [Obsolete("Use plugin.Secrets().TryGetAsync() instead")]
        public static Task<ISecretResult?> TryGetSecretAsync(this PluginBase plugin, string secretName)
        {
            return plugin
                .Secrets()
                .TryGetAsync(secretName);
        }
      

        /// <summary>
        /// Gets the Secret value as a byte buffer
        /// </summary>
        /// <param name="secret">The secret result containing the base64 encoded value.</param>
        /// <returns>The base64 decoded secret as a byte[]</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="secret"/> is <see langword="null"/>.</exception>
        /// <exception cref="FormatException">Thrown when the secret value is not valid Base64 data and cannot be decoded.</exception>
        public static byte[] GetFromBase64(this ISecretResult secret)
        {
            ArgumentNullException.ThrowIfNull(secret);
            
            // Base64 will always be larger than the encoded output, so overalloc is fine.
            using UnsafeMemoryHandle<byte> buffer = MemoryUtil.UnsafeAlloc(secret.Result.Length);
            
            // Get base64
            if(!Convert.TryFromBase64Chars(secret.Result, buffer.Span, out int count))
            {
                throw new FormatException("Failed to decode base64 bytes from stored secret.");
            }

            // Copy to array
            byte[] value = buffer.Span[..count].ToArray();

            // Clear block before returning
            MemoryUtil.InitializeBlock(
                ref buffer.GetReference(), 
                buffer.IntLength
            );

            return value;
        }

        /// <summary>
        /// Recovers a certificate from a PEM encoded secret
        /// </summary>
        /// <param name="secret">The secret result containing the PEM encoded certificate.</param>
        /// <returns>The <see cref="X509Certificate2"/> parsed from the PEM encoded data</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="secret"/> is <see langword="null"/>.</exception>
        public static X509Certificate2 GetCertificate(this ISecretResult secret)
        {
            ArgumentNullException.ThrowIfNull(secret, nameof(secret));
            return X509Certificate2.CreateFromPem(secret.Result);
        }

        /// <summary>
        /// Gets the secret value as a <see cref="JsonDocument"/>
        /// </summary>
        /// <param name="secret">The secret result containing the JSON value.</param>
        /// <returns>The document parsed from the secret value</returns>
        public static JsonDocument GetJsonDocument(this ISecretResult secret)
        {
            ArgumentNullException.ThrowIfNull(secret, nameof(secret));

            //Alloc buffer, utf8 so 1 byte per char
            using IMemoryHandle<byte> buffer = MemoryUtil.SafeAlloc<byte>(secret.Result.Length);
            
            try
            {
                //Get utf8 bytes
                int count = Encoding.UTF8.GetBytes(secret.Result, buffer.Span);

                //Reader and parse
                Utf8JsonReader reader = new(buffer.Span[..count]);

                return JsonDocument.ParseValue(ref reader);
            }
            finally
            {
                //Clear buffer before returning
                MemoryUtil.InitializeBlock(
                    ref buffer.GetReference(), 
                    buffer.GetIntLength()
                );
            }
        }

        /// <summary>
        /// Gets a SPKI encoded public key from a secret
        /// </summary>
        /// <param name="secret">The secret result containing the SPKI encoded public key.</param>
        /// <returns>The <see cref="PublicKey"/> parsed from the SPKI public key</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="secret"/> is <see langword="null"/>.</exception>
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
        /// Gets a <see cref="ReadOnlyJsonWebKey"/> whose key material is provided in the secret.
        /// </summary>
        /// <param name="secret">The secret result containing the JSON web key.</param>
        /// <returns>The <see cref="ReadOnlyJsonWebKey"/> from the result</returns>
        /// <exception cref="JsonException">Thrown when the secret value is not valid JSON.</exception>
        /// <exception cref="ArgumentException">Thrown when the secret value is not a valid public key format.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="secret"/> is <see langword="null"/>.</exception>
        public static ReadOnlyJsonWebKey GetJsonWebKey(this ISecretResult secret)
        {
            ArgumentNullException.ThrowIfNull(secret);

            //Alloc buffer, utf8 so 1 byte per char
            using IMemoryHandle<byte> buffer = MemoryUtil.SafeAlloc<byte>(secret.Result.Length);

            try
            {
                //Get utf8 bytes
                int count = Encoding.UTF8.GetBytes(secret.Result, buffer.Span);

                return ReadOnlyJsonWebKey.FromUtf8Bytes(buffer.Span[..count]);
            }
            finally
            {
                //Clear buffer before returning
                MemoryUtil.InitializeBlock(
                    ref buffer.GetReference(), 
                    buffer.GetIntLength()
                );
            }
        }

#nullable disable

        /// <summary>
        /// Converts the secret recovery task to return the base64 decoded secret as a byte[]
        /// </summary>
        /// <param name="secret">The secret recovery task to decode.</param>
        /// <returns>A task whose result is the base64 decoded secret as a byte[]</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="secret"/> is <see langword="null"/>.</exception>
        /// <exception cref="FormatException">Thrown when the secret value is not valid Base64 data and cannot be decoded.</exception>
        [Obsolete("Use ToLazy() with transformer")]
        public static Task<byte[]> ToBase64Bytes(this Task<ISecretResult> secret) 
            => secret.ToLazy(static s => s.GetFromBase64()).AsTask();

        /// <summary>
        /// Gets a task that resolves a <see cref="ReadOnlyJsonWebKey"/>
        /// from a <see cref="SecretResult"/> task
        /// </summary>
        /// <param name="secret">The secret recovery task to resolve the key from.</param>
        /// <returns>The <see cref="ReadOnlyJsonWebKey"/> from the secret, or null if the secret was not found</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="secret"/> is <see langword="null"/>.</exception>
        [Obsolete("Use ToLazy() with transformer")]
        public static Task<ReadOnlyJsonWebKey> ToJsonWebKey(this Task<ISecretResult> secret) 
            => secret.ToLazy(static s => s.GetJsonWebKey()).AsTask();

        /// <summary>
        /// Gets a task that resolves a <see cref="ReadOnlyJsonWebKey"/>
        /// from a <see cref="SecretResult"/> task
        /// </summary>
        /// <param name="secret">The secret recovery task to resolve the key from.</param>
        /// <param name="required">
        /// A value that indicates that a value is required from the result, 
        /// or a <see cref="KeyNotFoundException"/> is raised
        /// </param>
        /// <returns>The <see cref="ReadOnlyJsonWebKey"/> from the secret, or throws <see cref="KeyNotFoundException"/> if the key was not found</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="secret"/> is <see langword="null"/>.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the secret is not found in the plugin configuration.</exception>
        [Obsolete("Use ToLazy() with transformer")]
        public static async Task<ReadOnlyJsonWebKey> ToJsonWebKey(this Task<ISecretResult> secret, bool required)
        {
            ReadOnlyJsonWebKey result = await ToJsonWebKey(secret)
                .ConfigureAwait(false);

            return result is null && required 
                ? throw new KeyNotFoundException("A required secret was missing") 
                : result;
        }

        /// <summary>
        /// Converts a <see cref="ISecretResult"/> async operation to a lazy result that can be awaited, that transforms the result
        /// to your desired type. If the result is null, the default value of <typeparamref name="TResult"/> is returned. 
        /// </summary>
        /// <typeparam name="TResult">The type of the transformed secret value.</typeparam>
        /// <param name="result">The secret recovery task to convert to a lazy result.</param>
        /// <param name="transformer">Your function to transform the secret to its output form</param>
        /// <returns>A <see cref="IAsyncLazy{T}"/> </returns>
        /// <remarks>
        /// Takes ownership of the lifetime of the <see cref="ISecretResult"/>, properly disposing the secret value after the 
        /// operation completes (failed or successful).
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="transformer"/> is <see langword="null"/>.</exception>
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

        /// <inheritdoc cref="ToLazy{TResult}(Task{ISecretResult}, Func{ISecretResult, TResult})"/>
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
