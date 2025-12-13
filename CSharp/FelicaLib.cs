/*
 felicalib - FeliCa access wrapper library

 Copyright (c) 2007-2010, Takuya Murakami, All rights reserved.

 Redistribution and use in source and binary forms, with or without
 modification, are permitted provided that the following conditions are
 met:

 1. Redistributions of source code must retain the above copyright notice,
    this list of conditions and the following disclaimer. 

 2. Redistributions in binary form must reproduce the above copyright
    notice, this list of conditions and the following disclaimer in the
    documentation and/or other materials provided with the distribution. 

 3. Neither the name of the project nor the names of its contributors
    may be used to endorse or promote products derived from this software
    without specific prior written permission. 

 THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

///
/// Porting to x64 systems by DeForest(Hirokazu Hayashi)
///

/// 
/// Re-written to .NET 8 Class Library by mkht
///

using System;
using System.Runtime.InteropServices;

namespace FelicaLib
{
    // システムコード
    enum SystemCode : ushort
    {
        Any = 0xffff,       // ANY
        Common = 0xfe00,       // 共通領域
        Cyberne = 0x0003,       // サイバネ領域
        Edy = 0xfe00,       // Edy (=共通領域)
        Suica = 0x0003,       // Suica (=サイバネ領域)
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

    // ネイティブメソッド P/Invoke 宣言
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

    // Felicaクラス
    public class Felica : IDisposable
    {
        private IntPtr _pasori = IntPtr.Zero;
        private IntPtr _felica = IntPtr.Zero;
        private StrFelica? _felicaStructure = null;
        private bool _disposed = false;

        public byte[]? IDm { get; private set; }
        public byte[]? PMm { get; private set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public Felica()
        {
            _pasori = NativeMethods.pasori_open(null);
            if (_pasori == IntPtr.Zero)
            {
                throw new InvalidOperationException("PaSori open failed.");
            }
            if (NativeMethods.pasori_init(_pasori) != 0)
            {
                PasoriClose();
                throw new InvalidOperationException("Could not connect to PaSori.");
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

        /// <summary>
        /// ポーリング, Felica基本情報取得
        /// </summary>
        /// <param name="systemcode">システムコード</param>
        public void Polling(ushort systemcode)
        {
            FreeFelica();
            _felica = NativeMethods.felica_polling(_pasori, systemcode, 0, 0);
            if (_felica == IntPtr.Zero)
            {
                throw new InvalidOperationException("Polling card failed.");
            }

            // felica構造体を取得
            _felicaStructure = Marshal.PtrToStructure<StrFelica>(_felica);
            IDm = _felicaStructure.Value.IDm;
            PMm = _felicaStructure.Value.PMm;
        }

        /// オーバーロード: システムコード ANY 指定
        public void Polling()
        {
            Polling((ushort)SystemCode.Any);
        }

        /// <summary>
        /// 非暗号化領域読み込み
        /// </summary>
        /// <param name="servicecode">サービスコード</param>
        /// <param name="addr">ブロックアドレス</param>
        /// <returns>読み取りデータ(16バイト) or null (失敗時)</returns>
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

        /// <summary>
        /// 非暗号化領域書き込み
        /// </summary>
        /// <param name="servicecode">サービスコード</param>
        /// <param name="addr">ブロックアドレス</param>
        /// <param name="data">書き込みデータ(最大16バイト)</param>
        /// <returns>書き込みに成功した場合は0</returns>
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

        // Felica解放
        private void FreeFelica()
        {
            if (_felica != IntPtr.Zero)
            {
                NativeMethods.felica_free(_felica);
                _felica = IntPtr.Zero;
                _felicaStructure = null;
                IDm = null;
                PMm = null;
            }
        }

        /// <summary>
        /// IDm取得
        /// </summary>
        /// <returns>IDmバイナリデータ</returns>
        public byte[] GetIdm()
        {
            if (_felica == IntPtr.Zero || !_felicaStructure.HasValue) throw new InvalidOperationException("no polling executed.");
            IDm = _felicaStructure.Value.IDm;
            return IDm;
        }

        /// <summary>
        /// PMm取得
        /// </summary>
        /// <returns>PMmバイナリデータ</returns
        public byte[] GetPMm()
        {
            if (_felica == IntPtr.Zero || !_felicaStructure.HasValue) throw new InvalidOperationException("no polling executed.");
            PMm = _felicaStructure.Value.PMm;
            return PMm;
        }

        /// <summary>
        /// システムコード列挙
        /// </summary>
        /// <returns>システムコード配列</returns>
        public ushort[] EnumSystemCode()
        {
            FreeFelica();
            IntPtr f = NativeMethods.felica_enum_systemcode(_pasori);
            if (f == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to enumerate system codes.");
            }

            // 取得した felica 構造体を読み込み、インスタンスフィールドを更新
            var sf = Marshal.PtrToStructure<StrFelica>(f);
            _felica = f;
            _felicaStructure = sf;
            // IDmがすべて0の場合、サービスコードが取得できなかったと判断
            if (sf.IDm.All(b => b == 0))
            {
                FreeFelica();
                throw new InvalidOperationException("Failed to retrieve system codes.");
            }

            // num_system_code に基づいて配列を切り出して返す
            int count = sf.num_system_code;
            if (count > 8)
            {
                throw new InvalidOperationException("Failed to retrieve system codes.");
            }
            if (count <= 0) return [];
            if (sf.system_code == null) return [];
            ushort[] result = new ushort[count];
            // felica_enum_systemcode で取得した system_code はバイトオーダーが逆なので変換する
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (ushort)((sf.system_code[i] << 8) | (sf.system_code[i] >> 8));
            }
            IDm = sf.IDm;
            PMm = sf.PMm;
            return result;
        }

        /// <summary>
        /// サービスコード列挙
        /// </summary>
        /// <param name="systemcode">システムコード</param>
        /// <returns>サービスコード配列</returns>
        public ushort[] EnumServiceCode(ushort systemcode)
        {
            FreeFelica();
            IntPtr f = NativeMethods.felica_enum_service(_pasori, systemcode);
            if (f == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to enumerate service codes.");
            }

            var sf = Marshal.PtrToStructure<StrFelica>(f);
            _felica = f;
            _felicaStructure = sf;
            // IDmがすべて0の場合、サービスコードが取得できなかったと判断
            if (sf.IDm.All(b => b == 0))
            {
                FreeFelica();
                throw new InvalidOperationException("Failed to retrieve service codes.");
            }

            int count = sf.num_service_code;
            if (count > 256)
            {
                throw new InvalidOperationException("Failed to retrieve service codes.");
            }
            if (count <= 0) return [];
            if (sf.service_code == null) return [];

            ushort[] result = new ushort[count];
            Array.Copy(sf.service_code, result, Math.Min(count, sf.service_code.Length));

            IDm = sf.IDm;
            PMm = sf.PMm;
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

        // デストラクタ
        ~Felica()
        {
            Dispose(false);
        }
    }
}