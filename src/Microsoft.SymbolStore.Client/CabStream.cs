using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Microsoft.SymbolStore.Client
{
    class CabConverter
    {
        private MemoryStream _output = new MemoryStream();
        private MemoryStream _input;
        private IntPtr _context;
        private Stream _stream;
        private FileOpenDelegate _open;
        private FileCloseDelegate _close;
        private FileSeekDelegate _seek;
        private FileReadDelegate _read;
        private FileWriteDelegate _write;
        private MemAllocDelegate _alloc;
        private MemFreeDelegate _free;
        private Error _error = new Error();
        private IntPtr _outputHandle;

        public CabConverter(Stream stream)
        {
            _stream = stream;
            
            _open = Open;
            _close = Close;
            _seek = Seek;
            _read = Read;
            _write = Write;
            _alloc = Alloc;
            _free = Free;

        }

        public MemoryStream Convert()
        {
            if (_stream is MemoryStream)
            {
                _input = (MemoryStream)_stream;
            }
            else
            {
                _input = new MemoryStream();
                _stream.CopyTo(_input);
            }

            _input.Position = 0;

            Begin();
            _output.Position = 0;
            return _output;
        }

        public async Task<MemoryStream> ConvertAsync()
        {
            if (_stream is MemoryStream)
            {
                _input = (MemoryStream)_stream;
            }
            else
            {
                _input = new MemoryStream();
                await _stream.CopyToAsync(_input);
            }

            _input.Position = 0;

            Task task = new Task(() => Begin());
            task.Start();

            await task;
            _output.Position = 0;
            return _output;
        }

        public void Begin()
        {
            _context = Create(_alloc, _free, _open, _read, _write, _close, _seek, -1, _error);
            Copy(_context, "", "", 0, Notify, IntPtr.Zero, IntPtr.Zero);
            Destroy(_context);
        }

        private IntPtr CreateStreamPointer(MemoryStream ms)
        {
            return (IntPtr)GCHandle.Alloc(ms);
        }
        
        private MemoryStream PointerToStream(IntPtr ptr)
        {
            GCHandle handle = (GCHandle)ptr;
            return (MemoryStream)handle.Target;
        }

        private void FreeStreamPointer(IntPtr ptr)
        {
            GCHandle handle = (GCHandle)ptr;
            handle.Free();
        }

        string ToHex(IntPtr ptr)
        {
            return ptr.ToString("x");
        }

        private IntPtr Notify(NotificationType type, Notification notification)
        {
            if (type != NotificationType.CopyFile)
                return IntPtr.Zero;

            switch (type)
            {
                case NotificationType.CopyFile:
                    _outputHandle = CreateStreamPointer(_output);
                    Debug.WriteLine($"Notify({type}, {notification}) - {ToHex(_outputHandle)}");
                    return _outputHandle;

                case NotificationType.CloseFileInfo:
                    FreeStreamPointer(_outputHandle);
                    Debug.WriteLine($"Notify({type}, {notification}) - 0");
                    return IntPtr.Zero;

                default:
                    Debug.WriteLine($"Notify({type}, {notification}) - 0");
                    return IntPtr.Zero;
            }

        }

        private IntPtr Open(string fileName, int oflag, int pmode)
        {
            IntPtr result = CreateStreamPointer(_input);

            Debug.WriteLine($"Open({fileName}, {oflag:x}, {pmode:x}) - {ToHex(result)}");
            return result;
        }

        private int Close(IntPtr ptr)
        {
            FreeStreamPointer(ptr);

            Debug.WriteLine($"Close({ToHex(ptr)}) - 0");
            return 0;
        }

        private int Seek(IntPtr ptr, int offset, int seektype)
        {
            MemoryStream stream = PointerToStream(ptr);
            int result = (int)stream.Seek(offset, (SeekOrigin)seektype);

            Debug.WriteLine($"Seek({ToHex(ptr)}, {offset:x}, {seektype:x}) - {result:x}");
            return result;
        }
        
        private int Read(IntPtr ptr, byte[] buffer, int count)
        {
            MemoryStream stream = PointerToStream(ptr);
            Debug.Assert(stream == _input);

            int result = stream.Read(buffer, 0, count);
            Debug.WriteLine($"Read({ToHex(ptr)}, {count:x}) - {result:x}");
            return result;
        }

        private int Write(IntPtr ptr, byte[] buffer, int count)
        {
            MemoryStream stream = PointerToStream(ptr);
            Debug.Assert(stream == _output);

            stream.Write(buffer, 0, count);
            Debug.WriteLine($"Write({ToHex(ptr)}, {count:x}) - {count:x}");
            return count;
        }

        private IntPtr Alloc(int count)
        {
            IntPtr result = Marshal.AllocHGlobal(count);

            Debug.WriteLine($"Alloc({count:x}) - {ToHex(result)}");
            return result;
        }

        private void Free(IntPtr ptr)
        {
            Debug.WriteLine($"Free({ToHex(ptr)})");
            Marshal.FreeHGlobal(ptr);
        }

        #region External Functions
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate IntPtr MemAllocDelegate(int count);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void MemFreeDelegate(IntPtr mem);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate IntPtr FileOpenDelegate(string fileName, int oflag, int pmode);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate Int32 FileReadDelegate(IntPtr hf, [In, Out] [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2, ArraySubType = UnmanagedType.U1)] byte[] buffer, int cb);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate Int32 FileWriteDelegate(IntPtr hf, [In] [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2, ArraySubType = UnmanagedType.U1)] byte[] buffer, int cb);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate Int32 FileCloseDelegate(IntPtr hf);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate Int32 FileSeekDelegate(IntPtr hf, int dist, int seektype);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate IntPtr NotifyDelegate(NotificationType fdint, [In] [MarshalAs(UnmanagedType.LPStruct)] Notification fdin);

        [DllImport("cabinet.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "FDICreate", CharSet = CharSet.Ansi)]
        static extern IntPtr Create(MemAllocDelegate fnMemAlloc, MemFreeDelegate fnMemFree, FileOpenDelegate fnFileOpen, FileReadDelegate fnFileRead,
            FileWriteDelegate fnFileWrite, FileCloseDelegate fnFileClose, FileSeekDelegate fnFileSeek, int cpu, [MarshalAs(UnmanagedType.LPStruct)] Error error);

        [DllImport("cabinet.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "FDIDestroy", CharSet = CharSet.Ansi)]
        static extern bool Destroy(IntPtr hfdi);

        [DllImport("cabinet.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "FDICopy", CharSet = CharSet.Ansi)]
        static extern bool Copy(IntPtr context, string unused1, string unused2, int flags, NotifyDelegate notify, IntPtr decrypt, IntPtr userData);


        [StructLayout(LayoutKind.Sequential)]
        class Error
        {
            public int erfOper;
            public int erfType;
            public int fError;
        }

        [StructLayout(LayoutKind.Sequential)]
        class Notification
        {
            int cb;
            IntPtr psz1;
            IntPtr psz2;
            IntPtr psz3;
            IntPtr pv;
            IntPtr hf;
            short date;
            short time;
            short attribs;
            short setID;
            short iCabinet;
            short iFolder;
            int fdie;
        }

        enum NotificationType
        {
            CabinetInfo,
            PartialFile,
            CopyFile,
            CloseFileInfo,
            NextCabinet,
            Enumerate
        }
        #endregion
    }
}
