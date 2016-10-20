using System;
using System.Net;
using System.Timers;

namespace PksUdp
{
    internal class ClientThread
    {
        /// <summary>
        /// Udp Socket.
        /// </summary>
        private readonly PksClient _pksClient;

        /// <summary>
        /// Timer pre kontrolu spojenia.
        /// </summary>
        private readonly System.Timers.Timer _connectionTimer = new System.Timers.Timer
        {
            Interval = 45000,
            AutoReset = false
        };

        /// <summary>
        /// Timer pre znovu vyziadanie fragmentov.
        /// </summary>
        private readonly System.Timers.Timer _recieveTimer = new System.Timers.Timer {Interval = 500, AutoReset = false};

        public ClientThread(PksClient pksClient)
        {
            _pksClient = pksClient;
            _recieveTimer.Elapsed += _recieveTimer_Elapsed;
            _connectionTimer.Elapsed += _connectionTimer_Elapsed;
        }

        private void _connectionTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void _recieveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}