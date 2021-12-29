using System;
using System.Runtime.InteropServices;
using System.Security;

namespace OsEngine.Market.Servers.Kraken2.Sockets
{
    public static class SecureStringExtensions
    {
        /// <summary>
        /// Converts a <see cref="SecureString"/> instance to plain <see cref="string"/>.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">value</exception>
        public static string ToPlainString(this SecureString value)
        {
#pragma warning disable S1854 // Unused assignments should be removed
            IntPtr unmanagedString = IntPtr.Zero;
#pragma warning restore S1854 // Unused assignments should be removed
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(value ?? throw new ArgumentNullException(nameof(value)));
                return Marshal.PtrToStringUni(unmanagedString);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }

        /// <summary>
        /// Converts to a <see cref="SecureString"/> instance.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">value</exception>
        public static SecureString ToSecureString(this string value)
        {
            _ = value ?? throw new ArgumentNullException(nameof(value));

            var secureString = new SecureString();
            secureString.Clear();

            foreach (var character in value)
            {
                secureString.AppendChar(character);
            }
            secureString.MakeReadOnly();

            return secureString;
        }
    }
}
