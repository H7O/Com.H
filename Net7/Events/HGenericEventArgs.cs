using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Com.H.Events
{
    public class HErrorEventArgs
    {
        public object Sender { get; init; }
        public Exception Exception { get; init; }
        public HErrorEventArgs(
            object sender, Exception exception)
            => (Sender, Exception)
            = (sender, exception);
    }

    public delegate void HErrorEventHandler(object sender, HErrorEventArgs e);

    public class HMsgEventArgs
    {
        public object Sender { get; init; }
        public string Message { get; init; }
        public HMsgEventArgs(
            object sender, string message)
            => (Sender, Message)
            = (sender, message);
    }

    public delegate void HMsgEventHandler(object sender, HMsgEventArgs e);


}
