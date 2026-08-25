using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace EastFive.Serialization
{
    public static class GuidCompositionExtensions
    {
        public static Guid ComposeGuid(this Guid guid1, Guid guid2)
        {
            var id = guid1.ToByteArray().Concat(guid2.ToByteArray()).ToArray().MD5HashGuid();
            return id;
        }

        public static Guid ComposeGuid(this IReferenceable ref1, IReferenceable ref2)
            => ref1.id.ComposeGuid(ref2);

        public static Guid ComposeGuid(this Guid guid1, byte[] data)
        {
            if (data.IsDefaultNullOrEmpty())
            {
                return guid1;
            }

            var id = guid1
                .ToByteArray()
                .Concat(data)
                .ToArray()
                .MD5HashGuid();
            return id;
        }

        public static Guid ComposeGuid(this Guid guid1, string textKey)
        {
            if (textKey.IsDefaultOrNull())
            {
                var noHashButChanged = guid1
                    .ToByteArray()
                    .ToArray()
                    .MD5HashGuid();
                return noHashButChanged;
            }

            var id = guid1
                .ToByteArray()
                .Concat(
                    Encoding.UTF8.GetBytes(textKey))
                .ToArray()
                .MD5HashGuid();
            return id;
        }

        public static Guid ComposeGuid(this Guid guid1, int intKey)
        {
            var id = guid1
                .ToByteArray()
                .Concat(BitConverter.GetBytes(intKey))
                .ToArray()
                .MD5HashGuid();
            return id;
        }

        public static Guid ComposeGuid(this Guid guid, DateTime dateTime, TimeSpanUnits? fidelityMaybe =default)
        {
            var id = guid
                .ToByteArray()
                .Concat(GetDateBytes())
                .ToArray()
                .MD5HashGuid();
            return id;

            byte[] GetDateBytes()
            {
                if (!fidelityMaybe.HasValue)
                    return BitConverter.GetBytes(dateTime.Ticks);

                var fidelity = fidelityMaybe.Value;
                if(fidelity == TimeSpanUnits.continuous)
                    return BitConverter.GetBytes(dateTime.Ticks);

                var bytes = BitConverter.GetBytes(dateTime.Year);
                if (fidelity == TimeSpanUnits.years)
                    return bytes;

                if (fidelity == TimeSpanUnits.months)
                    return bytes.Concat(BitConverter.GetBytes(dateTime.Month)).ToArray();

                if (fidelity == TimeSpanUnits.weeks)
                {
                    var timeSinceEpoch = dateTime - new DateTime(1, 1, 1);
                    var weeks = timeSinceEpoch.Days / 7;
                    return bytes.Concat(BitConverter.GetBytes(weeks)).ToArray();
                }

                bytes = bytes.Concat(BitConverter.GetBytes(dateTime.DayOfYear)).ToArray();
                if (fidelity == TimeSpanUnits.days)
                    return bytes;

                bytes = bytes.Concat(BitConverter.GetBytes(dateTime.Hour)).ToArray();
                if (fidelity == TimeSpanUnits.hours)
                    return bytes;

                bytes = bytes.Concat(BitConverter.GetBytes(dateTime.Minute)).ToArray();
                if (fidelity == TimeSpanUnits.minutes)
                    return bytes;

                bytes = bytes.Concat(BitConverter.GetBytes(dateTime.Second)).ToArray();
                if (fidelity == TimeSpanUnits.seconds)
                    return bytes;

                return bytes;
            }
        }

        public static Guid ComposeGuid(this Guid guid1, Guid guid2, int intKey1)
        {
            var guid1Bytes = guid1.ToByteArray();
            var guid2Bytes = guid2.ToByteArray();
            var intKey1Bytes = BitConverter.GetBytes(intKey1);
            var id = guid1Bytes
                .Concat(guid2Bytes)
                .Concat(intKey1Bytes)
                .ToArray()
                .MD5HashGuid();
            return id;
        }

        public static Guid ComposeGuid(this Guid guid1, IReferenceable refRef)
        {
            var refId = refRef.IsDefaultOrNull() ? default(Guid) : refRef.id;
            var id = guid1.ToByteArray().Concat(refId.ToByteArray()).ToArray().MD5HashGuid();
            return id;
        }

        public static Guid NextGuid(this Guid guid1)
        {
            var id = guid1.ToByteArray().MD5HashGuid();
            return id;
        }
    }
}
