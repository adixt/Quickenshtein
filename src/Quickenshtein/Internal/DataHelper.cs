﻿using System;
using System.Numerics;
using System.Runtime.CompilerServices;
#if NETCOREAPP
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

#if TARGET_32BIT
using nint = System.Int32;
#else
using nint = System.Int64;
#endif

namespace Quickenshtein.Internal
{
	internal static class DataHelper
	{
#if NETCOREAPP
		private static readonly Vector128<int> VECTOR128_INT_ZERO_TO_SEVEN = Vector128.Create(0, 1, 2, 3);
		private static readonly Vector256<int> VECTOR256_INT_ZERO_TO_SEVEN = Vector256.Create(0, 1, 2, 3, 4, 5, 6, 7);
#endif

		/// <summary>
		/// Fills <paramref name="targetPtr"/> with a number sequence from 1 to the length specified.
		/// </summary>
		/// <param name="targetPtr"></param>
		/// <param name="length"></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void SequentialFill(int* targetPtr, int length)
		{
#if NETCOREAPP
			SequentialFill(targetPtr, 1, length);
#else
			var index = 0;

			while (length >= 8)
			{
				length -= 8;
				targetPtr[index] = ++index;
				targetPtr[index] = ++index;
				targetPtr[index] = ++index;
				targetPtr[index] = ++index;
				targetPtr[index] = ++index;
				targetPtr[index] = ++index;
				targetPtr[index] = ++index;
				targetPtr[index] = ++index;
			}

			if (length > 4)
			{
				length -= 4;
				targetPtr[index] = ++index;
				targetPtr[index] = ++index;
				targetPtr[index] = ++index;
				targetPtr[index] = ++index;
			}

			while (length > 0)
			{
				length--;
				targetPtr[index] = ++index;
			}
#endif
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void SequentialFill(int* targetPtr, int startValue, int length)
		{
			var index = 0;
			var value = startValue;
			nint lengthToProcess = length;

#if NETCOREAPP
			if (Sse2.IsSupported && length >= Vector128<int>.Count * 2)
			{
				lengthToProcess = UnalignedCountVector128(targetPtr);
			}
#endif

			while (lengthToProcess >= 8)
			{
				lengthToProcess -= 8;
				targetPtr[index++] = value++;
				targetPtr[index++] = value++;
				targetPtr[index++] = value++;
				targetPtr[index++] = value++;
				targetPtr[index++] = value++;
				targetPtr[index++] = value++;
				targetPtr[index++] = value++;
				targetPtr[index++] = value++;
			}

			if (lengthToProcess > 4)
			{
				lengthToProcess -= 4;
				targetPtr[index++] = value++;
				targetPtr[index++] = value++;
				targetPtr[index++] = value++;
				targetPtr[index++] = value++;
			}

			while (lengthToProcess > 0)
			{
				lengthToProcess--;
				targetPtr[index++] = value++;
			}

#if NETCOREAPP
			lengthToProcess = length - lengthToProcess;
			if (lengthToProcess > 0)
			{
				if (Avx2.IsSupported)
				{
					var shiftVector256 = Vector256.Create(8);
					var lastVector256 = Avx2.Add(Vector256.Create(value), VECTOR256_INT_ZERO_TO_SEVEN);

					while (lengthToProcess >= Vector256<int>.Count)
					{
						Avx.Store(targetPtr + index, lastVector256);
						lastVector256 = Avx2.Add(lastVector256, shiftVector256);
						index += Vector256<int>.Count;
						lengthToProcess -= Vector256<int>.Count;
					}

					if (lengthToProcess >= Vector128<int>.Count)
					{
						Sse2.Store(targetPtr + index, lastVector256.GetLower());
						index += Vector128<int>.Count;
						lengthToProcess -= Vector128<int>.Count;
						value = lastVector256.GetElement(4);
					}
					else
					{
						value = lastVector256.GetElement(0);
					}
				}
				else if (Sse2.IsSupported)
				{
					var shiftVector128 = Vector128.Create(8);
					var lastVector128 = Sse2.Add(Vector128.Create(value), VECTOR128_INT_ZERO_TO_SEVEN);

					while (lengthToProcess >= Vector128<int>.Count)
					{
						Sse2.Store(targetPtr + index, lastVector128);
						lastVector128 = Sse2.Add(lastVector128, shiftVector128);
						index += Vector128<int>.Count;
						lengthToProcess -= Vector128<int>.Count;
					}

					value = lastVector128.GetElement(0);
				}

				while (lengthToProcess > 0)
				{
					lengthToProcess--;
					targetPtr[index++] = value++;
				}
			}
#endif
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe int GetIndexOfFirstNonMatchingCharacter(char* sourcePtr, char* targetPtr, int sourceLength, int targetLength)
		{
			var searchLength = Math.Min(sourceLength, targetLength);
			var index = 0;

#if NETCOREAPP
			var sourceUShortPtr = (ushort*)sourcePtr;
			var targetUShortPtr = (ushort*)targetPtr;

			if (Sse2.IsSupported && searchLength >= Vector128<ushort>.Count * 2)
			{
				if (Avx2.IsSupported)
				{
					while (searchLength >= Vector256<ushort>.Count)
					{
						var sourceVector = Avx.LoadDquVector256(sourceUShortPtr + index);
						var targetVector = Avx.LoadDquVector256(targetUShortPtr + index);
						var match = (uint)Avx2.MoveMask(
							Avx2.CompareEqual(
								sourceVector,
								targetVector
							).AsByte()
						);

						if (match == uint.MaxValue)
						{
							index += Vector256<ushort>.Count;
							searchLength -= Vector256<ushort>.Count;
							continue;
						}

						index += FirstMaskMatchIndex(match);
						return index;
					}
				}

				while (searchLength >= Vector128<ushort>.Count)
				{
					var sourceVector = Sse2.LoadVector128(sourceUShortPtr + index);
					var targetVector = Sse2.LoadVector128(targetUShortPtr + index);
					var match = (uint)Sse2.MoveMask(
						Sse2.CompareEqual(
							sourceVector,
							targetVector
						).AsByte()
					);

					if (match == uint.MaxValue)
					{
						index += Vector128<ushort>.Count;
						searchLength -= Vector128<ushort>.Count;
						continue;
					}

					index += FirstMaskMatchIndex(match);
					return index;
				}
			}
#endif

			while (searchLength > 0 && sourcePtr[index] == targetPtr[index])
			{
				searchLength--;
				index++;
			}

			return index;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void GetIndexesOfLastNonMatchingCharacters(char* sourcePtr, char* targetPtr, int startIndex, int sourceLength, int targetLength, out int sourceEnd, out int targetEnd)
		{
			var searchLength = Math.Min(sourceLength, targetLength) - startIndex;
			sourceEnd = sourceLength;
			targetEnd = targetLength;

#if NETCOREAPP
			var sourceUShortPtr = (ushort*)sourcePtr;
			var targetUShortPtr = (ushort*)targetPtr;

			if (Sse2.IsSupported && searchLength >= Vector128<ushort>.Count * 2)
			{
				if (Avx2.IsSupported)
				{
					while (searchLength >= Vector256<ushort>.Count)
					{
						var sourceVector = Avx.LoadDquVector256(sourceUShortPtr + sourceEnd - Vector256<ushort>.Count);
						var targetVector = Avx.LoadDquVector256(targetUShortPtr + targetEnd - Vector256<ushort>.Count);
						var match = (uint)Avx2.MoveMask(
							Avx2.CompareEqual(
								sourceVector,
								targetVector
							).AsByte()
						);

						if (match == uint.MaxValue)
						{
							sourceEnd -= Vector256<ushort>.Count;
							targetEnd -= Vector256<ushort>.Count;
							searchLength -= Vector256<ushort>.Count;
							continue;
						}

						var lastMatch = LastMaskMatchIndex(match);
						sourceEnd -= lastMatch;
						targetEnd -= lastMatch;
						return;
					}
				}

				while (searchLength >= Vector128<ushort>.Count)
				{
					var sourceVector = Sse2.LoadVector128(sourceUShortPtr + sourceEnd - Vector128<ushort>.Count);
					var targetVector = Sse2.LoadVector128(targetUShortPtr + targetEnd - Vector128<ushort>.Count);
					var match = (uint)Sse2.MoveMask(
						Sse2.CompareEqual(
							sourceVector,
							targetVector
						).AsByte()
					);

					if (match == uint.MaxValue)
					{
						sourceEnd -= Vector128<ushort>.Count;
						targetEnd -= Vector128<ushort>.Count;
						searchLength -= Vector128<ushort>.Count;
						continue;
					}

					var lastMatch = LastMaskMatchIndex(match);
					sourceEnd -= lastMatch;
					targetEnd -= lastMatch;
					return;
				}
			}
#endif

			while (searchLength > 0 && sourcePtr[sourceEnd - 1] == targetPtr[targetEnd - 1])
			{
				sourceEnd--;
				targetEnd--;
				searchLength--;
			}
		}

#if NETCOREAPP
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int FirstMaskMatchIndex(uint match)
		{
			return BitOperations.TrailingZeroCount(match ^ uint.MaxValue) / sizeof(ushort);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int LastMaskMatchIndex(uint match)
		{
			return BitOperations.LeadingZeroCount(match ^ uint.MaxValue) / sizeof(ushort);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static unsafe nint UnalignedCountVector128(int* targetPtr)
		{
			return (uint)targetPtr / sizeof(int) & (Vector128<int>.Count - 1);
		}
#endif
	}
}
