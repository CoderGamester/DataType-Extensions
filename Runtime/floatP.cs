﻿using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace

namespace GameLovers
{
	/// <summary>
	/// Floating Point representation is identical to IEEE binary32 floatingpoints.
	/// Valuable for deterministic cases where <see cref="float"/> is not a valid option
	/// </summary>
	/// <author>
	/// https://github.com/CodesInChaos/floatP/tree/master
	/// </author>
	[DebuggerDisplay("{ToStringInv()}")]
	public struct floatP : IEquatable<floatP>, IComparable<floatP>, IComparable, IFormattable
	{
		/// <summary>
		/// Raw byte representation of an floatP number
		/// </summary>
		private readonly uint _raw;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal floatP(uint raw)
		{
			_raw = raw;
		}

		/// <summary>
		/// Creates an floatP number from its raw byte representation
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static floatP FromRaw(uint raw)
		{
			return new floatP(raw);
		}

		/// <summary>
		/// The raw value that determines the base of this flaoting point
		/// </summary>
		public uint RawValue => _raw;

		internal uint RawMantissa { get { return _raw & 0x7FFFFF; } }
		internal int Mantissa
		{
			get
			{
				if (RawExponent != 0)
				{
					uint sign = (uint)((int)_raw >> 31);
					return (int)(((RawMantissa | 0x800000) ^ sign) - sign);
				}
				else
				{
					uint sign = (uint)((int)_raw >> 31);
					return (int)(((RawMantissa) ^ sign) - sign);
				}
			}
		}

		internal sbyte Exponent { get { return (sbyte)(RawExponent - ExponentBias); } }
		internal byte RawExponent { get { return (byte)(_raw >> MantissaBits); } }

		private const uint SignMask = 0x80000000;
		private const int MantissaBits = 23;
		private const int ExponentBias = 127;

		private const uint RawZero = 0;
		private const uint RawNaN = 0xFFC00000;//same as float.NaN
		private const uint RawPositiveInfinity = 0x7F800000;
		private const uint RawNegativeInfinity = RawPositiveInfinity ^ SignMask;
		private const uint RawOne = 0x3F800000;
		private const uint RawMinusOne = RawOne ^ SignMask;
		private const uint RawMaxValue = 0x7F7FFFFF;
		private const uint RawMinValue = 0x7F7FFFFF ^ SignMask;
		private const uint RawEpsilon = 0x00000001;

		public static floatP Zero { get { return new floatP(); } }
		public static floatP PositiveInfinity { get { return new floatP(RawPositiveInfinity); } }
		public static floatP NegativeInfinity { get { return new floatP(RawNegativeInfinity); } }
		public static floatP NaN { get { return new floatP(RawNaN); } }
		public static floatP One { get { return new floatP(RawOne); } }
		public static floatP MinusOne { get { return new floatP(RawMinusOne); } }
		public static floatP MaxValue { get { return new floatP(RawMaxValue); } }
		public static floatP MinValue { get { return new floatP(RawMinValue); } }
		public static floatP Epsilon { get { return new floatP(RawEpsilon); } }

		public override string ToString() => ((float)this).ToString();

		/// <summary>
		/// Creates an floatP number from its parts: sign, exponent, mantissa
		/// </summary>
		/// <param name="sign">Sign of the number: false = the number is positive, true = the number is negative</param>
		/// <param name="exponent">Exponent of the number</param>
		/// <param name="mantissa">Mantissa (significand) of the number</param>
		/// <returns></returns>
		public static floatP FromParts(bool sign, uint exponent, uint mantissa)
		{
			return FromRaw((sign ? SignMask : 0) | ((exponent & 0xff) << MantissaBits) | (mantissa & ((1 << MantissaBits) - 1)));
		}

		/// <summary>
		/// Creates an floatP number from a float value
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator floatP(float f)
		{
			uint raw = ReinterpretFloatToInt32(f);
			return new floatP(raw);
		}

		/// <summary>
		/// Converts an floatP number to a float value
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator float(floatP f)
		{
			uint raw = f._raw;
			return ReinterpretIntToFloat32(raw);
		}

		/// <summary>
		/// Converts an floatP number to an integer
		/// </summary>
		public static implicit operator int(floatP f)
		{
			if (f.Exponent < 0)
			{
				return 0;
			}

			int shift = MantissaBits - f.Exponent;
			var mantissa = (int)(f.RawMantissa | (1 << MantissaBits));
			int value = shift < 0 ? mantissa << -shift : mantissa >> shift;
			return f.IsPositive() ? value : -value;
		}

		/// <summary>
		/// Creates an floatP number from an integer
		/// </summary>
		public static implicit operator floatP(int value)
		{
			if (value == 0)
			{
				return Zero;
			}

			if (value == int.MinValue)
			{
				// special case
				return FromRaw(0xcf000000);
			}

			bool negative = value < 0;
			uint u = (uint)Math.Abs(value);

			int shifts;

			uint lzcnt = clz(u);
			if (lzcnt < 8)
			{
				int count = 8 - (int)lzcnt;
				u >>= count;
				shifts = -count;
			}
			else
			{
				int count = (int)lzcnt - 8;
				u <<= count;
				shifts = count;
			}

			uint exponent = (uint)(ExponentBias + MantissaBits - shifts);
			return FromParts(negative, exponent, u);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static floatP operator -(floatP f)
		{
			return new floatP(f._raw ^ 0x80000000);
		}

		public static floatP operator +(floatP f1, floatP f2)
		{
			return f1.RawExponent - f2.RawExponent >= 0 ? InternalAdd(f1, f2) : InternalAdd(f2, f1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static floatP operator -(floatP f1, floatP f2)
		{
			return f1 + (-f2);
		}

		public static floatP operator *(floatP f1, floatP f2)
		{
			int man1;
			int rawExp1 = f1.RawExponent;
			uint sign1;
			uint sign2;
			if (rawExp1 == 0)
			{
				// SubNorm
				sign1 = (uint)((int)f1.RawValue >> 31);
				uint rawMan1 = f1.RawMantissa;
				if (rawMan1 == 0)
				{
					if (f2.IsFinite())
					{
						// 0 * f2
						return new floatP((f1.RawValue ^ f2.RawValue) & SignMask);
					}
					else
					{
						// 0 * Infinity
						// 0 * NaN
						return NaN;
					}
				}

				rawExp1 = 1;
				while ((rawMan1 & 0x800000) == 0)
				{
					rawMan1 <<= 1;
					rawExp1--;
				}

				//Debug.Assert(rawMan1 >> MantissaBits == 1);
				man1 = (int)((rawMan1 ^ sign1) - sign1);
			}
			else if (rawExp1 != 255)
			{
				// Norm
				sign1 = (uint)((int)f1.RawValue >> 31);
				man1 = (int)(((f1.RawMantissa | 0x800000) ^ sign1) - sign1);
			}
			else
			{
				// Non finite
				if (f1.RawValue == RawPositiveInfinity)
				{
					if (f2.IsZero())
					{
						// Infinity * 0
						return NaN;
					}

					if (f2.IsNaN())
					{
						// Infinity * NaN
						return NaN;
					}

					if ((int)f2.RawValue >= 0)
					{
						// Infinity * f
						return PositiveInfinity;
					}
					else
					{
						// Infinity * -f
						return NegativeInfinity;
					}
				}
				else if (f1.RawValue == RawNegativeInfinity)
				{
					if (f2.IsZero() || f2.IsNaN())
					{
						// -Infinity * 0
						// -Infinity * NaN
						return NaN;
					}

					if ((int)f2.RawValue < 0)
					{
						// -Infinity * -f
						return PositiveInfinity;
					}
					else
					{
						// -Infinity * f
						return NegativeInfinity;
					}
				}
				else
				{
					return f1;
				}
			}

			int man2;
			int rawExp2 = f2.RawExponent;
			if (rawExp2 == 0)
			{
				// SubNorm
				sign2 = (uint)((int)f2.RawValue >> 31);
				uint rawMan2 = f2.RawMantissa;
				if (rawMan2 == 0)
				{
					if (f1.IsFinite())
					{
						// f1 * 0
						return new floatP((f1.RawValue ^ f2.RawValue) & SignMask);
					}
					else
					{
						// Infinity * 0
						// NaN * 0
						return NaN;
					}
				}

				rawExp2 = 1;
				while ((rawMan2 & 0x800000) == 0)
				{
					rawMan2 <<= 1;
					rawExp2--;
				}
				//Debug.Assert(rawMan2 >> MantissaBits == 1);
				man2 = (int)((rawMan2 ^ sign2) - sign2);
			}
			else if (rawExp2 != 255)
			{
				// Norm
				sign2 = (uint)((int)f2.RawValue >> 31);
				man2 = (int)(((f2.RawMantissa | 0x800000) ^ sign2) - sign2);
			}
			else
			{
				// Non finite
				if (f2.RawValue == RawPositiveInfinity)
				{
					if (f1.IsZero())
					{
						// 0 * Infinity
						return NaN;
					}

					if ((int)f1.RawValue >= 0)
					{
						// f * Infinity
						return PositiveInfinity;
					}
					else
					{
						// -f * Infinity
						return NegativeInfinity;
					}
				}
				else if (f2.RawValue == RawNegativeInfinity)
				{
					if (f1.IsZero())
					{
						// 0 * -Infinity
						return NaN;
					}

					if ((int)f1.RawValue < 0)
					{
						// -f * -Infinity
						return PositiveInfinity;
					}
					else
					{
						// f * -Infinity
						return NegativeInfinity;
					}
				}
				else
				{
					return f2;
				}
			}

			long longMan = (long)man1 * (long)man2;
			int man = (int)(longMan >> MantissaBits);
			//Debug.Assert(man != 0);
			uint absMan = (uint)Math.Abs(man);
			int rawExp = rawExp1 + rawExp2 - ExponentBias;
			uint sign = (uint)man & 0x80000000;
			if ((absMan & 0x1000000) != 0)
			{
				absMan >>= 1;
				rawExp++;
			}

			//Debug.Assert(absMan >> MantissaBits == 1);
			if (rawExp >= 255)
			{
				// Overflow
				return new floatP(sign ^ RawPositiveInfinity);
			}

			if (rawExp <= 0)
			{
				// Subnorms/Underflow
				if (rawExp <= -24)
				{
					return new floatP(sign);
				}

				absMan >>= -rawExp + 1;
				rawExp = 0;
			}

			uint raw = sign | (uint)rawExp << MantissaBits | absMan & 0x7FFFFF;
			return new floatP(raw);
		}

		public static floatP operator /(floatP f1, floatP f2)
		{
			if (f1.IsNaN() || f2.IsNaN())
			{
				return NaN;
			}

			int man1;
			int rawExp1 = f1.RawExponent;
			uint sign1;
			uint sign2;
			if (rawExp1 == 0)
			{
				// SubNorm
				sign1 = (uint)((int)f1.RawValue >> 31);
				uint rawMan1 = f1.RawMantissa;
				if (rawMan1 == 0)
				{
					if (f2.IsZero())
					{
						// 0 / 0
						return NaN;
					}
					else
					{
						// 0 / f
						return new floatP((f1.RawValue ^ f2.RawValue) & SignMask);
					}
				}

				rawExp1 = 1;
				while ((rawMan1 & 0x800000) == 0)
				{
					rawMan1 <<= 1;
					rawExp1--;
				}

				//Debug.Assert(rawMan1 >> MantissaBits == 1);
				man1 = (int)((rawMan1 ^ sign1) - sign1);
			}
			else if (rawExp1 != 255)
			{
				// Norm
				sign1 = (uint)((int)f1.RawValue >> 31);
				man1 = (int)(((f1.RawMantissa | 0x800000) ^ sign1) - sign1);
			}
			else
			{
				// Non finite
				if (f1.RawValue == RawPositiveInfinity)
				{
					if (f2.IsZero())
					{
						// Infinity / 0
						return PositiveInfinity;
					}

					// +-Infinity / Infinity
					return NaN;
				}
				else if (f1.RawValue == RawNegativeInfinity)
				{
					if (f2.IsZero())
					{
						// -Infinity / 0
						return NegativeInfinity;
					}

					// -Infinity / +-Infinity
					return NaN;
				}
				else
				{
					// NaN
					return f1;
				}
			}

			int man2;
			int rawExp2 = f2.RawExponent;
			if (rawExp2 == 0)
			{
				// SubNorm
				sign2 = (uint)((int)f2.RawValue >> 31);
				uint rawMan2 = f2.RawMantissa;
				if (rawMan2 == 0)
				{
					// f / 0
					return new floatP(((f1.RawValue ^ f2.RawValue) & SignMask) | RawPositiveInfinity);
				}

				rawExp2 = 1;
				while ((rawMan2 & 0x800000) == 0)
				{
					rawMan2 <<= 1;
					rawExp2--;
				}

				//Debug.Assert(rawMan2 >> MantissaBits == 1);
				man2 = (int)((rawMan2 ^ sign2) - sign2);
			}
			else if (rawExp2 != 255)
			{
				// Norm
				sign2 = (uint)((int)f2.RawValue >> 31);
				man2 = (int)(((f2.RawMantissa | 0x800000) ^ sign2) - sign2);
			}
			else
			{
				// Non finite
				if (f2.RawValue == RawPositiveInfinity)
				{
					if (f1.IsZero())
					{
						// 0 / Infinity
						return Zero;
					}

					if ((int)f1.RawValue >= 0)
					{
						// f / Infinity
						return PositiveInfinity;
					}
					else
					{
						// -f / Infinity
						return NegativeInfinity;
					}
				}
				else if (f2.RawValue == RawNegativeInfinity)
				{
					if (f1.IsZero())
					{
						// 0 / -Infinity
						return new floatP(SignMask);
					}

					if ((int)f1.RawValue < 0)
					{
						// -f / -Infinity
						return PositiveInfinity;
					}
					else
					{
						// f / -Infinity
						return NegativeInfinity;
					}
				}
				else
				{
					// NaN
					return f2;
				}
			}

			long longMan = ((long)man1 << MantissaBits) / (long)man2;
			int man = (int)longMan;
			//Debug.Assert(man != 0);
			uint absMan = (uint)Math.Abs(man);
			int rawExp = rawExp1 - rawExp2 + ExponentBias;
			uint sign = (uint)man & 0x80000000;

			if ((absMan & 0x800000) == 0)
			{
				absMan <<= 1;
				--rawExp;
			}

			//Debug.Assert(absMan >> MantissaBits == 1);
			if (rawExp >= 255)
			{
				// Overflow
				return new floatP(sign ^ RawPositiveInfinity);
			}

			if (rawExp <= 0)
			{
				// Subnorms/Underflow
				if (rawExp <= -24)
				{
					return new floatP(sign);
				}

				absMan >>= -rawExp + 1;
				rawExp = 0;
			}

			uint raw = sign | (uint)rawExp << MantissaBits | absMan & 0x7FFFFF;
			return new floatP(raw);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static floatP operator %(floatP f1, floatP f2) => MathfloatP.Mod(f1, f2);

		public override bool Equals(object obj) => obj != null && GetType() == obj.GetType() && Equals((floatP)obj);

		public bool Equals(floatP other)
		{
			if (RawExponent != 255)
			{
				// 0 == -0
				return (RawValue == other.RawValue) || ((RawValue & 0x7FFFFFFF) == 0) && ((other.RawValue & 0x7FFFFFFF) == 0);
			}
			else
			{
				if (RawMantissa == 0)
				{
					// Infinities
					return RawValue == other.RawValue;
				}
				else
				{
					// NaNs are equal for `Equals` (as opposed to the == operator)
					return other.RawMantissa != 0;
				}
			}
		}

		public override int GetHashCode()
		{
			if (RawValue == SignMask)
			{
				// +0 equals -0
				return 0;
			}

			if (!IsNaN())
			{
				return (int)RawValue;
			}
			else
			{
				// All NaNs are equal
				return unchecked((int)RawNaN);
			}
		}

		public static bool operator ==(floatP f1, floatP f2)
		{
			if (f1.RawExponent != 255)
			{
				// 0 == -0
				return (f1.RawValue == f2.RawValue) || ((f1.RawValue & 0x7FFFFFFF) == 0) && ((f2.RawValue & 0x7FFFFFFF) == 0);
			}
			else
			{
				if (f1.RawMantissa == 0)
				{
					// Infinities
					return f1.RawValue == f2.RawValue;
				}
				else
				{
					//NaNs
					return false;
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(floatP f1, floatP f2) => !(f1 == f2);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(floatP f1, floatP f2) => !f1.IsNaN() && !f2.IsNaN() && f1.CompareTo(f2) < 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(floatP f1, floatP f2) => !f1.IsNaN() && !f2.IsNaN() && f1.CompareTo(f2) > 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(floatP f1, floatP f2) => !f1.IsNaN() && !f2.IsNaN() && f1.CompareTo(f2) <= 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(floatP f1, floatP f2) => !f1.IsNaN() && !f2.IsNaN() && f1.CompareTo(f2) >= 0;

		public int CompareTo(floatP other)
		{
			if (IsNaN() && other.IsNaN())
			{
				return 0;
			}

			uint sign1 = (uint)((int)RawValue >> 31);
			int val1 = (int)(((RawValue) ^ (sign1 & 0x7FFFFFFF)) - sign1);

			uint sign2 = (uint)((int)other.RawValue >> 31);
			int val2 = (int)(((other.RawValue) ^ (sign2 & 0x7FFFFFFF)) - sign2);
			return val1.CompareTo(val2);
		}

		public int CompareTo(object obj) => obj is floatP f ? CompareTo(f) : throw new ArgumentException("obj");

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsInfinity() => (RawValue & 0x7FFFFFFF) == 0x7F800000;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsNegativeInfinity() => RawValue == RawNegativeInfinity;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsPositiveInfinity() => RawValue == RawPositiveInfinity;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsNaN() => (RawExponent == 255) && !IsInfinity();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsFinite() => RawExponent != 255;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsZero() => (RawValue & 0x7FFFFFFF) == 0;

		public static bool IsInfinity(floatP f)
		{
			return (f._raw & 0x7FFFFFFF) == 0x7F800000;
		}

		public static bool IsNegativeInfinity(floatP f)
		{
			return f._raw == RawNegativeInfinity;
		}

		public static bool IsNaN(floatP f)
		{
			return (f.RawExponent == 255) && !IsInfinity(f);
		}

		public static bool IsFinite(floatP f)
		{
			return f.RawExponent != 255;
		}

		public string ToString(string format, IFormatProvider formatProvider) => ((float)this).ToString(format, formatProvider);
		public string ToString(string format) => ((float)this).ToString(format);
		public string ToString(IFormatProvider provider) => ((float)this).ToString(provider);
		public string ToStringInv() => ((float)this).ToString(System.Globalization.CultureInfo.InvariantCulture);

		/// <summary>
		/// Returns true if the floatP number has a positive sign.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsPositive() => (RawValue & 0x80000000) == 0;

		/// <summary>
		/// Returns true if the floatP number has a negative sign.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsNegative() => (RawValue & 0x80000000) != 0;

		public int Sign()
		{
			if (IsNaN())
			{
				return 0;
			}

			if (IsZero())
			{
				return 0;
			}
			else if (IsPositive())
			{
				return 1;
			}
			else
			{
				return -1;
			}
		}

		public static int Sign(floatP value)
		{
			if (value.IsNaN())
			{
				throw new ArithmeticException("Sign doesn't support NaN argument");
			}
			if ((value.RawValue & 0x7FFFFFFF) == 0)
			{
				return 0;
			}
			else if ((int)value >= 0)
			{
				return 1;
			}
			else
			{
				return -1;
			}
		}

		public uint ToIeeeRaw()
		{
			return _raw;
		}

		public static floatP FromIeeeRaw(uint ieeeRaw)
		{
			return new floatP(ieeeRaw);
		}

		private static readonly uint[] debruijn32 = new uint[32]
		{
		0, 31, 9, 30, 3, 8, 13, 29, 2, 5, 7, 21, 12, 24, 28, 19,
		1, 10, 4, 14, 6, 22, 25, 20, 11, 15, 23, 26, 16, 27, 17, 18
		};

		/// <summary>
		/// Returns the leading zero count of the given 32-bit unsigned integer
		/// </summary>
		private static uint clz(uint x)
		{
			if (x == 0)
			{
				return 32;
			}

			x |= x >> 1;
			x |= x >> 2;
			x |= x >> 4;
			x |= x >> 8;
			x |= x >> 16;
			x++;

			return debruijn32[x * 0x076be629 >> 27];
		}

		private static floatP InternalAdd(floatP f1, floatP f2)
		{
			byte rawExp1 = f1.RawExponent;
			byte rawExp2 = f2.RawExponent;
			int deltaExp = rawExp1 - rawExp2;

			if (rawExp1 != 255)
			{
				//Finite
				if (deltaExp > 25)
				{
					return f1;
				}

				int man1;
				int man2;
				if (rawExp2 != 0)
				{
					// man1 = f1.Mantissa
					// http://graphics.stanford.edu/~seander/bithacks.html#ConditionalNegate
					uint sign1 = (uint)((int)f1.RawValue >> 31);
					man1 = (int)(((f1.RawMantissa | 0x800000) ^ sign1) - sign1);
					// man2 = f2.Mantissa
					uint sign2 = (uint)((int)f2.RawValue >> 31);
					man2 = (int)(((f2.RawMantissa | 0x800000) ^ sign2) - sign2);
				}
				else
				{
					// Subnorm
					// man2 = f2.Mantissa
					uint sign2 = (uint)((int)f2.RawValue >> 31);
					man2 = (int)((f2.RawMantissa ^ sign2) - sign2);

					man1 = f1.Mantissa;

					rawExp2 = 1;
					if (rawExp1 == 0)
					{
						rawExp1 = 1;
					}

					deltaExp = rawExp1 - rawExp2;
				}

				int man = (man1 << 6) + ((man2 << 6) >> deltaExp);
				uint absMan = (uint)Math.Abs(man);
				if (absMan == 0)
				{
					return Zero;
				}

				uint msb = absMan >> MantissaBits;
				int rawExp = rawExp1 - 6;
				while (msb == 0)
				{
					rawExp -= 8;
					absMan <<= 8;
					msb = absMan >> MantissaBits;
				}

				int msbIndex = BitScanReverse8(msb);
				rawExp += msbIndex;
				absMan >>= msbIndex;
				if ((uint)(rawExp - 1) < 254)
				{
					uint raw = (uint)man & 0x80000000 | (uint)rawExp << MantissaBits | (absMan & 0x7FFFFF);
					return new floatP(raw);
				}
				else
				{
					if (rawExp >= 255)
					{
						//Overflow
						return man >= 0 ? PositiveInfinity : NegativeInfinity;
					}

					if (rawExp >= -24)
					{
						uint raw = (uint)man & 0x80000000 | absMan >> (-rawExp + 1);
						return new floatP(raw);
					}

					return Zero;
				}
			}
			else
			{
				// Special

				if (rawExp2 != 255)
				{
					// f1 is NaN, +Inf, -Inf and f2 is finite
					return f1;
				}

				// Both not finite
				return f1.RawValue == f2.RawValue ? f1 : NaN;
			}
		}

		private static readonly sbyte[] msb = new sbyte[256]
		{
			-1, 0, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
			5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
			6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
			6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
			7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
			7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
			7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
			7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7
		};

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int BitScanReverse8(uint b) => msb[b];

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static unsafe uint ReinterpretFloatToInt32(float f) => *(uint*)&f;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static unsafe float ReinterpretIntToFloat32(uint i) => *(float*)&i;
	}
}
