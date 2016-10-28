using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PksUdp;

namespace PksGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly PksServer _pksServer;
        private readonly PksClient _pksClient;
        private bool lastStateServer;

        ~MainWindow()
        {
            _pksServer?.Close();
            _pksClient?.Close();
        }

        public MainWindow()
        {
            InitializeComponent();

            _pksServer = new PksServer();
            _pksServer.ClientConnected += PksServerClientConnected;
            _pksServer.ClientDisconnected += PksServerClientDisconnected;
            _pksServer.ClientTimedOut += PksServerClientTimedOud;
            _pksServer.ReceivedMessage += PksServerReceivedMessage;
            _pksClient = new PksClient();
            _pksClient.ClientConnected += _pksClient_ClientConnected;
            _pksClient.ReceivedMessage += _pksClient_ReceivedMessage;
            _pksClient.ServerTimedOut += _pksClient_ServerTimedOut;
        }

        private void _pksClient_ServerTimedOut()
        {
            Dispatcher.Invoke(() =>
            {
                Output.AppendText($"{DateTime.Now}: Server Timedout{Environment.NewLine}");
                if(!lastStateServer)
                    Close();
            });
        }

        private void _pksClient_ReceivedMessage(PaketId id, bool success)
        {
            if (!success)
            {
                Output.Dispatcher.Invoke(() => { Output.AppendText($"{DateTime.Now}: Neuspesne odoslanie spravy.{Environment.NewLine}"); });
                return;
            }
            Output.Dispatcher.Invoke(() => { Output.AppendText($"{DateTime.Now}: Uspesne odoslanie spravy.{Environment.NewLine}"); });
        }

        private void _pksClient_ClientConnected()
        {
            Output.Dispatcher.Invoke(() => { Output.AppendText($"{DateTime.Now}: Vzniklo spojenie zo serverom{Environment.NewLine}"); });
            InputPanel.Dispatcher.Invoke(() => { InputPanel.IsEnabled = true; }); 
        }

        private void PksServerReceivedMessage(System.Net.IPEndPoint endPoint, Message message)
        {
           Output.Dispatcher.Invoke(() => {
               Output.AppendText($"({endPoint}) {DateTime.Now}:{Environment.NewLine}");
               Output.AppendText($"Pocet fragmentov: {message.FragmentsCount}{Environment.NewLine}");
               Output.AppendText($"Dlzka fragmentu: {message.FragmentLength}{Environment.NewLine}");
               Output.AppendText(message.Error
                   ? $"Nepodarilo sa nacitat celu spravu.{Environment.NewLine}"
                   : $"Sprava: {message.Text}{Environment.NewLine}");
           });
        }

        private void PksServerClientDisconnected(System.Net.IPEndPoint endPoint)
        {
            Output.Dispatcher.Invoke(() => { Output.AppendText($"({endPoint}) {DateTime.Now}: Client disconnected{Environment.NewLine}"); });
        }

        private void PksServerClientConnected(System.Net.IPEndPoint endPoint)
        {
            Output.Dispatcher.Invoke(() => { Output.AppendText($"({endPoint}) {DateTime.Now}: Client connected{Environment.NewLine}"); });
        }
        private void PksServerClientTimedOud(System.Net.IPEndPoint endPoint)
        {
            Output.Dispatcher.Invoke(() => { Output.AppendText($"({endPoint}) {DateTime.Now}: Client Timedout{Environment.NewLine}"); });
        }

        private void Disconnect_ButtonClick(object sender, RoutedEventArgs e)
        {
            Title = "Komunikator";

            StartServerButton.IsEnabled = true;
            ConnectButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            ThisPort.IsEnabled = true;
            EndPointPanel.IsEnabled = true;

            if (lastStateServer)
            {
                Output.AppendText($"Server sa ukoncil.{Environment.NewLine}");
                _pksServer.Close();
            }
            else
            {
                FragmentSize.IsEnabled = false;
                InputPanel.IsEnabled = false;
                Output.AppendText($"Klient sa odpojil od servera.{Environment.NewLine}");
                _pksClient.Close();
            }
        }

        private void ServerStart_ButtonClick(object sender, RoutedEventArgs e)
        {
            ushort port;
            if (!ushort.TryParse(ThisPort.Text, out port))
            {
                Output.AppendText($"Nepodarilo sa nacitat port.{Environment.NewLine}");
                return;
            }

            try
            {
                _pksServer.Port = port;
            }
            catch (Exception ex)
            {
                Output.AppendText($"{ex.Message}{Environment.NewLine}");
                return;
            }
            Output.AppendText($"Uspesne spustenie server na porte {port}.{Environment.NewLine}");
            Title = $"Server - {port}";
            lastStateServer = true;
            StartServerButton.IsEnabled = false;
            ConnectButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            EndPointPanel.IsEnabled = false;
            ThisPort.IsEnabled = false;
        }

        private void Connect_ButtonClick(object sender, RoutedEventArgs e)
        {

            ushort port;
            if (!ushort.TryParse(ThisPort.Text, out port))
            {
                try
                {
                    _pksClient.Port = null;
                }
                catch (Exception ex)
                {
                    Output.AppendText($"{ex.Message}{Environment.NewLine}");
                    return;
                }

            }
            try
            {
                _pksClient.Port = port;
            }
            catch (Exception ex)
            {
                Output.AppendText($"{ex.Message}{Environment.NewLine}");
                return;
            }


            IPAddress ip;
            if (!IPAddress.TryParse(ServerIp.Text, out ip))
            {
                Output.AppendText($"Nepodarilo sa nacitat IP adresu.{Environment.NewLine}");
                return;
            }

            ushort portServer;
            if (!ushort.TryParse(ServerPort.Text, out portServer))
            {
                Output.AppendText($"Nepodarilo sa nacitat port servera.{Environment.NewLine}");
                return;
            }

            Output.AppendText($"Pokusam sa pripojit na {ip}:{portServer}.{Environment.NewLine}");
            Title = $"Klient - {ip}:{portServer}";
            lastStateServer = false;
            StartServerButton.IsEnabled = false;
            ConnectButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            EndPointPanel.IsEnabled = false;
            ThisPort.IsEnabled = false;
            FragmentSize.IsEnabled = true;

            _pksClient.Connect(ip, portServer);
        }
    }

}
