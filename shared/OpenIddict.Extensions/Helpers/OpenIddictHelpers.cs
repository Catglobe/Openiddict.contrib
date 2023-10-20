﻿using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;

namespace OpenIddict.Extensions;

/// <summary>
/// Exposes common helpers used by the OpenIddict assemblies.
/// </summary>
internal static class OpenIddictHelpers
{
    /// <summary>
    /// Finds the first base type that matches the specified generic type definition.
    /// </summary>
    /// <param name="type">The type to introspect.</param>
    /// <param name="definition">The generic type definition.</param>
    /// <returns>A <see cref="Type"/> instance if the base type was found, <see langword="null"/> otherwise.</returns>
    public static Type? FindGenericBaseType(Type type, Type definition)
        => FindGenericBaseTypes(type, definition).FirstOrDefault();

    /// <summary>
    /// Finds all the base types that matches the specified generic type definition.
    /// </summary>
    /// <param name="type">The type to introspect.</param>
    /// <param name="definition">The generic type definition.</param>
    /// <returns>A <see cref="Type"/> instance if the base type was found, <see langword="null"/> otherwise.</returns>
    public static IEnumerable<Type> FindGenericBaseTypes(Type type, Type definition)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (definition is null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (!definition.IsGenericTypeDefinition)
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0263), nameof(definition));
        }

        if (definition.IsInterface)
        {
            foreach (var contract in type.GetInterfaces())
            {
                if (!contract.IsGenericType && !contract.IsConstructedGenericType)
                {
                    continue;
                }

                if (contract.GetGenericTypeDefinition() == definition)
                {
                    yield return contract;
                }
            }
        }

        else
        {
            for (var candidate = type; candidate is not null; candidate = candidate.BaseType)
            {
                if (!candidate.IsGenericType && !candidate.IsConstructedGenericType)
                {
                    continue;
                }

                if (candidate.GetGenericTypeDefinition() == definition)
                {
                    yield return candidate;
                }
            }
        }
    }

#if !SUPPORTS_TASK_WAIT_ASYNC
    /// <summary>
    /// Waits until the specified task returns a result or the cancellation token is signaled.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>
    /// A <see cref="Task"/> that can be used to monitor the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">The specified <paramref name="cancellationToken"/> is signaled.</exception>
    public static async Task WaitAsync(this Task task, CancellationToken cancellationToken)
    {
        var source = new TaskCompletionSource<bool>(TaskCreationOptions.None);

        using (cancellationToken.Register(static state => ((TaskCompletionSource<bool>) state!).SetResult(true), source))
        {
            if (await Task.WhenAny(task, source.Task) == source.Task)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            await task;
        }
    }

    /// <summary>
    /// Waits until the specified task returns a result or the cancellation token is signaled.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that can be used to abort the operation.</param>
    /// <returns>
    /// A <see cref="Task"/> that can be used to monitor the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">The specified <paramref name="cancellationToken"/> is signaled.</exception>
    public static async Task<T> WaitAsync<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        var source = new TaskCompletionSource<bool>(TaskCreationOptions.None);

        using (cancellationToken.Register(static state => ((TaskCompletionSource<bool>) state!).SetResult(true), source))
        {
            if (await Task.WhenAny(task, source.Task) == source.Task)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return await task;
        }
    }
#endif

    /// <summary>
    /// Determines whether the specified <paramref name="exception"/> is considered fatal.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>
    /// <see langword="true"/> if the exception is considered fatal, <see langword="false"/> otherwise.
    /// </returns>
    public static bool IsFatal(Exception exception)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();

        return exception switch
        {
            ThreadAbortException => true,
            OutOfMemoryException and not InsufficientMemoryException => true,

            AggregateException { InnerExceptions: var exceptions } => IsAnyFatal(exceptions),
            Exception { InnerException: Exception inner } => IsFatal(inner),

            _ => false
        };

        static bool IsAnyFatal(ReadOnlyCollection<Exception> exceptions)
        {
            for (var index = 0; index < exceptions.Count; index++)
            {
                if (IsFatal(exceptions[index]))
                {
                    return true;
                }
            }

            return false;
        }
    }

#if !SUPPORTS_TOHASHSET_LINQ_EXTENSION
    /// <summary>
    /// Creates a new <see cref="HashSet{T}"/> instance and imports the elements present in the specified source.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements present in the collection.</typeparam>
    /// <param name="source">The source collection.</param>
    /// <param name="comparer">The comparer to use.</param>
    /// <returns>A new <see cref="HashSet{T}"/> instance and imports the elements present in the specified source.</returns>
    /// <exception cref="ArgumentNullException">The <paramref name="source"/> is <see langword="null"/>.</exception>
    public static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source, IEqualityComparer<TSource>? comparer)
        => new(source ?? throw new ArgumentNullException(nameof(source)), comparer);
#endif

    /// <summary>
    /// Computes an absolute URI from the specified <paramref name="left"/> and <paramref name="right"/> URIs.
    /// Note: if the <paramref name="right"/> URI is already absolute, it is directly returned.
    /// </summary>
    /// <param name="left">The left part.</param>
    /// <param name="right">The right part.</param>
    /// <returns>An absolute URI from the specified <paramref name="left"/> and <paramref name="right"/>.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="left"/> is not an absolute URI.</exception>
    [return: NotNullIfNotNull(nameof(right))]
    public static Uri? CreateAbsoluteUri(Uri? left, string? right)
        => CreateAbsoluteUri(left, !string.IsNullOrEmpty(right) ? new Uri(right, UriKind.RelativeOrAbsolute) : null);

    /// <summary>
    /// Computes an absolute URI from the specified <paramref name="left"/> and <paramref name="right"/> URIs.
    /// Note: if the <paramref name="right"/> URI is already absolute, it is directly returned.
    /// </summary>
    /// <param name="left">The left part.</param>
    /// <param name="right">The right part.</param>
    /// <returns>An absolute URI from the specified <paramref name="left"/> and <paramref name="right"/>.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="left"/> is not an absolute URI.</exception>
    [return: NotNullIfNotNull(nameof(right))]
    public static Uri? CreateAbsoluteUri(Uri? left, Uri? right)
    {
        if (right is null)
        {
            return null;
        }

        if (right.IsAbsoluteUri)
        {
            return right;
        }

        if (left is not { IsAbsoluteUri: true })
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0144), nameof(left));
        }

        // Ensure the left part ends with a trailing slash, as it is necessary
        // for Uri's constructor to include the last path segment in the base URI.
        left = left.AbsolutePath switch
        {
            null or { Length: 0 } => new UriBuilder(left) { Path = "/" }.Uri,
            [.., not '/'] => new UriBuilder(left) { Path = left.AbsolutePath + "/" }.Uri,
            ['/'] or _ => left
        };

        return new Uri(left, right);
    }

    /// <summary>
    /// Determines whether the <paramref name="left"/> URI is a base of the <paramref name="right"/> URI.
    /// </summary>
    /// <param name="left">The left part.</param>
    /// <param name="right">The right part.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is base of
    /// <paramref name="right"/>, <see langword="false"/> otherwise.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="left"/> or
    /// <paramref name="right"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="left"/> is not an absolute URI.</exception>
    public static bool IsBaseOf(Uri left, Uri right)
    {
        if (left is null)
        {
            throw new ArgumentNullException(nameof(left));
        }

        if (right is null)
        {
            throw new ArgumentNullException(nameof(right));
        }

        if (left is not { IsAbsoluteUri: true })
        {
            throw new ArgumentException(SR.GetResourceString(SR.ID0144), nameof(left));
        }

        // Ensure the left part ends with a trailing slash, as it is necessary
        // for Uri's constructor to include the last path segment in the base URI.
        left = left.AbsolutePath switch
        {
            null or { Length: 0 } => new UriBuilder(left) { Path = "/" }.Uri,
            [.., not '/'] => new UriBuilder(left) { Path = left.AbsolutePath + "/" }.Uri,
            ['/'] or _ => left
        };

        return left.IsBaseOf(right);
    }

    /// <summary>
    /// Adds a query string parameter to the specified <see cref="Uri"/>.
    /// </summary>
    /// <param name="uri">The URI to which the query string parameter will be appended.</param>
    /// <param name="name">The name of the query string parameter to append.</param>
    /// <param name="value">The value of the query string parameter to append.</param>
    /// <returns>The final <see cref="Uri"/> instance, with the specified parameter appended.</returns>
    public static Uri AddQueryStringParameter(Uri uri, string name, string? value)
    {
        if (uri is null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        var builder = new StringBuilder(uri.Query);
        if (builder.Length > 0)
        {
            builder.Append('&');
        }

        builder.Append(Uri.EscapeDataString(name));

        if (!string.IsNullOrEmpty(value))
        {
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(value));
        }

        return new UriBuilder(uri) { Query = builder.ToString() }.Uri;
    }

    /// <summary>
    /// Adds query string parameters to the specified <see cref="Uri"/>.
    /// </summary>
    /// <param name="uri">The URI to which the query string parameters will be appended.</param>
    /// <param name="parameters">The query string parameters to append.</param>
    /// <returns>The final <see cref="Uri"/> instance, with the specified parameters appended.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <see langword="null"/>.</exception>
    public static Uri AddQueryStringParameters(Uri uri, IReadOnlyDictionary<string, StringValues> parameters)
    {
        if (uri is null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        if (parameters.Count is 0)
        {
            return uri;
        }

        var builder = new StringBuilder(uri.Query);

        foreach (var parameter in parameters)
        {
            // If the parameter doesn't include any string value,
            // only append the parameter key to the query string.
            if (parameter.Value.Count is 0)
            {
                if (builder.Length > 0)
                {
                    builder.Append('&');
                }

                builder.Append(Uri.EscapeDataString(parameter.Key));
            }

            // Otherwise, iterate the string values and create
            // a new "name=value" pair for each iterated value.
            else
            {
                foreach (var value in parameter.Value)
                {
                    if (builder.Length > 0)
                    {
                        builder.Append('&');
                    }

                    builder.Append(Uri.EscapeDataString(parameter.Key));

                    if (!string.IsNullOrEmpty(value))
                    {
                        builder.Append('=');
                        builder.Append(Uri.EscapeDataString(value));
                    }
                }
            }
        }

        return new UriBuilder(uri) { Query = builder.ToString() }.Uri;
    }

    /// <summary>
    /// Extracts the parameters from the specified query string.
    /// </summary>
    /// <param name="query">The query string, which may start with a '?'.</param>
    /// <returns>The parameters extracted from the specified query string.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    public static IReadOnlyDictionary<string, StringValues> ParseQuery(string query)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        return query.TrimStart(Separators.QuestionMark[0])
            .Split(new[] { Separators.Ampersand[0], Separators.Semicolon[0] }, StringSplitOptions.RemoveEmptyEntries)
            .Select(parameter => parameter.Split(Separators.EqualsSign, StringSplitOptions.RemoveEmptyEntries))
            .Select(parts => (
                Key: parts[0] is string key ? Uri.UnescapeDataString(key) : null,
                Value: parts.Length > 1 && parts[1] is string value ? Uri.UnescapeDataString(value) : null))
            .Where(pair => !string.IsNullOrEmpty(pair.Key))
            .GroupBy(pair => pair.Key)
            .ToDictionary(pair => pair.Key!, pair => new StringValues(pair.Select(parts => parts.Value).ToArray()));
    }

#if SUPPORTS_ECDSA
    /// <summary>
    /// Creates a new <see cref="ECDsa"/> key.
    /// </summary>
    /// <returns>A new <see cref="ECDsa"/> key.</returns>
    /// <exception cref="CryptographicException">
    /// The implementation resolved from <see cref="CryptoConfig.CreateFromName(string)"/> is not valid.
    /// </exception>
    public static ECDsa CreateEcdsaKey()
        => CryptoConfig.CreateFromName("OpenIddict ECDSA Cryptographic Provider") switch
        {
            ECDsa result => result,
            null => ECDsa.Create(),
            var result => throw new CryptographicException(SR.FormatID0351(result.GetType().FullName))
        };

    /// <summary>
    /// Creates a new <see cref="ECDsa"/> key.
    /// </summary>
    /// <param name="curve">The EC curve to use to create the key.</param>
    /// <returns>A new <see cref="ECDsa"/> key.</returns>
    /// <exception cref="CryptographicException">
    /// The implementation resolved from <see cref="CryptoConfig.CreateFromName(string)"/> is not valid.
    /// </exception>
    public static ECDsa CreateEcdsaKey(ECCurve curve)
    {
        var algorithm = CryptoConfig.CreateFromName("OpenIddict ECDSA Cryptographic Provider") switch
        {
            ECDsa result => result,
            null => null,
            var result => throw new CryptographicException(SR.FormatID0351(result.GetType().FullName))
        };

        // If no custom algorithm was registered, use either the static Create() API
        // on platforms that support it or create a default instance provided by the BCL.
        if (algorithm is null)
        {
            return ECDsa.Create(curve);
        }

        try
        {
            algorithm.GenerateKey(curve);
        }

        catch
        {
            algorithm.Dispose();

            throw;
        }

        return algorithm;
    }
#endif

    /// <summary>
    /// Creates a new <see cref="RSA"/> key.
    /// </summary>
    /// <param name="size">The key size to use to create the key.</param>
    /// <returns>A new <see cref="RSA"/> key.</returns>
    /// <exception cref="CryptographicException">
    /// The implementation resolved from <see cref="CryptoConfig.CreateFromName(string)"/> is not valid.
    /// </exception>
    public static RSA CreateRsaKey(int size)
    {
        var algorithm = CryptoConfig.CreateFromName("OpenIddict RSA Cryptographic Provider") switch
        {
            RSA result => result,

#if SUPPORTS_RSA_KEY_CREATION_WITH_SPECIFIED_SIZE
            // Note: on .NET Framework >= 4.7.2, the new RSA.Create(int keySizeInBits) uses
            // CryptoConfig.CreateFromName("RSAPSS") internally, which returns by default
            // a RSACng instance instead of a RSACryptoServiceProvider based on CryptoAPI.
            null => RSA.Create(size),
#else
            // Note: while a RSACng object could be manually instantiated and returned on
            // .NET Framework < 4.7.2, the static RSA.Create() factory (which returns a
            // RSACryptoServiceProvider instance by default) is always preferred to RSACng
            // as this type is known to have compatibility issues on .NET Framework < 4.6.2.
            //
            // Developers who prefer using a CNG-based implementation on .NET Framework 4.6.1
            // can do so by tweaking machine.config or by using CryptoConfig.AddAlgorithm().
            null => RSA.Create(),
#endif
            var result => throw new CryptographicException(SR.FormatID0351(result.GetType().FullName))
        };

        // Note: on .NET Framework, the RSA.Create() overload uses CryptoConfig.CreateFromName()
        // and always returns a RSACryptoServiceProvider instance unless the default name mapping was
        // explicitly overriden in machine.config or via CryptoConfig.AddAlgorithm(). Unfortunately,
        // RSACryptoServiceProvider still uses 1024-bit keys by default and doesn't support changing
        // the key size via RSACryptoServiceProvider.KeySize (setting it has no effect on the object).
        //
        // To ensure the key size matches the requested size, this method replaces the instance by a
        // new RSACryptoServiceProvider using the constructor allowing to override the default key size.
        try
        {
            if (algorithm.KeySize != size)
            {
                if (algorithm is RSACryptoServiceProvider)
                {
                    algorithm.Dispose();
                    algorithm = new RSACryptoServiceProvider(size);
                }

                else
                {
                    algorithm.KeySize = size;
                }

                if (algorithm.KeySize != size)
                {
                    throw new CryptographicException(SR.FormatID0059(algorithm.GetType().FullName));
                }
            }
        }

        catch
        {
            algorithm.Dispose();

            throw;
        }

        return algorithm;
    }

    /// <summary>
    /// Computes the SHA-256 message authentication code (HMAC) of the specified <paramref name="data"/> array.
    /// </summary>
    /// <param name="key">The cryptographic key.</param>
    /// <param name="data">The data to hash.</param>
    /// <returns>The SHA-256 message authentication code (HMAC) of the specified <paramref name="data"/> array.</returns>
    /// <exception cref="CryptographicException">
    /// The implementation resolved from <see cref="CryptoConfig.CreateFromName(string)"/> is not valid.
    /// </exception>
    public static byte[] ComputeSha256MessageAuthenticationCode(byte[] key, byte[] data)
    {
        var algorithm = CryptoConfig.CreateFromName("OpenIddict HMAC SHA-256 Cryptographic Provider", new[] { key }) switch
        {
            HMACSHA256 result => result,
            null => null,
            var result => throw new CryptographicException(SR.FormatID0351(result.GetType().FullName))
        };

        // If no custom algorithm was registered, use either the static/one-shot HashData() API
        // on platforms that support it or create a default instance provided by the BCL.
        if (algorithm is null)
        {
#if SUPPORTS_ONE_SHOT_HASHING_METHODS
            return HMACSHA256.HashData(key, data);
#else
            algorithm = new HMACSHA256(key);
#endif
        }

        try
        {
            return algorithm.ComputeHash(data);
        }

        finally
        {
            algorithm.Dispose();
        }
    }

    /// <summary>
    /// Computes the SHA-256 hash of the specified <paramref name="data"/> array.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The SHA-256 hash of the specified <paramref name="data"/> array.</returns>
    /// <exception cref="CryptographicException">
    /// The implementation resolved from <see cref="CryptoConfig.CreateFromName(string)"/> is not valid.
    /// </exception>
    public static byte[] ComputeSha256Hash(byte[] data)
    {
        var algorithm = CryptoConfig.CreateFromName("OpenIddict SHA-256 Cryptographic Provider") switch
        {
            SHA256 result => result,
            null => null,
            var result => throw new CryptographicException(SR.FormatID0351(result.GetType().FullName))
        };

        // If no custom algorithm was registered, use either the static/one-shot HashData() API
        // on platforms that support it or create a default instance provided by the BCL.
        if (algorithm is null)
        {
#if SUPPORTS_ONE_SHOT_HASHING_METHODS
            return SHA256.HashData(data);
#else
            algorithm = SHA256.Create();
#endif
        }

        try
        {
            return algorithm.ComputeHash(data);
        }

        finally
        {
            algorithm.Dispose();
        }
    }

    /// <summary>
    /// Computes the SHA-384 hash of the specified <paramref name="data"/> array.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The SHA-384 hash of the specified <paramref name="data"/> array.</returns>
    /// <exception cref="CryptographicException">
    /// The implementation resolved from <see cref="CryptoConfig.CreateFromName(string)"/> is not valid.
    /// </exception>
    public static byte[] ComputeSha384Hash(byte[] data)
    {
        var algorithm = CryptoConfig.CreateFromName("OpenIddict SHA-384 Cryptographic Provider") switch
        {
            SHA384 result => result,
            null => null,
            var result => throw new CryptographicException(SR.FormatID0351(result.GetType().FullName))
        };

        // If no custom algorithm was registered, use either the static/one-shot HashData() API
        // on platforms that support it or create a default instance provided by the BCL.
        if (algorithm is null)
        {
#if SUPPORTS_ONE_SHOT_HASHING_METHODS
            return SHA384.HashData(data);
#else
            algorithm = SHA384.Create();
#endif
        }

        try
        {
            return algorithm.ComputeHash(data);
        }

        finally
        {
            algorithm.Dispose();
        }
    }

    /// <summary>
    /// Computes the SHA-512 hash of the specified <paramref name="data"/> array.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The SHA-512 hash of the specified <paramref name="data"/> array.</returns>
    /// <exception cref="CryptographicException">
    /// The implementation resolved from <see cref="CryptoConfig.CreateFromName(string)"/> is not valid.
    /// </exception>
    public static byte[] ComputeSha512Hash(byte[] data)
    {
        var algorithm = CryptoConfig.CreateFromName("OpenIddict SHA-512 Cryptographic Provider") switch
        {
            SHA512 result => result,
            null => null,
            var result => throw new CryptographicException(SR.FormatID0351(result.GetType().FullName))
        };

        // If no custom algorithm was registered, use either the static/one-shot HashData() API
        // on platforms that support it or create a default instance provided by the BCL.
        if (algorithm is null)
        {
#if SUPPORTS_ONE_SHOT_HASHING_METHODS
            return SHA512.HashData(data);
#else
            algorithm = SHA512.Create();
#endif
        }

        try
        {
            return algorithm.ComputeHash(data);
        }

        finally
        {
            algorithm.Dispose();
        }
    }

    /// <summary>
    /// Creates a new array of <see cref="byte"/> containing random data.
    /// </summary>
    /// <param name="size">The desired entropy, in bits.</param>
    /// <returns>A new array of <see cref="byte"/> containing random data.</returns>
    /// <exception cref="CryptographicException">
    /// The implementation resolved from <see cref="CryptoConfig.CreateFromName(string)"/> is not valid.
    /// </exception>
    public static byte[] CreateRandomArray(int size)
    {
        var algorithm = CryptoConfig.CreateFromName("OpenIddict RNG Cryptographic Provider") switch
        {
            RandomNumberGenerator result => result,
            null => null,
            var result => throw new CryptographicException(SR.FormatID0351(result.GetType().FullName))
        };

        // If no custom random number generator was registered, use either the static GetBytes() or
        // Fill() APIs on platforms that support them or create a default instance provided by the BCL.
#if SUPPORTS_ONE_SHOT_RANDOM_NUMBER_GENERATOR_METHODS
        if (algorithm is null)
        {
            return RandomNumberGenerator.GetBytes(size / 8);
        }
#endif
        var array = new byte[size / 8];

#if SUPPORTS_STATIC_RANDOM_NUMBER_GENERATOR_METHODS
        if (algorithm is null)
        {
            RandomNumberGenerator.Fill(array);
            return array;
        }
#else
        algorithm ??= RandomNumberGenerator.Create();
#endif
        try
        {
            algorithm.GetBytes(array);
        }

        finally
        {
            algorithm.Dispose();
        }

        return array;
    }

    /// <summary>
    /// Creates a new <see cref="string"/> containing characters
    /// randomly selected in the specified <paramref name="charset"/>.
    /// </summary>
    /// <param name="charset">The characters allowed to be included in the <see cref="string"/>.</param>
    /// <param name="length">The desired length of the <see cref="string"/>.</param>
    /// <returns>A new <see cref="string"/> containing random data.</returns>
    /// <exception cref="CryptographicException">
    /// The implementation resolved from <see cref="CryptoConfig.CreateFromName(string)"/> is not valid.
    /// </exception>
    public static string CreateRandomString(ReadOnlySpan<char> charset, int length)
    {
        var algorithm = CryptoConfig.CreateFromName("OpenIddict RNG Cryptographic Provider") switch
        {
            RandomNumberGenerator result => result,
            null => null,
            var result => throw new CryptographicException(SR.FormatID0351(result.GetType().FullName))
        };

        try
        {
            var buffer = new char[length];

            for (var index = 0; index < buffer.Length; index++)
            {
                // Pick a character in the specified charset by generating a random index.
                buffer[index] = charset[index: algorithm switch
                {
#if SUPPORTS_INTEGER32_RANDOM_NUMBER_GENERATOR_METHODS
                    // If no custom random number generator was registered, use
                    // the static GetInt32() API on platforms that support it.
                    null => RandomNumberGenerator.GetInt32(0, charset.Length),
#endif
                    // Otherwise, create a default implementation if necessary
                    // and use the local function that achieves the same result.
                    _ => GetInt32(algorithm ??= RandomNumberGenerator.Create(), 0..charset.Length)
                }];
            }

            return new string(buffer);
        }

        finally
        {
            algorithm?.Dispose();
        }

        static int GetInt32(RandomNumberGenerator algorithm, Range range)
        {
            // Note: the logic used here is directly taken from the official implementation
            // of the RandomNumberGenerator.GetInt32() method introduced in .NET Core 3.0.
            //
            // See https://github.com/dotnet/corefx/pull/31243 for more information.

            var count = (uint) range.End.Value - (uint) range.Start.Value - 1;
            if (count is 0)
            {
                return range.Start.Value;
            }

            var mask = count;
            mask |= mask >> 1;
            mask |= mask >> 2;
            mask |= mask >> 4;
            mask |= mask >> 8;
            mask |= mask >> 16;

            var buffer = new byte[sizeof(uint)];
            uint value;

            do
            {
                algorithm.GetBytes(buffer);

                value = mask & BitConverter.ToUInt32(buffer, 0);
            }

            while (value > count);

            return (int) value + range.Start.Value;
        }
    }

    /// <summary>
    /// Determines the equality of two byte sequences in an amount of time
    /// which depends on the length of the sequences, but not the values.
    /// </summary>
    /// <param name="left">The first buffer to compare.</param>
    /// <param name="right">The second buffer to compare.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="left"/> and <paramref name="right"/> have the same values
    /// for <see cref="ReadOnlySpan{T}.Length"/> and the same contents, <see langword="false"/> otherwise.
    /// </returns>
#if !SUPPORTS_TIME_CONSTANT_COMPARISONS
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
#endif
    public static bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
#if SUPPORTS_TIME_CONSTANT_COMPARISONS
        return CryptographicOperations.FixedTimeEquals(left, right);
#else
        // Note: the logic used here is directly taken from the official implementation of
        // the CryptographicOperations.FixedTimeEquals() method introduced in .NET Core 2.1.
        //
        // See https://github.com/dotnet/corefx/pull/27103 for more information.

        // Note: these null checks can be theoretically considered as early checks
        // (which would defeat the purpose of a time-constant comparison method),
        // but the expected string length is the only information an attacker
        // could get at this stage, which is not critical where this method is used.

        if (left.Length != right.Length)
        {
            return false;
        }

        var length = left.Length;
        var accumulator = 0;

        for (var index = 0; index < length; index++)
        {
            accumulator |= left[index] - right[index];
        }

        return accumulator is 0;
#endif
    }

    /// <summary>
    /// Converts the specified hex-encoded <paramref name="value"/> to a byte array.
    /// </summary>
    /// <param name="value">The hexadecimal string.</param>
    /// <returns>The byte array.</returns>
    public static byte[] ConvertFromHexadecimalString(string value)
    {
#if SUPPORTS_HEXADECIMAL_STRING_CONVERSION
        return Convert.FromHexString(value);
#else
        if ((uint) value.Length % 2 is not 0)
        {
            throw new FormatException(SR.GetResourceString(SR.ID0413));
        }

        var array = new byte[value.Length / 2];

        for (var index = 0; index < value.Length; index += 2)
        {
            array[index / 2] = Convert.ToByte(value.Substring(index, 2), 16);
        }

        return array;
#endif
    }

#if SUPPORTS_KEY_DERIVATION_WITH_SPECIFIED_HASH_ALGORITHM
    /// <summary>
    /// Creates a derived key based on the specified <paramref name="secret"/> using PBKDF2.
    /// </summary>
    /// <param name="secret">The secret from which the derived key is created.</param>
    /// <param name="salt">The salt.</param>
    /// <param name="algorithm">The hash algorithm to use.</param>
    /// <param name="iterations">The number of iterations to use.</param>
    /// <param name="length">The desired length of the derived key.</param>
    /// <returns>A derived key based on the specified <paramref name="secret"/>.</returns>
    /// <exception cref="CryptographicException">
    /// The implementation resolved from <see cref="CryptoConfig.CreateFromName(string)"/> is not valid.
    /// </exception>
    public static byte[] DeriveKey(string secret, byte[] salt, HashAlgorithmName algorithm, int iterations, int length)
    {
        // Warning: the type and order of the arguments specified here MUST exactly match the parameters used with
        // Rfc2898DeriveBytes(string password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm).
        using var generator = CryptoConfig.CreateFromName("OpenIddict PBKDF2 Cryptographic Provider",
            args: new object?[] { secret, salt, iterations, algorithm }) switch
        {
            Rfc2898DeriveBytes result => result,

#pragma warning disable CA5379
            null => new Rfc2898DeriveBytes(secret, salt, iterations, algorithm),
#pragma warning restore CA5379

            var result => throw new CryptographicException(SR.FormatID0351(result.GetType().FullName))
        };

        return generator.GetBytes(length);
    }
#endif

#if SUPPORTS_ECDSA
    /// <summary>
    /// Determines whether the specified <paramref name="parameters"/> represent a specific EC curve.
    /// </summary>
    /// <param name="parameters">The <see cref="ECParameters"/>.</param>
    /// <param name="curve">The <see cref="ECCurve"/>.</param>
    /// <returns>
    /// <see langword="true"/> if <see cref="ECParameters.Curve"/> is identical to
    /// the specified <paramref name="curve"/>, <see langword="false"/> otherwise.
    /// </returns>
    public static bool IsEcCurve(ECParameters parameters, ECCurve curve)
    {
        Debug.Assert(parameters.Curve.Oid is not null, SR.GetResourceString(SR.ID4011));
        Debug.Assert(curve.Oid is not null, SR.GetResourceString(SR.ID4011));

        // Warning: on .NET Framework 4.x and .NET Core 2.1, exported ECParameters generally have
        // a null OID value attached. To work around this limitation, both the raw OID values and
        // the friendly names are compared to determine whether the curve is of the specified type.
        if (!string.IsNullOrEmpty(parameters.Curve.Oid.Value) &&
            !string.IsNullOrEmpty(curve.Oid.Value))
        {
            return string.Equals(parameters.Curve.Oid.Value,
                curve.Oid.Value, StringComparison.Ordinal);
        }

        if (!string.IsNullOrEmpty(parameters.Curve.Oid.FriendlyName) &&
            !string.IsNullOrEmpty(curve.Oid.FriendlyName))
        {
            return string.Equals(parameters.Curve.Oid.FriendlyName,
                curve.Oid.FriendlyName, StringComparison.Ordinal);
        }

        Debug.Fail(SR.GetResourceString(SR.ID4012));
        return false;
    }
#endif
}
