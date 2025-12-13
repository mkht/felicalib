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

    /// <summary>
    /// felicalib.hで定義されているstrfelica構造体に対応するC#構造体
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct StrFelica
    {
        public IntPtr p;            // pasori ハンドル
        public ushort systemcode;   // システムコード
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] IDm;          // IDm
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] PMm;          // PMm

        // systemcode
        public byte num_system_code;            // 列挙システムコード数
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public ushort[] system_code;            // 列挙システムコード

        // area/service codes
        public byte num_area_code;              // エリアコード数
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public ushort[] area_code;              // エリアコード
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public ushort[] end_service_code;       // エンドサービスコード

        public byte num_service_code;           // サービスコード数
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] service_code;           // サービスコード
    }

	internal static partial class NativeMethods
	{
		private const string DllName = "felicalib.dll";

		[LibraryImport(DllName, StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial IntPtr pasori_open(string dummy);

		[LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void pasori_close(IntPtr p);

		[LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial int pasori_init(IntPtr p);

		[LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial IntPtr felica_polling(IntPtr p, ushort systemcode, byte RFU, byte timeslot);

		[LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial int felica_read_without_encryption02(IntPtr f, int servicecode, int mode, byte addr, [Out] byte[] data);

		[LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial int felica_write_without_encryption(IntPtr f, int servicecode, byte addr, byte[] data);

		[LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void felica_free(IntPtr f);

		[LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void felica_getidm(IntPtr f, [Out] byte[] buf);

		[LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial void felica_getpmm(IntPtr f, [Out] byte[] buf);

		[LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial IntPtr felica_enum_systemcode(IntPtr p);

		[LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        internal static partial IntPtr felica_enum_service(IntPtr p, ushort systemcode);
	}

	public class Felica : IDisposable
	{
		private IntPtr _pasori = IntPtr.Zero;
		private IntPtr _felica = IntPtr.Zero;
		private StrFelica? _felicaStructure = null;
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

            // felica構造体を取得
            _felicaStructure = Marshal.PtrToStructure<StrFelica>(_felica);
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
            ArgumentNullException.ThrowIfNull(data);
            if (data.Length > 16) throw new ArgumentException("data buffer must not exceed 16 bytes", nameof(data));
			if (_felica == IntPtr.Zero) throw new InvalidOperationException("no polling executed.");
			byte[] writeData = data;
			if (data.Length < 16)
			{
				writeData = new byte[16];
				Array.Copy(data, writeData, data.Length);
			}
			return NativeMethods.felica_write_without_encryption(_felica, servicecode, addr, writeData);
		}

		// felica_free
		private void FreeFelica()
		{
			if (_felica != IntPtr.Zero)
			{
				NativeMethods.felica_free(_felica);
				_felica = IntPtr.Zero;
				_felicaStructure = null;
			}
		}

		// felica_getidm
		public byte[] GetIdm()
		{
			if (_felica == IntPtr.Zero || !_felicaStructure.HasValue) throw new InvalidOperationException("no polling executed.");
			return _felicaStructure.Value.IDm;
		}

		// felica_getpmm
		public byte[] GetPmm()
		{
			if (_felica == IntPtr.Zero || !_felicaStructure.HasValue) throw new InvalidOperationException("no polling executed.");
			return _felicaStructure.Value.PMm;
		}

		// felica_enum_systemcode
		public ushort[] EnumSystemCode()
		{
			if (_pasori == IntPtr.Zero) throw new InvalidOperationException("pasori handle is not initialized.");

			// 既存の_felicaがあれば解放
			FreeFelica();

			IntPtr f = NativeMethods.felica_enum_systemcode(_pasori);
			if (f == IntPtr.Zero)
			{
				throw new InvalidOperationException("felica_enum_systemcode failed");
			}

			// 取得した felica 構造体を読み込み、インスタンスフィールドを更新
			var sf = Marshal.PtrToStructure<StrFelica>(f);
			_felica = f;
			_felicaStructure = sf;

			// num_system_code に基づいて配列を切り出して返す
			int count = sf.num_system_code;
			if (count <= 0) return [];
			if (sf.system_code == null) return [];

			ushort[] result = new ushort[count];
			Array.Copy(sf.system_code, result, Math.Min(count, sf.system_code.Length));
			return result;
		}

		// felica_enum_service
		public ushort[] EnumService(ushort systemcode)
		{
			if (_pasori == IntPtr.Zero) throw new InvalidOperationException("pasori handle is not initialized.");

			// 既存の_felicaがあれば解放
			FreeFelica();

			IntPtr f = NativeMethods.felica_enum_service(_pasori, systemcode);
			if (f == IntPtr.Zero)
			{
				throw new InvalidOperationException("felica_enum_service failed");
			}

			var sf = Marshal.PtrToStructure<StrFelica>(f);
			_felica = f;
			_felicaStructure = sf;

			int count = sf.num_service_code;
			if (count <= 0) return [];
			if (sf.service_code == null) return [];

			ushort[] result = new ushort[count];
			Array.Copy(sf.service_code, result, Math.Min(count, sf.service_code.Length));
			return result;
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