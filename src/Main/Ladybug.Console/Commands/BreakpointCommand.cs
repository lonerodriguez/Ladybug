﻿using System;
using System.Globalization;
using System.Linq;
using Ladybug.Core;

namespace Ladybug.Console.Commands
{
    public class BreakpointCommand : ICommand
    {
        public string Description
        {
            get { return "Sets or changes the behaviour of software breakpoints in the process."; }
        }

        public string Usage
        {
            get { return "<set|remove|enable|disable> <address>"; }
        }

        public void Execute(IDebuggerSession session, string[] arguments, Logger output)
        {
            if (arguments.Length < 2)
                throw new ArgumentException("Not enough arguments.");
            
            ulong address = ulong.Parse(arguments[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            var debuggeeProcess = session.GetProcesses().First();
            switch (arguments[0].ToLowerInvariant())
            {
                case "set":
                    debuggeeProcess.SetSoftwareBreakpoint((IntPtr) address);
                    break;
                case "remove":
                    debuggeeProcess.RemoveSoftwareBreakpoint(
                        debuggeeProcess.GetSoftwareBreakpointByAddress((IntPtr) address));
                    break;
                case "enable":
                    debuggeeProcess.GetSoftwareBreakpointByAddress((IntPtr) address).Enabled = true;
                    break;
                case "disable":
                    debuggeeProcess.GetSoftwareBreakpointByAddress((IntPtr) address).Enabled = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Invalid switch " + arguments[1]);
            }


        }
    }
}