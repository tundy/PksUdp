using System;

namespace PksUdp
{
    /// <summary>
    ///     Paket containing string as Data.
    /// </summary>
    public class Message : Packet
    {
        private string _text;

        /// <summary>
        ///     Recieved message.
        /// </summary>
        /// <exception cref="InvalidOperationException" />
        public string Text
        {
            internal set { _text = value; }
            get
            {
                if (Error) throw new InvalidOperationException();
                return _text;
            }
        }
    }
}