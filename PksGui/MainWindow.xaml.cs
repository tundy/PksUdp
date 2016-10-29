using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using PksUdp;
using PksUdp.Client;
using PksUdp.Server;

namespace PksGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly PksServer _pksServer;
        private readonly PksClient _pksClient;
        private bool _lastStateServer;

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
            _pksClient.SocketException += _pksClient_SocketException;
            _pksClient.NoServerResponse += _pksClient_NoServerResponse;
            _pksClient.ClientError += _pksClient_ClientError;
        }

        private void _pksClient_ClientError()
        {
            Dispatcher.Invoke(() =>
            {
                Output.AppendTextAndScroll($"{DateTime.Now}: Prerusilo sa spojenie zo serverom{Environment.NewLine}");
                if (!_lastStateServer)
                    ResetControls();
            });
        }

        private void _pksClient_NoServerResponse()
        {
            Dispatcher.Invoke(() =>
            {
                Output.AppendTextAndScroll($"{DateTime.Now}: Server nepotvrdil spojenie{Environment.NewLine}");
                if (!_lastStateServer)
                    ResetControls();
            });
        }

        private void _pksClient_SocketException(SocketException e)
        {
            Dispatcher.Invoke(() =>
            {
                Output.AppendTextAndScroll($"{DateTime.Now}: {e.Message}{Environment.NewLine}");
                if (!_lastStateServer)
                    ResetControls();
            });
        }

        private void _pksClient_ReceivedMessage(PaketId id, bool success)
        {
            if (!success)
            {
                Output.Dispatcher.Invoke(() =>
                {
                    Output.AppendTextAndScroll($"{DateTime.Now}: Neuspesne odoslanie spravy.{Environment.NewLine}");
                });
                return;
            }
            Output.Dispatcher.Invoke(() =>
            {
                Output.AppendTextAndScroll($"{DateTime.Now}: Uspesne odoslanie spravy.{Environment.NewLine}");
            });
        }

        private void _pksClient_ClientConnected()
        {
            Dispatcher.Invoke(() => {
                Output.AppendTextAndScroll($"{DateTime.Now}: Vzniklo spojenie zo serverom{Environment.NewLine}");
                InputPanel.IsEnabled = true;
            }); 
        }

        private void PksServerReceivedMessage(IPEndPoint endPoint, Message message)
        {
           Output.Dispatcher.Invoke(() => {
               Output.AppendTextAndScroll($"({endPoint}) {DateTime.Now}:{Environment.NewLine}");
               Output.AppendTextAndScroll($"Pocet fragmentov: {message.FragmentsCount}{Environment.NewLine}");
               Output.AppendTextAndScroll($"Dlzka fragmentu: {message.FragmentLength}{Environment.NewLine}");
               Output.AppendTextAndScroll(message.Error
                   ? $"Nepodarilo sa nacitat celu spravu.{Environment.NewLine}"
                   : $"Sprava: {message.Text}{Environment.NewLine}");
           });
        }

        private void PksServerClientDisconnected(IPEndPoint endPoint)
        {
            Output.Dispatcher.Invoke(() => { Output.AppendTextAndScroll($"({endPoint}) {DateTime.Now}: Client disconnected{Environment.NewLine}"); });
        }

        private void PksServerClientConnected(IPEndPoint endPoint)
        {
            Output.Dispatcher.Invoke(() => { Output.AppendTextAndScroll($"({endPoint}) {DateTime.Now}: Client connected{Environment.NewLine}"); });
        }
        private void PksServerClientTimedOud(IPEndPoint endPoint)
        {
            Output.Dispatcher.Invoke(() => { Output.AppendTextAndScroll($"({endPoint}) {DateTime.Now}: Client Timedout{Environment.NewLine}"); });
        }

        private void Disconnect_ButtonClick(object sender, RoutedEventArgs e)
        {
            ResetControls();

            if (_lastStateServer)
            {
                Output.AppendTextAndScroll($"Server sa ukoncil.{Environment.NewLine}");
                _pksServer.Close();
            }
            else
            {
                Output.AppendTextAndScroll($"Klient sa odpojil od servera.{Environment.NewLine}");
                _pksClient.Close();
            }
        }

        private void ResetControls()
        {
            Title = "Komunikator";
            StartServerButton.IsEnabled = true;
            ConnectButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            ThisPort.IsEnabled = true;
            EndPointPanel.IsEnabled = true;
            FragmentSize.IsEnabled = false;
            InputPanel.IsEnabled = false;
        }

        private void ServerStart_ButtonClick(object sender, RoutedEventArgs e)
        {
            if(!_lastStateServer)
                Output.Clear();
            _lastStateServer = true;

            ushort port;
            if (!ushort.TryParse(ThisPort.Text, out port))
            {
                Output.AppendTextAndScroll($"Nepodarilo sa nacitat port.{Environment.NewLine}");
                return;
            }

            try
            {
                _pksServer.Port = port;
            }
            catch (Exception ex)
            {
                Output.AppendTextAndScroll($"{ex.Message}{Environment.NewLine}");
                return;
            }
            Output.AppendTextAndScroll($"Uspesne spustenie server na porte {port}.{Environment.NewLine}");
            Title = $"Server - {port}";
            StartServerButton.IsEnabled = false;
            ConnectButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            EndPointPanel.IsEnabled = false;
            ThisPort.IsEnabled = false;
        }

        private void Connect_ButtonClick(object sender, RoutedEventArgs e)
        {
            if (_lastStateServer)
                Output.Clear();
            _lastStateServer = false;

            ushort port;
            if (!ushort.TryParse(ThisPort.Text, out port))
            {
                try
                {
                    _pksClient.Port = null;
                }
                catch (Exception ex)
                {
                    Output.AppendTextAndScroll($"{ex.Message}{Environment.NewLine}");
                    return;
                }

            }
            try
            {
                _pksClient.Port = port;
            }
            catch (Exception ex)
            {
                Output.AppendTextAndScroll($"{ex.Message}{Environment.NewLine}");
                return;
            }


            IPAddress ip;
            if (!IPAddress.TryParse(ServerIp.Text, out ip))
            {
                Output.AppendTextAndScroll($"Nepodarilo sa nacitat IP adresu.{Environment.NewLine}");
                return;
            }

            ushort portServer;
            if (!ushort.TryParse(ServerPort.Text, out portServer))
            {
                Output.AppendTextAndScroll($"Nepodarilo sa nacitat port servera.{Environment.NewLine}");
                return;
            }

            Output.AppendTextAndScroll($"Pokusam sa pripojit na {ip}:{portServer}.{Environment.NewLine}");
            Title = $"Klient - {ip}:{portServer}";
            StartServerButton.IsEnabled = false;
            ConnectButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            EndPointPanel.IsEnabled = false;
            ThisPort.IsEnabled = false;
            FragmentSize.IsEnabled = true;

            _pksClient.Connect(ip, portServer);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Output.Clear();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            int size;
            if (!int.TryParse(FragmentSize.Text, out size) || size > 65470)
            {
                Output.AppendTextAndScroll($"Nepodarilo sa nacitat velkost fragmentu.{Environment.NewLine}");
                return;
            }

            _pksClient.SendMessage(Input.Text, size, ChybnyFragment.IsChecked == true);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _pksServer?.Close();
            _pksClient?.Close();
        }

        private void Input_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
            Button_Click_1(sender, e);
            e.Handled = true;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.InitialDirectory = Path.GetDirectoryName(FilePath.Text);
            if (dialog.ShowDialog() == true)
                FilePath.Text = dialog.FileName;
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            int size;
            if (!int.TryParse(FragmentSize.Text, out size) || size > 65470)
            {
                Output.AppendTextAndScroll($"Nepodarilo sa nacitat velkost fragmentu.{Environment.NewLine}");
                return;
            }

            _pksClient.SendFile(FilePath.Text, size, ChybnyFragment.IsChecked == true);
        }
    }

}
