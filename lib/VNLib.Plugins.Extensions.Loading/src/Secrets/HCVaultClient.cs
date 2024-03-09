/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: HCVaultClient.cs 
*
* HCVaultClient.cs is part of VNLib.Plugins.Extensions.Loading which 
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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.Security;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

/*
 * The purpose of the HCVaultClient is to provide a very simple Hashicorp Vault client
 * interface that reads KV secrets from a vault server with minimal dependencies.
 * 
 * Since I only need the KV store for now, I don't think there is a need for the 
 * VaultSharp package which adds at least 600kb to the final package size.
 */

namespace VNLib.Plugins.Extensions.Loading
{
    internal sealed class HCVaultClient : VnDisposeable, IHCVaultClient
    {
        const string VaultTokenHeaderName = "X-Vault-Token";
        const long MaxErrResponseContentLength = 8192;
        const uint DefaultBufferSize = 4096;

        private static readonly TimeSpan ClientDefaultTimeout = TimeSpan.FromSeconds(30);

        private readonly HttpClient _client;
        private readonly int _kvVersion;
        private readonly IUnmangedHeap _bufferHeap;

        HCVaultClient(string serverAddress, string hcToken, int kvVersion, bool trustCert, IUnmangedHeap heap)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            HttpClientHandler handler = new()
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                MaxResponseHeadersLength = 2048,
                ClientCertificateOptions = ClientCertificateOption.Automatic,
                AutomaticDecompression = DecompressionMethods.All,
                PreAuthenticate = false,

                //Setup a callback to trust the server certificate if the cert chain is invalid
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => trustCert || errors == SslPolicyErrors.None
            };

#pragma warning restore CA2000 // Dispose objects before losing scope

            _client = new HttpClient(handler, true)
            {
                BaseAddress = new Uri(serverAddress),
                Timeout = ClientDefaultTimeout,
                DefaultRequestVersion = new Version(1, 1),
                MaxResponseContentBufferSize = 4096     //Buffer only needs to be little for vault requests 
            };

           
            //Set the vault access token header, should probably clean this up later
            _client.DefaultRequestHeaders.Add(VaultTokenHeaderName, hcToken);
            _kvVersion = kvVersion;
            _bufferHeap = heap;
        }

        /// <summary>
        /// Creates a new Hashicorp vault client with the given server address, token, and KV storage version
        /// </summary>
        /// <param name="serverAddress">The vault server address</param>
        /// <param name="hcToken">The vault token used to connect to the vault server</param>
        /// <param name="kvVersion">The hc vault Key value store version (must be 1 or 2)</param>
        /// <param name="trustCert">A value that tells the HTTP client to trust the Vault server's certificate even if it's not valid</param>
        /// <param name="heap">Heap instance to allocate internal buffers from</param>
        /// <returns>The new client instance</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static HCVaultClient Create(string serverAddress, string hcToken, int kvVersion, bool trustCert, IUnmangedHeap heap)
        {
            ArgumentException.ThrowIfNullOrEmpty(serverAddress);
            ArgumentException.ThrowIfNullOrEmpty(hcToken);
            ArgumentNullException.ThrowIfNull(heap);

            if(kvVersion != 1 && kvVersion != 2)
            {
                throw new ArgumentException($"Unsupported vault KV storage version {kvVersion}, must be either 1 or 2");
            }

            return new HCVaultClient(serverAddress, hcToken, kvVersion, trustCert, heap);
        }

        ///<inheritdoc/>
        protected override void Free()
        {
            _client.Dispose();
        }

        ///<inheritdoc/>
        public async Task<ISecretResult?> ReadSecretAsync(string path, string mountPoint, string secretName)
        {
            string secretPath = GetSecretPathForKvVersion(_kvVersion, path, mountPoint);
            using HttpRequestMessage ms = GetRequestMessageForPath(secretPath);

            try
            {
                using HttpResponseMessage response = await _client.SendAsync(ms, HttpCompletionOption.ResponseHeadersRead);

                //Check if an error occured in the response 
                await ProcessVaultErrorResponseAsync(response, true);

                //Read the response async
                using SecretResponse res = await ReadSecretResponse(response.Content, true);

                return FromResponse(res, secretName);
            }
            catch(HttpRequestException he) when(he.InnerException is SocketException se)
            {
                throw se.SocketErrorCode switch
                {
                    SocketError.HostNotFound => new HCVaultException("Failed to connect to Hashicorp Vault server, because it's DNS hostname could not be resolved"),
                    SocketError.ConnectionRefused => new HCVaultException("Failed to establish a TCP connection to the vault server, the server refused the connection"),
                    _ => new HCVaultException("Failed to establish a TCP connection to the vault server, see inner exception", se),
                };
            }
            catch(Exception ex)
            {
                throw new HCVaultException("Failed to retreive secret from Hashicorp Vault server, see inner exception", ex);
            }
        }

        ///<inheritdoc/>
        public ISecretResult? ReadSecret(string path, string mountPoint, string secretName)
        {
            string secretPath = GetSecretPathForKvVersion(_kvVersion, path, mountPoint);
            using HttpRequestMessage ms = GetRequestMessageForPath(secretPath);

            try
            {
                //Exec the response synchronously
                using HttpResponseMessage response = _client.Send(ms, HttpCompletionOption.ResponseHeadersRead);

                /*
                 * It is safe to await the error result here because its 
                 * already completed when the async flag is false
                 */
                ValueTask errTask = ProcessVaultErrorResponseAsync(response, false);
                Debug.Assert(errTask.IsCompleted);
                errTask.GetAwaiter().GetResult();

                //Did not throw, handle a secret response

                ValueTask<SecretResponse> resTask = ReadSecretResponse(response.Content, false);
                Debug.Assert(resTask.IsCompleted);

                //Always wrap response in using to clean memory
                using SecretResponse res = resTask.GetAwaiter().GetResult();

                return FromResponse(res, secretName);
            }
            catch (HttpRequestException he) when (he.InnerException is SocketException se)
            {
                throw se.SocketErrorCode switch
                {
                    SocketError.HostNotFound => new HCVaultException("Failed to connect to Hashicorp Vault server, because it's DNS hostname could not be resolved"),
                    SocketError.ConnectionRefused => new HCVaultException("Failed to establish a TCP connection to the vault server, the server refused the connection"),
                    _ => new HCVaultException("Failed to establish a TCP connection to the vault server, see inner exception", se),
                };
            }
            catch (Exception ex)
            {
                throw new HCVaultException("Failed to retreive secret from Hashicorp Vault server, see inner exception", ex);
            }
        }

        private ValueTask<SecretResponse> ReadSecretResponse(HttpContent content, bool async)
        {
            SecretResponse res = new(DefaultBufferSize, _bufferHeap);
            try
            {
                if (async)
                {
                    return ReadStreamAsync(content, res);
                }
                else
                {
                    //Read into a memory stream
                    content.CopyTo(res.StreamData, null, default);
                    res.ResetStream();

                    return ValueTask.FromResult(res);
                }
            }
            catch
            {
                res.Dispose();
                throw;
            }

            async static ValueTask<SecretResponse> ReadStreamAsync(HttpContent content, SecretResponse response)
            {
                try
                {
                    await content.CopyToAsync(response.StreamData);
                    
                    response.ResetStream();

                    return response;
                }
                catch
                {
                    response.Dispose();
                    throw;
                }
            }

        }

        private static string GetSecretPathForKvVersion(int version, string path, string mount)
        {
            return version switch
            {
                1 => $"v1/{mount}/{path}",
                2 => $"v1/{mount}/data/{path}",
                _ => throw new InvalidOperationException("Invalid KV version")
            };
        }

        private static HttpRequestMessage GetRequestMessageForPath(string secretPath)
        {
            return new(HttpMethod.Get, secretPath)
            {
                VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
            };
        }

        private static SecretResult? FromResponse(SecretResponse res, string secretName)
        {
            using JsonDocument json = res.AsJson();

            if (!json.RootElement.TryGetProperty("data", out JsonElement dataEl))
            {
                throw new HttpRequestException("Vault KV response did not include a top-level 'data' element");
            }

            if (!dataEl.TryGetProperty("data", out dataEl))
            {
                throw new HttpRequestException("Vault KV response did not include a 'data' element");
            }

            //Try to get the secret from the data element
            if (dataEl.TryGetProperty(secretName, out JsonElement secretEl))
            {
                string? secValue = secretEl.GetString();
                return secValue == null ? null : SecretResult.ToSecret(secValue);
            }

            return null;
        }

        private static ValueTask ProcessVaultErrorResponseAsync(HttpResponseMessage response, bool async)
        {
            if (response.IsSuccessStatusCode)
            {
                return default;
            }

            //Make sure the response has content
            long? ctLen = response.Content.Headers.ContentLength;
            if(!ctLen.HasValue || ctLen.Value == 0)
            {
                return ValueTask.FromException(
                    new HttpRequestException($"Failed to fetch secret from vault with error code {response.StatusCode}")
                );
            }

            //Check for way too big response entity body
            if(ctLen.Value > MaxErrResponseContentLength)
            {
                return ValueTask.FromException(
                    new HttpRequestException(
                        $"Vault error {response.StatusCode}. Response content length was too large, expected less than {MaxErrResponseContentLength} but got {ctLen.Value}"
                ));
            }


            //Assert json response body
            if (!string.Equals("application/json", response.Content.Headers.ContentType?.MediaType, StringComparison.OrdinalIgnoreCase))
            {
                return ValueTask.FromException(
                    new HttpRequestException("Vault response was not in JSON format")
                );
            }

            return async ? ExceptionsFromContentAsync(response) : ExceptionsFromContent(response);
           
            static ValueTask ExceptionFromVaultErrors(HttpStatusCode code, VaultErrorMessage? errs)
            {
                //If the error message is null, raise an exception
                if (errs == null || errs.Errors == null || errs.Errors.Length == 0)
                {
                    return ValueTask.FromException(
                        new HttpRequestException($"Failed to fetch secret from vault with error code {code}")
                    );
                }

                //Join the errors into a single string with newlines
                IEnumerable<string> errors = errs.Errors.Select(err => $"Vault Error -> {err}");
                string errStr = string.Join(Environment.NewLine, errors);

                //Finally raise the exception with all the returned errors
                return ValueTask.FromException(
                    new HttpRequestException($"Failed to fetch secre from vault with {code}, errors:\n {errStr}")
                );
            }

            static async ValueTask ExceptionsFromContentAsync(HttpResponseMessage response)
            {
                //Read stream async and deserialize async
                using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                VaultErrorMessage? errs = await JsonSerializer.DeserializeAsync<VaultErrorMessage>(stream);

                await ExceptionFromVaultErrors(response.StatusCode, errs);
            }

            static ValueTask ExceptionsFromContent(HttpResponseMessage response)
            {
#pragma warning disable CA1849 // Call async methods when in an async method

                //Read the error content stream and deserialize
                using Stream stream = response.Content.ReadAsStream();
                VaultErrorMessage? errs = JsonSerializer.Deserialize<VaultErrorMessage>(stream);

#pragma warning restore CA1849 // Call async methods when in an async method

                return ExceptionFromVaultErrors(response.StatusCode, errs);
            }
        }


        private sealed class SecretResponse : VnDisposeable
        {
            /*
             * Purpose of this class is to hold a memory stream that can read 
             * the vault response into memory, use it for some operation,
             * then zero the memory before releasing it back to the heap
             */

            private readonly MemoryHandle<byte> _memHandle;

            public VnMemoryStream StreamData { get; }

            public SecretResponse(uint initSize, IUnmangedHeap heap)
            {
                _memHandle = heap.Alloc<byte>(initSize, false);
                StreamData = VnMemoryStream.FromHandle(_memHandle, false, 0, false);
            }

            /// <summary>
            /// Gets a <see cref="JsonDocument"/> from the response data
            /// </summary>
            /// <returns></returns>
            public JsonDocument AsJson()
            {
                //read the data as a raw span then parse it as json
                Utf8JsonReader reader = new(StreamData.AsSpan());
                return JsonDocument.ParseValue(ref reader);
            }

            /// <summary>
            /// Resets the stream to the beginning
            /// </summary>
            public void ResetStream() => StreamData.Seek(0, SeekOrigin.Begin);

            protected override void Free()
            {
                //zero the handle before disposing
                MemoryUtil.InitializeBlock(ref _memHandle.GetReference(), _memHandle.GetIntLength());
                _memHandle.Dispose();
            }
        }

        private sealed class VaultErrorMessage
        {
            [JsonPropertyName("errors")]
            public string[]? Errors { get; set; }
        }
    }
}
