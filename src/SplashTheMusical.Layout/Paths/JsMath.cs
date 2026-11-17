namespace SplashTheMusical.Layout.Paths;

/// <summary>
/// The <c>Math</c> functions of V8 whose results differ from .NET's. V8 computes the trigonometric functions with its
/// port of fdlibm in <c>src/base/ieee754.cc</c>, which these follow, so a value mermaid prints in full matches to the
/// last digit. The oracle runs V8 on macOS arm64, where clang compiles that file with <c>-ffp-contract=on</c> and
/// turns each <c>a + b * c</c> inside one expression into a fused multiply-add, taking the left product first. The
/// ports call <see cref="Fma"/> where clang fuses.
/// </summary>
internal static class JsMath
{
	private static double Fma(double a, double b, double c) => Math.FusedMultiplyAdd(a, b, c);

	/// <summary><c>Math.round</c>: the nearest integer, with halves rounded up.</summary>
	public static double Round(double value)
	{
		if (!double.IsFinite(value))
		{
			return value;
		}

		var floor = Math.Floor(value);
		return value - floor >= 0.5 ? floor + 1 : floor;
	}

	/// <summary><c>Math.hypot</c> of two values, with V8's scaling and Kahan summation.</summary>
	public static double Hypot(double x, double y)
	{
		var a = Math.Abs(x);
		var b = Math.Abs(y);
		var max = Math.Max(a, b);
		if (double.IsPositiveInfinity(a) || double.IsPositiveInfinity(b))
		{
			return double.PositiveInfinity;
		}

		if (double.IsNaN(a) || double.IsNaN(b))
		{
			return double.NaN;
		}

		if (max == 0)
		{
			return 0;
		}

		var sum = 0.0;
		var compensation = 0.0;
		foreach (var value in (ReadOnlySpan<double>)[a, b])
		{
			var n = value / max;
			var summand = n * n - compensation;
			var preliminary = sum + summand;
			compensation = preliminary - sum - summand;
			sum = preliminary;
		}

		return Math.Sqrt(sum) * max;
	}

	private const double Pi = 3.14159265358979311600e+00;
	private const double PiOver2Hi = 1.57079632679489655800e+00;
	private const double PiOver2Lo = 6.12323399573676603587e-17;
	private const double PiOver4 = 7.8539816339744827900e-01;
	private const double PiLo = 1.2246467991473531772e-16;

	// The rational approximation of acos: p is z*(pS0+z*(pS1+...)), q is one+z*(qS1+...).
	private static double AcosP(double z)
	{
		const double pS0 = 1.66666666666666657415e-01;
		const double pS1 = -3.25565818622400915405e-01;
		const double pS2 = 2.01212532134862925881e-01;
		const double pS3 = -4.00555345006794114027e-02;
		const double pS4 = 7.91534994289814532176e-04;
		const double pS5 = 3.47933107596021167570e-05;
		return z * Fma(z, Fma(z, Fma(z, Fma(z, Fma(z, pS5, pS4), pS3), pS2), pS1), pS0);
	}

	private static double AcosQ(double z)
	{
		const double qS1 = -2.40339491173441421878e+00;
		const double qS2 = 2.02094576023350569471e+00;
		const double qS3 = -6.88283971605453293030e-01;
		const double qS4 = 7.70381505559019352791e-02;
		return Fma(z, Fma(z, Fma(z, Fma(z, qS4, qS3), qS2), qS1), 1.0);
	}

	/// <summary><c>Math.acos</c>, fdlibm's <c>__ieee754_acos</c>.</summary>
	public static double Acos(double x)
	{
		var hx = High(x);
		var ix = hx & 0x7fffffff;
		if (ix >= 0x3ff00000)
		{
			if (((ix - 0x3ff00000) | (int)Low(x)) == 0)
			{
				return hx > 0 ? 0.0 : Pi + 2.0 * PiOver2Lo;
			}

			return double.NaN;
		}

		if (ix < 0x3fe00000)
		{
			if (ix <= 0x3c600000)
			{
				return PiOver2Hi + PiOver2Lo;
			}

			var z = x * x;
			var r = AcosP(z) / AcosQ(z);
			return PiOver2Hi - (x - Fma(-x, r, PiOver2Lo));
		}

		if (hx < 0)
		{
			var z = (1.0 + x) * 0.5;
			var s = Math.Sqrt(z);
			var r = AcosP(z) / AcosQ(z);
			var w = Fma(r, s, -PiOver2Lo);
			return Fma(-2.0, s + w, Pi);
		}
		else
		{
			var z = (1.0 - x) * 0.5;
			var s = Math.Sqrt(z);
			var df = WithLow(s, 0);
			var c = Fma(-df, df, z) / (s + df);
			var r = AcosP(z) / AcosQ(z);
			var w = Fma(r, s, c);
			return 2.0 * (df + w);
		}
	}

	private static ReadOnlySpan<double> AtanHi => [4.63647609000806093515e-01, 7.85398163397448278999e-01, 9.82793723247329054082e-01, 1.57079632679489655800e+00];

	private static ReadOnlySpan<double> AtanLo => [2.26987774529616870924e-17, 3.06161699786838301793e-17, 1.39033110312309984516e-17, 6.12323399573676603587e-17];

	private static ReadOnlySpan<double> AT =>
	[
		3.33333333333329318027e-01,
		-1.99999999998764832476e-01,
		1.42857142725034663711e-01,
		-1.11111104054623557880e-01,
		9.09088713343650656196e-02,
		-7.69187620504482999495e-02,
		6.66107313738753120669e-02,
		-5.83357013379057348645e-02,
		4.97687799461593236017e-02,
		-3.65315727442169155270e-02,
		1.62858201153657823623e-02,
	];

	/// <summary><c>Math.atan</c>, fdlibm's <c>atan</c>.</summary>
	public static double Atan(double x)
	{
		var hx = High(x);
		var ix = hx & 0x7fffffff;
		int id;
		if (ix >= 0x44100000)
		{
			if (double.IsNaN(x))
			{
				return x + x;
			}

			return hx > 0 ? AtanHi[3] + AtanLo[3] : -AtanHi[3] - AtanLo[3];
		}

		if (ix < 0x3fdc0000)
		{
			if (ix < 0x3e400000)
			{
				return x;
			}

			id = -1;
		}
		else
		{
			x = Math.Abs(x);
			if (ix < 0x3ff30000)
			{
				if (ix < 0x3fe60000)
				{
					id = 0;
					x = Fma(2.0, x, -1.0) / (2.0 + x);
				}
				else
				{
					id = 1;
					x = (x - 1.0) / (x + 1.0);
				}
			}
			else if (ix < 0x40038000)
			{
				id = 2;
				x = (x - 1.5) / Fma(1.5, x, 1.0);
			}
			else
			{
				id = 3;
				x = -1.0 / x;
			}
		}

		var z = x * x;
		var w = z * z;
		var s1 = z * Fma(w, Fma(w, Fma(w, Fma(w, Fma(w, AT[10], AT[8]), AT[6]), AT[4]), AT[2]), AT[0]);
		var s2 = w * Fma(w, Fma(w, Fma(w, Fma(w, AT[9], AT[7]), AT[5]), AT[3]), AT[1]);
		if (id < 0)
		{
			return Fma(-x, s1 + s2, x);
		}

		z = AtanHi[id] - (Fma(x, s1 + s2, -AtanLo[id]) - x);
		return hx < 0 ? -z : z;
	}

	/// <summary><c>Math.atan2</c>, fdlibm's <c>__ieee754_atan2</c> as FreeBSD's msun and V8 have it.</summary>
	public static double Atan2(double y, double x)
	{
		const double tiny = 1.0e-300;
		if (double.IsNaN(x) || double.IsNaN(y))
		{
			return x + y;
		}

		var hx = High(x);
		var ix = hx & 0x7fffffff;
		var lx = Low(x);
		var hy = High(y);
		var iy = hy & 0x7fffffff;
		var ly = Low(y);
		if (((hx - 0x3ff00000) | (int)lx) == 0)
		{
			return Atan(y);
		}

		var m = ((hy >> 31) & 1) | ((hx >> 30) & 2);
		if ((iy | (int)ly) == 0)
		{
			switch (m)
			{
				case 0:
				case 1:
					return y;
				case 2:
					return Pi + tiny;
				default:
					return -Pi - tiny;
			}
		}

		if ((ix | (int)lx) == 0)
		{
			return hy < 0 ? -PiOver2Hi - tiny : PiOver2Hi + tiny;
		}

		if (ix == 0x7ff00000)
		{
			if (iy == 0x7ff00000)
			{
				return m switch
				{
					0 => PiOver4 + tiny,
					1 => -PiOver4 - tiny,
					2 => 3.0 * PiOver4 + tiny,
					_ => -3.0 * PiOver4 - tiny,
				};
			}

			return m switch
			{
				0 => 0.0,
				1 => -0.0,
				2 => Pi + tiny,
				_ => -Pi - tiny,
			};
		}

		if (iy == 0x7ff00000)
		{
			return hy < 0 ? -PiOver2Hi - tiny : PiOver2Hi + tiny;
		}

		var k = (iy - ix) >> 20;
		double z;
		if (k > 60)
		{
			z = PiOver2Hi + 0.5 * PiLo;
			m &= 1;
		}
		else if (hx < 0 && k < -60)
		{
			z = 0.0;
		}
		else
		{
			z = Atan(Math.Abs(y / x));
		}

		return m switch
		{
			0 => z,
			1 => -z,
			2 => Pi - (z - PiLo),
			_ => (z - PiLo) - Pi,
		};
	}

	/// <summary><c>Math.sin</c>, fdlibm's <c>sin</c>, for arguments up to 2^19 times pi/2.</summary>
	public static double Sin(double x)
	{
		var ix = High(x) & 0x7fffffff;
		if (ix <= 0x3fe921fb)
		{
			return KernelSin(x, 0, 0);
		}

		if (ix >= 0x7ff00000)
		{
			return double.NaN;
		}

		if (!TryReduce(x, out var y0, out var y1, out var n))
		{
			return Math.Sin(x);
		}

		return (n & 3) switch
		{
			0 => KernelSin(y0, y1, 1),
			1 => KernelCos(y0, y1),
			2 => -KernelSin(y0, y1, 1),
			_ => -KernelCos(y0, y1),
		};
	}

	/// <summary><c>Math.cos</c>, fdlibm's <c>cos</c>, for arguments up to 2^19 times pi/2.</summary>
	public static double Cos(double x)
	{
		var ix = High(x) & 0x7fffffff;
		if (ix <= 0x3fe921fb)
		{
			return KernelCos(x, 0);
		}

		if (ix >= 0x7ff00000)
		{
			return double.NaN;
		}

		if (!TryReduce(x, out var y0, out var y1, out var n))
		{
			return Math.Cos(x);
		}

		return (n & 3) switch
		{
			0 => KernelCos(y0, y1),
			1 => -KernelSin(y0, y1, 1),
			2 => -KernelCos(y0, y1),
			_ => KernelSin(y0, y1, 1),
		};
	}

	private static double KernelSin(double x, double y, int iy)
	{
		const double s1 = -1.66666666666666324348e-01;
		const double s2 = 8.33333333332248946124e-03;
		const double s3 = -1.98412698298579493134e-04;
		const double s4 = 2.75573137070700676789e-06;
		const double s5 = -2.50507602534068634195e-08;
		const double s6 = 1.58969099521155010221e-10;

		var ix = High(x) & 0x7fffffff;
		if (ix < 0x3e400000 && (int)x == 0)
		{
			return x;
		}

		var z = x * x;
		var v = z * x;
		var r = Fma(z, Fma(z, Fma(z, Fma(z, s6, s5), s4), s3), s2);
		return iy == 0 ? Fma(v, Fma(z, r, s1), x) : x - Fma(-v, s1, Fma(z, Fma(0.5, y, -(v * r)), -y));
	}

	private static double KernelCos(double x, double y)
	{
		const double c1 = 4.16666666666666019037e-02;
		const double c2 = -1.38888888888741095749e-03;
		const double c3 = 2.48015872894767294178e-05;
		const double c4 = -2.75573143513906633035e-07;
		const double c5 = 2.08757232129817482790e-09;
		const double c6 = -1.13596475577881948265e-11;

		var ix = High(x) & 0x7fffffff;
		if (ix < 0x3e400000 && (int)x == 0)
		{
			return 1.0;
		}

		var z = x * x;
		var r = z * Fma(z, Fma(z, Fma(z, Fma(z, Fma(z, c6, c5), c4), c3), c2), c1);
		if (ix < 0x3fd33333)
		{
			return 1.0 - Fma(0.5, z, -Fma(z, r, -(x * y)));
		}

		var qx = ix > 0x3fe90000 ? 0.28125 : FromWords(ix - 0x00200000, 0);
		var hz = Fma(0.5, z, -qx);
		var a = 1.0 - qx;
		return a - (hz - Fma(z, r, -(x * y)));
	}

	// The high words of n times pi/2 for n from 1 to 32.
	private static ReadOnlySpan<int> NPiOver2High =>
	[
		0x3FF921FB, 0x400921FB, 0x4012D97C, 0x401921FB, 0x401F6A7A, 0x4022D97C, 0x4025FDBB, 0x402921FB,
		0x402C463A, 0x402F6A7A, 0x4031475C, 0x4032D97C, 0x40346B9C, 0x4035FDBB, 0x40378FDB, 0x403921FB,
		0x403AB41B, 0x403C463A, 0x403DD85A, 0x403F6A7A, 0x40407E4C, 0x4041475C, 0x4042106C, 0x4042D97C,
		0x4043A28C, 0x40446B9C, 0x404534AC, 0x4045FDBB, 0x4046C6CB, 0x40478FDB, 0x404858EB, 0x404921FB,
	];

	// fdlibm's __ieee754_rem_pio2 up to its medium size, which gives x minus n times pi/2 as y0 + y1. Larger arguments
	// need __kernel_rem_pio2, which is not ported, and return false.
	private static bool TryReduce(double x, out double y0, out double y1, out int n)
	{
		const double invpio2 = 6.36619772367581382433e-01;
		const double pio2_1 = 1.57079632673412561417e+00;
		const double pio2_1t = 6.07710050650619224932e-11;
		const double pio2_2 = 6.07710050630396597660e-11;
		const double pio2_2t = 2.02226624879595063154e-21;
		const double pio2_3 = 2.02226624871116645580e-21;
		const double pio2_3t = 8.47842766036889956997e-32;

		var hx = High(x);
		var ix = hx & 0x7fffffff;
		if (ix < 0x4002d97c)
		{
			double z;
			if (hx > 0)
			{
				z = x - pio2_1;
				if (ix != 0x3ff921fb)
				{
					y0 = z - pio2_1t;
					y1 = (z - y0) - pio2_1t;
				}
				else
				{
					z -= pio2_2;
					y0 = z - pio2_2t;
					y1 = (z - y0) - pio2_2t;
				}

				n = 1;
				return true;
			}

			z = x + pio2_1;
			if (ix != 0x3ff921fb)
			{
				y0 = z + pio2_1t;
				y1 = (z - y0) + pio2_1t;
			}
			else
			{
				z += pio2_2;
				y0 = z + pio2_2t;
				y1 = (z - y0) + pio2_2t;
			}

			n = -1;
			return true;
		}

		if (ix > 0x413921fb)
		{
			y0 = y1 = 0;
			n = 0;
			return false;
		}

		var t = Math.Abs(x);
		n = (int)Fma(t, invpio2, 0.5);
		var fn = (double)n;
		var r = Fma(-fn, pio2_1, t);
		var w = fn * pio2_1t;
		if (n < 32 && ix != NPiOver2High[n - 1])
		{
			y0 = r - w;
		}
		else
		{
			var j = ix >> 20;
			y0 = r - w;
			var i = j - ((High(y0) >> 20) & 0x7ff);
			if (i > 16)
			{
				t = r;
				w = fn * pio2_2;
				r = t - w;
				w = Fma(fn, pio2_2t, -((t - r) - w));
				y0 = r - w;
				i = j - ((High(y0) >> 20) & 0x7ff);
				if (i > 49)
				{
					t = r;
					w = fn * pio2_3;
					r = t - w;
					w = Fma(fn, pio2_3t, -((t - r) - w));
					y0 = r - w;
				}
			}
		}

		y1 = (r - y0) - w;
		if (hx < 0)
		{
			y0 = -y0;
			y1 = -y1;
			n = -n;
		}

		return true;
	}

	private static int High(double x) => (int)(BitConverter.DoubleToInt64Bits(x) >> 32);

	private static uint Low(double x) => (uint)BitConverter.DoubleToInt64Bits(x);

	private static double WithLow(double x, uint low) => BitConverter.Int64BitsToDouble((BitConverter.DoubleToInt64Bits(x) & unchecked((long)0xFFFFFFFF00000000)) | low);

	private static double FromWords(int high, uint low) => BitConverter.Int64BitsToDouble(((long)high << 32) | low);
}
