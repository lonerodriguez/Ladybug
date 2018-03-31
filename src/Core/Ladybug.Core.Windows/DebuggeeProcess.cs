﻿using System;
using System.Collections.Generic;
using System.Linq;
using Ladybug.Core.Windows.Kernel32;

namespace Ladybug.Core.Windows
{
    public class DebuggeeProcess : IDebuggeeProcess
    {
        public event EventHandler<DebuggeeThreadEventArgs> ThreadStarted;
        public event EventHandler<DebuggeeThreadEventArgs> ThreadTerminated;

        private readonly IDictionary<int, IDebuggeeThread> _threads = new Dictionary<int, IDebuggeeThread>();
        private readonly IDictionary<IntPtr, IDebuggeeLibrary> _libraries = new Dictionary<IntPtr, IDebuggeeLibrary>();
        private readonly IDictionary<IntPtr, Int3Breakpoint> _breakpoints = new Dictionary<IntPtr, Int3Breakpoint>();
        
        private readonly IntPtr _processHandle;

        internal DebuggeeProcess(IDebuggerSession session, PROCESS_INFORMATION processInfo)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            _processHandle = processInfo.hProcess;
            Id = processInfo.dwProcessId;
        }

        internal DebuggeeProcess(IDebuggerSession session, IntPtr handle, int id)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            _processHandle = handle;
            Id = id;
        }

        public IDebuggerSession Session
        {
            get;
        }

        public int Id
        {
            get;
        }

        public ICollection<IDebuggeeThread> Threads
        {
            get { return _threads.Values; }
        }

        public ICollection<IDebuggeeLibrary> Libraries
        {
            get { return _libraries.Values; }
        }

        public int ExitCode
        {
            get;
            internal set;
        }

        public IntPtr BaseAddress
        {
            get;
            internal set;
        }

        public DebuggeeThread GetThreadById(int id)
        {
            _threads.TryGetValue(id, out var thread);
            return (DebuggeeThread) thread;
        }

        public DebuggeeLibrary GetLibraryByBase(IntPtr baseAddress)
        {
            _libraries.TryGetValue(baseAddress, out var library);
            return (DebuggeeLibrary) library;
        }

        public void Break()
        {
            NativeMethods.DebugBreakProcess(_processHandle);
        }

        public IEnumerable<IBreakpoint> GetSoftwareBreakpoints()
        {
            return _breakpoints.Values;
        }

        public IBreakpoint SetSoftwareBreakpoint(IntPtr address)
        {
            if (!_breakpoints.TryGetValue(address, out var breakpoint))
            {
                breakpoint = new Int3Breakpoint(this, address, true);
                _breakpoints[address] = breakpoint;
            }
            return breakpoint;
        }

        public void RemoveSoftwareBreakpoint(IBreakpoint breakpoint)
        {
            if (_breakpoints.Remove(breakpoint.Address))
                breakpoint.Enabled = false;
        }

        public void ReadMemory(IntPtr address, byte[] buffer, int offset, int length)
        {
            byte[] b = new byte[length];
            NativeMethods.ReadProcessMemory(_processHandle, address, b, b.Length, out var read);

            var affectedBreakpoints = _breakpoints.Values.Where(x =>
                (ulong) x.Address >= (ulong) address && (ulong) x.Address < (ulong) (address + length));
            foreach (var breakpoint in affectedBreakpoints)
            {
                int start = (int) (breakpoint.Address - (int) address);
                int end = Math.Min(start + breakpoint.OriginalBytes.Length, length);
                Buffer.BlockCopy(breakpoint.OriginalBytes, 0, b, start, end - start);
            }
            
            Buffer.BlockCopy(b, 0, buffer, offset, length);
        }

        public void WriteMemory(IntPtr address, byte[] buffer, int offset, int length)
        {
            byte[] b = new byte[length];
            Buffer.BlockCopy(buffer, offset, b, 0, length);
            NativeMethods.WriteProcessMemory(_processHandle, address, b, b.Length, out var written);
            NativeMethods.FlushInstructionCache(_processHandle, address, written);
        }

        internal void AddThread(DebuggeeThread thread)
        {
            _threads.Add(thread.Id, thread);
            OnThreadStarted(new DebuggeeThreadEventArgs(thread));
        }

        internal void RemoveThread(DebuggeeThread thread)
        {
            _threads.Remove(thread.Id);
            OnThreadTerminated(new DebuggeeThreadEventArgs(thread));
        }

        internal void AddLibrary(DebuggeeLibrary library)
        {
            _libraries.Add(library.BaseOfLibrary, library);
        }

        internal void RemoveLibrary(DebuggeeLibrary library)
        {
            _libraries.Remove(library.BaseOfLibrary);
        }
        
        public void Dispose()
        {
        }

        protected virtual void OnThreadStarted(DebuggeeThreadEventArgs e)
        {
            ThreadStarted?.Invoke(this, e);
        }

        protected virtual void OnThreadTerminated(DebuggeeThreadEventArgs e)
        {
            ThreadTerminated?.Invoke(this, e);
        }

        IDebuggeeThread IDebuggeeProcess.GetThreadById(int id)
        {
            return GetThreadById(id);
        }

        IDebuggeeLibrary IDebuggeeProcess.GetLibraryByBase(IntPtr baseAddress)
        {
            return GetLibraryByBase(baseAddress);
        }

        internal Int3Breakpoint GetBreakpointByAddress(IntPtr address)
        {
            _breakpoints.TryGetValue(address, out var breakpoint);
            return breakpoint;
        }
    }
}