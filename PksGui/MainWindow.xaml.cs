using System;
using System.ComponentModel;
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
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly PksClient _pksClient;
        private readonly PksServer _pksServer;
        private bool _lastStateServer;

        public MainWindow()
        {
            InitializeComponent();

            _pksServer = new PksServer();
            _pksServer.ClientConnected += PksServerClientConnected;
            _pksServer.ClientDisconnected += PksServerClientDisconnected;
            _pksServer.ClientTimedOut += PksServerClientTimedOud;
            _pksServer.ReceivedMessage += PksServerReceivedMessage;
            _pksServer.ReceivedFile += _pksServer_ReceivedFile;
            _pksServer.Buffering += _pksServer_Buffering;
            _pksServer.ServerDown += _pksServer_ServerDown;
            _pksClient = new PksClient();
            _pksClient.ClientConnected += _pksClient_ClientConnected;
            _pksClient.ReceivedMessage += _pksClient_ReceivedMessage;
            _pksClient.ReceivedFile += _pksClient_ReceivedFile;
            _pksClient.SocketException += _pksClient_SocketException;
            _pksClient.NoServerResponse += _pksClient_NoServerResponse;
            _pksClient.ClientError += _pksClient_ClientError;
        }

        ~MainWindow()
        {
            _pksServer?.Close();
            _pksClient?.Close();
        }

        private void _pksServer_ServerDown(Exception e)
        {
            if (!_lastStateServer) return;
            Output.AppendTextAndScroll($"Spadol server.{Environment.NewLine}{e.Message}{Environment.NewLine}");
            _pksServer.Close();
            ResetControls();
            GC.Collect();
        }

        private void _pksServer_Buffering(IPEndPoint endpoint, PaketId id, uint loaded, uint? total)
        {
            Output.Dispatcher.Invoke(() =>
            {
                Output.AppendTextAndScroll(total == null
                    ? $"{DateTime.Now}: Nepodarilo sa identifikovať správu{Environment.NewLine}"
                    : $"{DateTime.Now}: Mám zatiaľ načítaných {loaded} z {total.Value} fragmentov ({loaded*100/total.Value})%{Environment.NewLine}");
            });
        }

        private void _pksClient_ReceivedFile(PaketId id, bool success)
        {
            if (!success)
            {
                Output.Dispatcher.Invoke(
                    () =>
                    {
                        Output.AppendTextAndScroll($"{DateTime.Now}: Neúspešné odoslanie súboru.{Environment.NewLine}");
                    });
                return;
            }
            Output.Dispatcher.Invoke(
                () => { Output.AppendTextAndScroll($"{DateTime.Now}: Úspešné odoslanie súboru.{Environment.NewLine}"); });
        }

        private void _pksServer_ReceivedFile(IPEndPoint endPoint, FilePacket file)
        {
            Output.Dispatcher.Invoke(() =>
            {
                Output.AppendTextAndScroll($"({endPoint}) {DateTime.Now}:{Environment.NewLine}");
                Output.AppendTextAndScroll($"Počet fragmentov: {file.FragmentsCount}{Environment.NewLine}");
                Output.AppendTextAndScroll($"Dížka fragmentu: {file.FragmentLength}{Environment.NewLine}");
                Output.AppendTextAndScroll(file.Error
                    ? $"Nepodarilo sa stiahnuť súbor.{Environment.NewLine}"
                    : $"Súbor: {file.FileInfo.FullName}{Environment.NewLine}");
            });
        }

        private void _pksClient_ClientError()
        {
            Dispatcher.Invoke(() =>
            {
                if (_lastStateServer) return;
                Output.AppendTextAndScroll($"{DateTime.Now}: Prerušilo sa spojenie zo serverom{Environment.NewLine}");
                ResetControls();
            });
            GC.Collect();
        }

        private void _pksClient_NoServerResponse()
        {
            Dispatcher.Invoke(() =>
            {
                if (_lastStateServer) return;
                Output.AppendTextAndScroll($"{DateTime.Now}: Server nepotvrdil spojenie{Environment.NewLine}");
            });
        }

        private void _pksClient_SocketException(SocketException e)
        {
            Dispatcher.Invoke(() =>
            {
                if (_lastStateServer) return;
                Output.AppendTextAndScroll($"{DateTime.Now}: {e.Message}{Environment.NewLine}");
                ResetControls();
            });
            GC.Collect();
        }

        private void _pksClient_ReceivedMessage(PaketId id, bool success)
        {
            if (!success)
            {
                Output.Dispatcher.Invoke(
                    () =>
                    {
                        Output.AppendTextAndScroll($"{DateTime.Now}: Neúspešné odoslanie správy.{Environment.NewLine}");
                    });
                return;
            }
            Output.Dispatcher.Invoke(
                () => { Output.AppendTextAndScroll($"{DateTime.Now}: Úspešné odoslanie správy.{Environment.NewLine}"); });
        }

        private void _pksClient_ClientConnected()
        {
            Dispatcher.Invoke(() =>
            {
                Output.AppendTextAndScroll($"{DateTime.Now}: Vzniklo spojenie zo serverom{Environment.NewLine}");
                InputPanel.IsEnabled = true;
            });
        }

        private void PksServerReceivedMessage(IPEndPoint endPoint, Message message)
        {
            Output.Dispatcher.Invoke(() =>
            {
                Output.AppendTextAndScroll($"({endPoint}) {DateTime.Now}:{Environment.NewLine}");
                Output.AppendTextAndScroll($"Počet fragmentov: {message.FragmentsCount}{Environment.NewLine}");
                Output.AppendTextAndScroll($"Dĺžka fragmentu: {message.FragmentLength}{Environment.NewLine}");
                Output.AppendTextAndScroll(message.Error
                    ? $"Nepodarilo sa načítať celú správu.{Environment.NewLine}"
                    : $"Správa: {message.Text}{Environment.NewLine}");
            });
        }

        private void PksServerClientDisconnected(IPEndPoint endPoint)
        {
            Output.Dispatcher.Invoke(
                () =>
                {
                    Output.AppendTextAndScroll($"({endPoint}) {DateTime.Now}: Klient sa odpojil{Environment.NewLine}");
                });
        }

        private void PksServerClientConnected(IPEndPoint endPoint)
        {
            Output.Dispatcher.Invoke(
                () =>
                {
                    Output.AppendTextAndScroll($"({endPoint}) {DateTime.Now}: Klient sa pripojil{Environment.NewLine}");
                });
        }

        private void PksServerClientTimedOud(IPEndPoint endPoint)
        {
            Output.Dispatcher.Invoke(
                () =>
                {
                    Output.AppendTextAndScroll(
                        $"({endPoint}) {DateTime.Now}: Prerušilo sa spojenie zo serverom{Environment.NewLine}");
                });
        }

        private void Disconnect_ButtonClick(object sender, RoutedEventArgs e)
        {
            ResetControls();

            if (_lastStateServer)
            {
                Output.AppendTextAndScroll($"Server sa ukončil.{Environment.NewLine}");
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
            Title = "Komunikátor";
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
            if (!_lastStateServer)
                Output.Clear();
            _lastStateServer = true;

            ushort port;
            if (!ushort.TryParse(ThisPort.Text, out port))
            {
                Output.AppendTextAndScroll($"Nepodarilo sa načítať port.{Environment.NewLine}");
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
            Output.AppendTextAndScroll($"Úspešné spustenie servera na porte {port}.{Environment.NewLine}");
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
                try
                {
                    _pksClient.Port = null;
                }
                catch (Exception ex)
                {
                    Output.AppendTextAndScroll($"{ex.Message}{Environment.NewLine}");
                    return;
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
                Output.AppendTextAndScroll($"Nepodarilo sa načítať IP adresu.{Environment.NewLine}");
                return;
            }

            ushort portServer;
            if (!ushort.TryParse(ServerPort.Text, out portServer))
            {
                Output.AppendTextAndScroll($"Nepodarilo sa načítať port servera.{Environment.NewLine}");
                return;
            }

            Output.AppendTextAndScroll($"Pokúšam sa pripojiť na {ip}:{portServer}.{Environment.NewLine}");
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
            if (!int.TryParse(FragmentSize.Text, out size))
            {
                Output.AppendTextAndScroll($"Nepodarilo sa načítat veľkosť fragmentu.{Environment.NewLine}");
                return;
            }

            if (size > 65470)
            {
                Output.AppendTextAndScroll($"Veľkosť fragmentu nemôže byť vačšia ako 65470.{Environment.NewLine}");
                return;
            }

            if (size < 20)
            {
                Output.AppendTextAndScroll($"Veľkosť fragmentu nemôže byť menšia ako 20.{Environment.NewLine}");
                return;
            }

            PksClient.Fragmenty f;
            if (Vsetky.IsChecked == true)
                f = PksClient.Fragmenty.VsetkyChybne;
            else if (Prvy.IsChecked == true)
                f = PksClient.Fragmenty.PrvyChybny;
            else
                f = PksClient.Fragmenty.ZiadneChybne;
            _pksClient.SendMessage(Input.Text, size, f);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _pksServer?.Close();
            _pksClient?.Close();
        }

        private void Input_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key != Key.Enter) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
            Button_Click_1(sender, e);
            e.Handled = true;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            try
            {
                var x = Path.GetDirectoryName(FilePath.Text);
                dialog.InitialDirectory = x;
            }
            catch
            {
                // ignored
            }
            if (dialog.ShowDialog() == true)
                FilePath.Text = dialog.FileName;
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            int size;
            if (!int.TryParse(FragmentSize.Text, out size))
            {
                Output.AppendTextAndScroll($"Nepodarilo sa načítať veľkostť fragmentu.{Environment.NewLine}");
                return;
            }

            if (size > 65470)
            {
                Output.AppendTextAndScroll($"Veľkosť fragmentu nemôže byť väčšia ako 65470.{Environment.NewLine}");
                return;
            }

            if (size < 20)
            {
                Output.AppendTextAndScroll($"Veľkosť fragmentu nemôžze byť menšia ako 20.{Environment.NewLine}");
                return;
            }

            PksClient.Fragmenty f;
            if (Vsetky.IsChecked == true)
                f = PksClient.Fragmenty.VsetkyChybne;
            else if (Prvy.IsChecked == true)
                f = PksClient.Fragmenty.PrvyChybny;
            else
                f = PksClient.Fragmenty.ZiadneChybne;
            _pksClient.SendFile(FilePath.Text, size, f);
        }
    }
}