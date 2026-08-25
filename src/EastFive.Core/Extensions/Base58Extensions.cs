using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace EastFive
{
    // Adapted from https://gist.github.com/CodesInChaos/3175971 
    // placed into public domain by CodesInChaos Frankfurt, Germany
    // July 25, 2012
    
    /// <summary>
    /// Extensions methods for Base58
    /// </summary>
    public static class Base58Extensions
    {
		#region Encoded / decode

		#region Encode

		public static string Base58Encode(this byte[] data)
		{
			// Decode byte[] to BigInteger
			BigInteger intData = 0;
			for (int i = 0; i < data.Length; i++)
			{
				intData = intData * 256 + data[i];
			}

			// Encode BigInteger to Base58 string
			string result = "";
			while (intData > 0)
			{
				int remainder = (int)(intData % 58);
				intData /= 58;
				result = Digits[remainder] + result;
			}

			// Append `1` for each leading 0 byte
			for (int i = 0; i < data.Length && data[i] == 0; i++)
			{
				result = '1' + result;
			}
			return result;
		}

		public static string Base58EncodeWithCheckSum(this byte[] data)
		{
			return Base58Encode(AddCheckSum(data));
		}

		#endregion

		#region Decode

		#region No Checksum

		public static byte[] Base58Decode(this string s)
		{
			if(!s.TryBase58Decode(out byte [] decodedData, out string failureMessage))
				throw new FormatException(failureMessage);

			return decodedData;
		}

		public static bool TryBase58Decode(this string s, out byte[] decodedData)
		{
			return s.TryBase58Decode(out decodedData, out string discard);
		}

		public static bool TryBase58Decode(this string s,
			out byte[] decodedData, out string failureReason)
		{
			// Decode Base58 string to BigInteger 
			BigInteger intData = 0;
			for (int i = 0; i < s.Length; i++)
			{
				int digit = Digits.IndexOf(s[i]); //Slow
				if (digit < 0)
				{
					failureReason = string.Format("Invalid Base58 character `{0}` at position {1}", s[i], i);
					decodedData = null;
					return false;
				}
				intData = intData * 58 + digit;
			}

			// Encode BigInteger to byte[]
			// Leading zero bytes get encoded as leading `1` characters
			int leadingZeroCount = s.TakeWhile(c => c == '1').Count();
			var leadingZeros = Enumerable.Repeat((byte)0, leadingZeroCount);
			var bytesWithoutLeadingZeros =
				intData.ToByteArray()
				.Reverse()// to big endian
				.SkipWhile(b => b == 0);//strip sign byte
			decodedData = leadingZeros.Concat(bytesWithoutLeadingZeros).ToArray();
			failureReason = string.Empty;
			return true;
		}

		#endregion

		#region With Checksum

		// Throws `FormatException` if s is not a valid Base58 string, or the checksum is invalid
		public static byte[] Base58DecodeWithCheckSum(this string s)
		{
			if(!s.TryBase58DecodeWithCheckSum(out byte[] dataWithoutCheckSum, out string failureMessage))
				throw new FormatException(failureMessage);
			return dataWithoutCheckSum;
		}

		public static bool TryBase58DecodeWithCheckSum(this string s,
			out byte[] dataWithoutCheckSum)
		{
			return TryBase58DecodeWithCheckSum(s,
				out dataWithoutCheckSum, out string discard);
		}

		public static bool TryBase58DecodeWithCheckSum(this string s, 
			out byte[] dataWithoutCheckSum, out string failureMessage)
		{
			if (!TryBase58Decode(s, out byte[] dataWithCheckSum, out failureMessage))
			{
				dataWithoutCheckSum = null;
				return false;
			}
			if(!TryVerifyAndRemoveCheckSum(dataWithCheckSum, out dataWithoutCheckSum))
			{
				failureMessage = "Base58 checksum is invalid";
				return false;
			}
			return true;
		}

		#endregion

		#endregion

		#endregion

		#region Utility

		private const int CheckSumSizeInBytes = 4;
		private const string Digits = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

		private static byte[] AddCheckSum(byte[] data)
		{
			byte[] checkSum = GetCheckSum(data);
			byte[] dataWithCheckSum = ArrayHelpers.ConcatArrays(data, checkSum);
			return dataWithCheckSum;
		}

		//Returns null if the checksum is invalid
		private static bool TryVerifyAndRemoveCheckSum(byte[] data,
			out byte[] dataWithoutCheckSum)
		{
			dataWithoutCheckSum = ArrayHelpers.SubArray(data, 0, data.Length - CheckSumSizeInBytes);
			byte[] givenCheckSum = ArrayHelpers.SubArray(data, data.Length - CheckSumSizeInBytes);
			byte[] correctCheckSum = GetCheckSum(dataWithoutCheckSum);
			if (givenCheckSum.SequenceEqual(correctCheckSum))
				return true;
			else
				return false;
		}

		private static byte[] GetCheckSum(byte[] data)
		{
			using (var algorithm = SHA256.Create())
			{
				byte[] hash1 = algorithm.ComputeHash(data);
				byte[] hash2 = algorithm.ComputeHash(hash1);

				var result = new byte[CheckSumSizeInBytes];
				Buffer.BlockCopy(hash2, 0, result, 0, result.Length);
				return result;
			}
		}

		public class ArrayHelpers
		{
			public static T[] ConcatArrays<T>(T[] arr1, T[] arr2)
			{
				var result = new T[arr1.Length + arr2.Length];

				#pragma warning disable CA2018
				Buffer.BlockCopy(arr1, 0, result, 0, arr1.Length);
				Buffer.BlockCopy(arr2, 0, result, arr1.Length, arr2.Length);
				#pragma warning restore CA2018

				return result;
			}

			public static T[] SubArray<T>(T[] arr, int start, int length)
			{
				var result = new T[length];
				Buffer.BlockCopy(arr, start, result, 0, length);
				return result;
			}

			public static T[] SubArray<T>(T[] arr, int start)
			{
				return SubArray(arr, start, arr.Length - start);
			}
		}

		#endregion
	}
}
