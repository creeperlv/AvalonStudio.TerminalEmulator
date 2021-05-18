﻿using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using static winpty.WinPty;

namespace AvalonStudio.Terminals.Win32
{
    public class Win32PsuedoTerminalProvider : IPsuedoTerminalProvider
    {
        private static IntPtr TryGetHandle(Process p)
        {
            var result = IntPtr.Zero;

            try
            {
                result = p.Handle;
            }
            catch (Exception e)
            {

            }

            return result;
        }

        public IPsuedoTerminal Create(int columns, int rows, string initialDirectory, string environment, string command, params string[] arguments)
        {
            var cfg = winpty_config_new(WINPTY_FLAG_COLOR_ESCAPES, out IntPtr err);
            winpty_config_set_initial_size(cfg, columns, rows);

            var handle = winpty_open(cfg, out err);

            if (err != IntPtr.Zero)
            {
                Trace.WriteLine("Pointer is Zero");
                Trace.WriteLine(winpty_error_code(err));
                return null;
            }

            string exe = command;
            string args = String.Join(" ", arguments);
            string cwd = initialDirectory;

            var spawnCfg = winpty_spawn_config_new(WINPTY_SPAWN_FLAG_AUTO_SHUTDOWN, exe, args, cwd, environment, out err);
            if (err != IntPtr.Zero)
            {
                Trace.WriteLine("Pointer is Zero");
                Trace.WriteLine(winpty_error_code(err));
                return null;
            }

            var stdin = CreatePipe(winpty_conin_name(handle), PipeDirection.Out);
            var stdout = CreatePipe(winpty_conout_name(handle), PipeDirection.In);

            if (!winpty_spawn(handle, spawnCfg, out IntPtr process, out IntPtr thread, out int procError, out err))
            {
                Trace.WriteLine("Launch Failed");
                Trace.WriteLine(winpty_error_code(err));
                return null;
            }

            var id = GetProcessId(process);

            var terminalProcess = Process.GetProcessById(id);                      

            return new Win32PsuedoTerminal(terminalProcess, handle, cfg, spawnCfg, err, stdin, stdout);
        }

        [DllImport("kernel32.dll")]
        static extern int GetProcessId(IntPtr handle);

        private Stream CreatePipe(string pipeName, PipeDirection direction)
        {
            string serverName = ".";

            if (pipeName.StartsWith("\\"))
            {
                int slash3 = pipeName.IndexOf('\\', 2);

                if (slash3 != -1)
                {
                    serverName = pipeName.Substring(2, slash3 - 2);
                }

                int slash4 = pipeName.IndexOf('\\', slash3 + 1);

                if (slash4 != -1)
                {
                    pipeName = pipeName.Substring(slash4 + 1);
                }
            }

            var pipe = new NamedPipeClientStream(serverName, pipeName, direction);

            pipe.Connect();

            return pipe;
        }
    }
}
