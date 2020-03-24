using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SimpleTransmission
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        SimpleTransimission.Models.Transmission.SensorBLE Sensor;
        ObservableCollection<string> Messages;

        public MainPage()
        {
            this.InitializeComponent();

            Messages = new ObservableCollection<string>();
            ListViewConsole.ItemsSource = Messages;

            Sensor = new SimpleTransimission.Models.Transmission.SensorBLE("DA:DD:55:21:D0:3F");

            Sensor.OnChangeState += Sensor_OnChangeState;
            Sensor.OnPacketReceived += Sensor_OnPacketReceived;
            Sensor.OnSensorDetected += Sensor_OnSensorDetected;
            Sensor.OnSensorMisaligned += Sensor_OnSensorMisaligned;
            Sensor.OnException += Sensor_OnException;
            Sensor.Connect();
        }

        private async void Sensor_OnException(object sender, EventArgs e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    Messages.Insert(0, $"EXCEPTION:\n{sender}");
                });
        }

        private async void Sensor_OnSensorMisaligned(object sender, EventArgs e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    Messages.Insert(0, $"Sensor misaligned!");
                });
        }

        private async void Sensor_OnSensorDetected(object sender, EventArgs e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    Messages.Insert(0, $"Detected BLE: {sender}");
                });
        }

        private async void Sensor_OnPacketReceived(object sender, EventArgs e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    Messages.Insert(0, ((double)sender).ToString());
                });
        }

        private async void Sensor_OnChangeState(object sender, EventArgs e)
        {
            SimpleTransimission.Models.Transmission.SensorBLE sensor = (sender as SimpleTransimission.Models.Transmission.SensorBLE);
            SimpleTransimission.Helpers.Services.BLEServices.ConnectionState sensorstate = sensor.State;

            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    Messages.Insert(0, $"{sensor.Address.ToUpper()} -> State: {sensorstate}");
                });

            await System.Threading.Tasks.Task.Delay(500);

            if (sensorstate == SimpleTransimission.Helpers.Services.BLEServices.ConnectionState.Connected)
                await sensor.StartStream();
        }
    }
}
