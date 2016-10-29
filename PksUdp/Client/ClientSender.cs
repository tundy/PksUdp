using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace PksUdp.Client
{
    internal class ClientSender
    {
        /// <summary>
        /// Udp Socket.
        /// </summary>
        private readonly PksClient _pksClient;

        public ClientSender(PksClient pksClient)
        {
            _pksClient = pksClient;
        }


        internal void SenderLoop()
        {
            try
            {
                for (;;)
                {
                    lock (_pksClient.ConnectedLocker)
                    {
                        if (!_pksClient.Connected)
                        {
                            continue;
                        }
                    }


                    lock (_pksClient.LastMessageLock)
                    {
                        if(_pksClient.lastMessage != null)
                            continue;
                    }

                    PksClient.NaOdoslanie odoslat;
                    lock (_pksClient.PoradovnikLock)
                    {
                        if (!_pksClient.Poradovnik.Any())
                        {
                            continue;
                        }
                        odoslat = _pksClient.Poradovnik.Dequeue();
                    }

                    var odoslanie = odoslat as PksClient.SpravaNaOdoslanie;
                    if (odoslanie != null)
                    {

                        lock (_pksClient.LastMessageLock)
                        {
                            _pksClient.lastMessage = new MessageFragments(new PaketId());
                            RozdelSpravuNaFragmenty((MessageFragments) _pksClient.lastMessage, odoslanie);
                            foreach (var fragment in ((MessageFragments) _pksClient.lastMessage).fragments)
                            {
                                _pksClient.Socket.Client.Send(fragment, fragment.Length, SocketFlags.None);
                            }
                        }
                    }
                    else
                    {
                        var naOdoslanie = odoslat as PksClient.SuborNaOdoslanie;
                        if (naOdoslanie != null)
                        {
                            // ToDo
                        }
                    }
                }
            }
            catch (ThreadAbortException)
            {
            }
            catch (SocketException ex)
            {
                _pksClient.OnSocketException(ex);
            }
        }

        private static void RozdelSpravuNaFragmenty(MessageFragments pksClientLastMessage, PksClient.SpravaNaOdoslanie sprava)
        {
            if (sprava.FragmentSize >= sprava.Sprava.Length + 10)
            {
                var fragment = new byte[sprava.Sprava.Length + 10];
                fragment[0] = 0x7E;
                fragment[fragment.Length-1] = 0x7E;
                fragment.SetFragmentId(pksClientLastMessage.PaketId);
                fragment.SetPaketType(Extensions.Type.Message);
                Encoding.UTF8.GetBytes(sprava.Sprava, 0, sprava.Sprava.Length, fragment, Extensions.FragmentDataIndex);
                fragment.CreateChecksum();
                pksClientLastMessage.fragments.Add(fragment);
            }
            else
            {
                uint order = 0;

                var fragment = new byte[sprava.FragmentSize];
                fragment[0] = 0x7E;
                fragment[fragment.Length - 1] = 0x7E;
                fragment.SetFragmentId(pksClientLastMessage.PaketId);
                fragment.SetPaketType(Extensions.Type.Message);
                fragment.SetFragmentOrder(order++);
                Encoding.UTF8.GetBytes(sprava.Sprava, 0, fragment.Length - 18, fragment, Extensions.FragmentDataf0Index);
                sprava.Sprava = sprava.Sprava.Substring(fragment.Length - 18);
                fragment.SetFragmentCount((uint)(sprava.Sprava.Length / (sprava.FragmentSize - 14)) + 1 );
                fragment.CalculateChecksum();

                pksClientLastMessage.fragments.Add(fragment);

                var offset = 0;
                while (sprava.Sprava.Length - offset > sprava.FragmentSize - 14)
                {
                    fragment = new byte[sprava.FragmentSize];
                    fragment[0] = 0x7E;
                    fragment[fragment.Length - 1] = 0x7E;
                    fragment.SetFragmentId(pksClientLastMessage.PaketId);
                    fragment.SetPaketType(Extensions.Type.Message);
                    fragment.SetFragmentOrder(order++);
                    Encoding.UTF8.GetBytes(sprava.Sprava, offset, fragment.Length - 14, fragment, Extensions.FragmentDatafIndex);
                    offset += sprava.FragmentSize - 14;
                    fragment.CalculateChecksum();
                    pksClientLastMessage.fragments.Add(fragment);
                }

                fragment = new byte[sprava.FragmentSize - (sprava.Sprava.Length - offset)];
                fragment[0] = 0x7E;
                fragment[fragment.Length - 1] = 0x7E;
                fragment.SetFragmentId(pksClientLastMessage.PaketId);
                fragment.SetPaketType(Extensions.Type.Message);
                fragment.SetFragmentOrder(order);
                Encoding.UTF8.GetBytes(sprava.Sprava, offset, fragment.Length - 14, fragment, Extensions.FragmentDatafIndex);
                fragment.CalculateChecksum();
                pksClientLastMessage.fragments.Add(fragment);
            }
        }
    }
}