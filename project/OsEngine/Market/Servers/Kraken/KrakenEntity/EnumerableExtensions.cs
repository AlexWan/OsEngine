using System;
using System.Collections.Generic;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.Kraken.KrakenEntity
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Limits the rate at which the sequence is enumerated.
        /// </summary>
        /// <typeparam name="T">The type of the elements of <paramref name="source"/>.</typeparam>
        /// <param name="source">The <see cref="IEnumerable{T}"/> whose enumeration is to be rate limited.</param>
        /// <param name="count">The number of items in the sequence that are allowed to be processed per time unit.</param>
        /// <param name="timeUnit">Length of the time unit.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> containing the elements of the source sequence.</returns>
        public static IEnumerable<T> LimitRate<T>(this IEnumerable<T> source, int count, TimeSpan timeUnit)
        {
            using (var rateGate = new RateGate(count, timeUnit))
            {
                foreach (var item in source)
                {
                    rateGate.WaitToProceed();
                    yield return item;
                }
            }
        }
    }
}