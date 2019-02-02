using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BifrostExtended
{
    public enum SerilogLogLevel
    {
        Verbose = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Fatal = 5
    }

    public class Utilities
    {
        public static void RaiseEventOnUIThread(Delegate theEvent, string args)
        {
            if (theEvent == null)
                return;

            foreach (Delegate d in theEvent.GetInvocationList())
            {
                ISynchronizeInvoke syncer = d.Target as ISynchronizeInvoke;
                if (syncer == null)
                {
                    d.DynamicInvoke(args);
                }
                else
                {
                    try
                    {
                        syncer.BeginInvoke(d, new object[] { args });
                    }
                    catch { }
                }
            }
        }

        public delegate void ClientConnectionState(Client client, bool Connected);

        // Client
        public delegate void ClientDataReceived(Client client, Dictionary<string, byte[]> Store);
        
        public static void RaiseEventOnUIThread(Delegate theEvent, ClientData arg1, Dictionary<string, byte[]> arg2)
        {
            if (theEvent == null)
                return;

            foreach (Delegate d in theEvent.GetInvocationList())
            {
                ISynchronizeInvoke syncer = d.Target as ISynchronizeInvoke;
                if (syncer == null)
                {
                    d.DynamicInvoke(new object[] { arg1, arg2 });
                }
                else
                {
                    try
                    {
                        syncer.BeginInvoke(d, new object[] { arg1, arg2 });
                    }
                    catch { }
                }
            }
        }

        public static void RaiseEventOnUIThread(Delegate theEvent, Client arg1, bool arg2)
        {
            if (theEvent == null)
                return;

            foreach (Delegate d in theEvent.GetInvocationList())
            {
                ISynchronizeInvoke syncer = d.Target as ISynchronizeInvoke;
                if (syncer == null)
                {
                    d.DynamicInvoke(new object[] { arg1, arg2 });
                }
                else
                {
                    try
                    {
                        syncer.BeginInvoke(d, new object[] { arg1, arg2 });
                    }
                    catch { }
                }
            }
        }

        public static void RaiseEventOnUIThread(Delegate theEvent, Client arg1, Dictionary<string, byte[]> arg2)
        {
            if (theEvent == null)
                return;

            foreach (Delegate d in theEvent.GetInvocationList())
            {
                ISynchronizeInvoke syncer = d.Target as ISynchronizeInvoke;
                if (syncer == null)
                {
                    d.DynamicInvoke(new object[] { arg1, arg2 });
                }
                else
                {
                    try
                    {
                        syncer.BeginInvoke(d, new object[] { arg1, arg2 });
                    }
                    catch { }
                }
            }
        }
        
        public static void RaiseEventOnUIThread(Delegate theEvent, Dictionary<string, byte[]> args)
        {
            if (theEvent == null)
                return;

            foreach (Delegate d in theEvent.GetInvocationList())
            {
                ISynchronizeInvoke syncer = d.Target as ISynchronizeInvoke;
                if (syncer == null)
                {
                    d.DynamicInvoke(args);
                }
                else
                {
                    try
                    {
                        syncer.BeginInvoke(d, new object[] { args });
                    }
                    catch { }
                }
            }
        }

        public static void RaiseEventOnUIThread(Delegate theEvent, ClientData args)
        {
            if (theEvent == null)
                return;

            foreach (Delegate d in theEvent.GetInvocationList())
            {
                ISynchronizeInvoke syncer = d.Target as ISynchronizeInvoke;
                if (syncer == null)
                {
                    d.DynamicInvoke(args);
                }
                else
                {
                    try
                    {
                        syncer.BeginInvoke(d, new object[] { args });
                    }
                    catch { }
                }
            }
        }
    }

}
