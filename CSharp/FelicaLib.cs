using System;
using System.Runtime.InteropServices;

namespace FelicaLib
{
    enum SystemCode : ushort
    {
        Any     = 0xffff,       // ANY
        Common  = 0xfe00,       // 共通領域
        Cyberne = 0x0003,       // サイバネ領域
        Edy     = 0xfe00,       // Edy (=共通領域)
        Suica   = 0x0003,       // Suica (=サイバネ領域)
        QUICPay = 0x04c1,       // QUICPay
    }

	internal static class NativeMethods
	{
		private const string DllName = "felicalib.dll";

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		internal static extern IntPtr pasori_open(string dummy);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void pasori_close(IntPtr p);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern int pasori_init(IntPtr p);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern IntPtr felica_polling(IntPtr p, ushort systemcode, byte RFU, byte timeslot);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern int felica_read_without_encryption02(IntPtr f, int servicecode, int mode, byte addr, [Out] byte[] data);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern int felica_write_without_encryption(IntPtr f, int servicecode, byte addr, byte[] data);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void felica_free(IntPtr f);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void felica_getidm(IntPtr f, [Out] byte[] buf);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void felica_getpmm(IntPtr f, [Out] byte[] buf);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern IntPtr felica_enum_systemcode(IntPtr p);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern IntPtr felica_enum_service(IntPtr p, ushort systemcode);
	}

	public class Felica : IDisposable
	{
		private IntPtr _pasori = IntPtr.Zero;
		private IntPtr _felica = IntPtr.Zero;
		private bool _disposed = false;

		// constructor
		public Felica()
		{
			_pasori = NativeMethods.pasori_open(null);
			if (_pasori == IntPtr.Zero)
			{
				throw new InvalidOperationException("pasori_open failed");
			}
            if (NativeMethods.pasori_init(_pasori) != 0)
            {
                PasoriClose();
                throw new InvalidOperationException("pasori_init failed");
            }
		}

		// pasori_close
		private void PasoriClose()
		{
			if (_pasori != IntPtr.Zero)
			{
				NativeMethods.pasori_close(_pasori);
				_pasori = IntPtr.Zero;
			}
		}

		// felica_polling
        public void Polling(ushort systemcode)
        {
            FreeFelica();
            _felica = NativeMethods.felica_polling(_pasori, systemcode, 0, 0);
            if (_felica == IntPtr.Zero)
            {
                throw new InvalidOperationException("felica_polling failed");
            }
        }

		// felica_read_without_encryption02
		public byte[] ReadWithoutEncryption(int servicecode, byte addr)
		{
			if (_felica == IntPtr.Zero) throw new InvalidOperationException("no polling executed.");
			var data = new byte[16];
			if (NativeMethods.felica_read_without_encryption02(_felica, servicecode, 0, addr, data) != 0)
			{
				return null;
			}
			return data;
		}

		// felica_write_without_encryption
		public int WriteWithoutEncryption(int servicecode, byte addr, byte[] data)
		{
			if (_felica == IntPtr.Zero) throw new InvalidOperationException("no polling executed.");
			if (data == null || data.Length < 16) throw new ArgumentException("data buffer must be at least 16 bytes", nameof(data));
			return NativeMethods.felica_write_without_encryption(_felica, servicecode, addr, data);
		}

		// felica_free
		private void FreeFelica()
		{
			if (_felica != IntPtr.Zero)
			{
				NativeMethods.felica_free(_felica);
				_felica = IntPtr.Zero;
			}
		}

		// felica_getidm
		public byte[] GetIdm()
		{
			if (_felica == IntPtr.Zero) throw new InvalidOperationException("no polling executed.");
			var buf = new byte[8];
			NativeMethods.felica_getidm(_felica, buf);
			return buf;
		}

		// felica_getpmm
		public byte[] GetPmm()
		{
			if (_felica == IntPtr.Zero) throw new InvalidOperationException("no polling executed.");
			var buf = new byte[8];
			NativeMethods.felica_getpmm(_felica, buf);
			return buf;
		}

		// felica_enum_systemcode
		public static IntPtr EnumSystemCode(IntPtr pasoriHandle)
		{
			if (pasoriHandle == IntPtr.Zero) throw new ArgumentNullException(nameof(pasoriHandle));
			return NativeMethods.felica_enum_systemcode(pasoriHandle);
		}

		// felica_enum_service
		public static IntPtr EnumService(IntPtr pasoriHandle, ushort systemcode)
		{
			if (pasoriHandle == IntPtr.Zero) throw new ArgumentNullException(nameof(pasoriHandle));
			return NativeMethods.felica_enum_service(pasoriHandle, systemcode);
		}

		// IDisposable
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (_disposed) return;

			// マネージドリソースの解除（無し）
			// アンマネージドリソースの解除
			FreeFelica();
			if (_pasori != IntPtr.Zero)
			{
				try
				{
					NativeMethods.pasori_close(_pasori);
				}
				catch { /* 無視 */ }
				_pasori = IntPtr.Zero;
			}

			_disposed = true;
		}

		~Felica()
		{
			Dispose(false);
		}
	}
}