using System.Security.Cryptography;

namespace AsyncMonolith.Utilities;

/// <summary>
///     Represents an asynchronous monolith ID generator.
/// </summary>
public interface IAsyncMonolithIdGenerator
{
    /// <summary>
    ///     Generates a new ID.
    /// </summary>
    /// <returns>The generated ID.</returns>
    string GenerateId();
}

/// <summary>
///     Represents an implementation of the <see cref="IAsyncMonolithIdGenerator" /> interface.
/// </summary>
public sealed class AsyncMonolithIdGenerator : IAsyncMonolithIdGenerator
{
    private const string ValidIdCharacters = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const int Length = 12;

    private static readonly char[] Characters = ValidIdCharacters.ToCharArray();
    private static readonly int CharacterSetLength = Characters.Length;
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

    /// <inheritdoc />
    public string GenerateId()
    {
        var result = new char[Length];
        var buffer = new byte[Length * 4]; // Allocate buffer for 4 bytes per character

        Rng.GetBytes(buffer); // Fill buffer with cryptographically secure random bytes

        for (var i = 0; i < Length; i++)
            result[i] = Characters[BitConverter.ToUInt32(buffer, i * 4) % CharacterSetLength];

        return new string(result);
    }
}