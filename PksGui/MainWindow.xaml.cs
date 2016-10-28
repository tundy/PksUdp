using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        ~MainWindow()
        {
            _pksServer?.Close();
        }

        public MainWindow()
        {
            InitializeComponent();

            /*_pksServer = new PksServer(35555);
            _pksServer.ClientConnected += PksServerClientConnected;
            _pksServer.ClientDisconnected += PksServerClientDisconnected;
            _pksServer.ClientTimedOut += PksServerClientTimedOud;
            _pksServer.ReceivedMessage += PksServerReceivedMessage;*/
        }
        /*
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
        }*/
    }
}
