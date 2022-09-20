﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Doorstop
{
    class Entrypoint
    {
        public static string NStripPath => Path.Combine ( AppDomain.CurrentDomain.BaseDirectory, "carbon", "tools", "NStrip.exe" );
        public static string AssemblyCSharp => Path.GetFullPath ( Path.Combine ( AppDomain.CurrentDomain.BaseDirectory, "RustDedicated_Data/Managed/Assembly-CSharp.dll" ) );

        public static void Start ()
        {
            try
            {
                Process.Start ( new ProcessStartInfo
                {
                    FileName = NStripPath,
                    Arguments = $@"-p -cg --keep-resources -n --unity-non-serialized ""{AssemblyCSharp}"" ""{AssemblyCSharp}""",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                } ).WaitForExit ();
            }
            catch { }
        }
    }
}