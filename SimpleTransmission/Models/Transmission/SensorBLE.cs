using SimpleTransimission.Helpers.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Timers;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace SimpleTransimission.Models.Transmission
{
    public class SensorBLE : INotifyPropertyChanged
    {
        #region UI Events
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Events Vars & Funtions
        public event EventHandler OnChangeState;
        public event EventHandler OnPacketReceived;
        public event EventHandler OnSensorDetected;
        public event EventHandler OnSensorMisaligned;
        public event EventHandler OnException;

        public void send_event(EventHandler event_handler, object args = null)
        {
            event_handler?.Invoke(args ?? (this), EventArgs.Empty);
        }

        private const int ConnectionTimeout = 60000;
        #endregion

        #region Status Vars
        private BLEServices.ConnectionState state;
        public BLEServices.ConnectionState State { get { return state; } set { state = value; OnPropertyChanged("State"); send_event(OnChangeState); } }

        private BLEServices.ConnectionState previousstate;
        public BLEServices.ConnectionState Previous_State { get { return previousstate; } set { previousstate = value; OnPropertyChanged("Previous_State"); } }
        #endregion

        #region Internal Vars
        public string Name { get; set; }
        public string Address { get; set; }
        #endregion

        public BluetoothLEDevice Device { get; set; }

        public GattCommunicationStatus _notificationsStatus;
        private byte[] _devicePassword = new byte[] { 0x04, 0x03, 0x02, 0x01 };

        #region Service gatt
        private GattDeviceService _gattDataSensorService;
        #endregion

        #region Characteristic gatt
        private GattCharacteristic _gattPasswordCharacteristic;
        private GattCharacteristic _gattDataSensorsCharacteristic;
        private GattCharacteristic _gattWriteStatusCharacteristic;

        private List<GattCharacteristic> _allGatt = new List<GattCharacteristic>();
        #endregion

        private List<byte> FullBuffer;

        private Timer ConnectTimeout;

        private BluetoothLEAdvertisementWatcher BleWatcher;

        #region Constructor & Init functions
        public SensorBLE(string mac)
        {
            Name = $"Sensor-{mac.Substring(mac.Length - 5)}";
            Address = mac;
            State = BLEServices.ConnectionState.Disconnected;
            Previous_State = BLEServices.ConnectionState.Disconnected;
            FullBuffer = new List<byte>();
        }
        #endregion

        #region Connection
        private void CancelDeviceWatcher_Added(object sender, ElapsedEventArgs e)
        {
            if (BleWatcher != null)
                if (BleWatcher.Status != BluetoothLEAdvertisementWatcherStatus.Stopped || BleWatcher.Status != BluetoothLEAdvertisementWatcherStatus.Stopping)
                    BleWatcher.Stop();

            ConnectTimeout.Stop();

            Previous_State = BLEServices.ConnectionState.Disconnected;
            State = BLEServices.ConnectionState.Disconnected;

            send_event(OnException, $"Connect timeout. Close and open again.");
        }

        private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
        }
        #endregion

        #region Basic Functions
        public void Connect()
        {
            Previous_State = State;
            State = BLEServices.ConnectionState.Connecting;

            ConnectTimeout = new Timer(ConnectionTimeout);
            ConnectTimeout.Elapsed += CancelDeviceWatcher_Added;
            ConnectTimeout.Start();

            BleWatcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };

            bool sensorfound = false;
            BleWatcher.Received += (w, btAdv) =>
            {
                BluetoothLEDevice device = null;

                try
                {
                    device = BluetoothLEDevice.FromBluetoothAddressAsync(btAdv.BluetoothAddress).GetAwaiter().GetResult();
                }
                catch (Exception exc)
                {
                    send_event(OnException, $"FromBluetoothAddressAsync crashed\n{PrintException(exc)}");
                    return;
                }

                if (device == null)
                    return;

                if (!device.Name.Equals("DYFORZE4"))
                    return;

                //Debug.WriteLine($"BLEWATCHER Found: {device.Name}  {device.ConnectionStatus}  {device.DeviceId} {device.DeviceInformation.Id}");
                send_event(OnSensorDetected, device.DeviceId.ToUpper().Substring(device.DeviceId.Length - 17));

                if (device.DeviceId.ToUpper().Substring(device.DeviceId.Length - 17).Equals(Address.ToUpper()) && !sensorfound)
                {
                    sensorfound = true;
                    ConnectTimeout.Elapsed -= CancelDeviceWatcher_Added;
                    ConnectTimeout.Stop();
                    w.Stop();

                    Previous_State = State;
                    State = BLEServices.ConnectionState.Connecting;

                    // SERVICES!!
                    GattDeviceServicesResult gatt = null;
                    try
                    {
                        gatt = device.GetGattServicesAsync().GetAwaiter().GetResult();
                    }
                    catch (Exception exc)
                    {
                        Previous_State = State;
                        State = BLEServices.ConnectionState.Disconnected;
                        send_event(OnException, $"GetGattServicesAsync crashed\n{PrintException(exc)}");
                        return;
                    }

                    if (gatt == null)
                    {
                        Previous_State = State;
                        State = BLEServices.ConnectionState.Disconnected;
                        send_event(OnException, "GetGattServicesAsync crashed somehow as it's still null");
                        return;
                    }

                    if (gatt.Services.Count == 0)
                    {
                        Previous_State = State;
                        State = BLEServices.ConnectionState.Disconnected;
                        send_event(OnException, "Sensor returns no services");
                        return;
                    }

                    foreach (GattDeviceService service in gatt.Services)
                    {
                        GattCharacteristicsResult characteristics = null;
                        try
                        {
                            characteristics = service.GetCharacteristicsAsync().GetAwaiter().GetResult();
                        }
                        catch (Exception exc)
                        {
                            Previous_State = State;
                            State = BLEServices.ConnectionState.Disconnected;
                            send_event(OnException, $"GetCharacteristicsAsync crashed\n{PrintException(exc)}");
                            return;
                        }

                        if (characteristics == null)
                        {
                            Previous_State = State;
                            State = BLEServices.ConnectionState.Disconnected;
                            send_event(OnException, "GetCharacteristicsAsync crashed somehow as it's still null");
                            return;
                        }

                        if (characteristics.Characteristics.Count == 0)
                        {
                            Previous_State = State;
                            State = BLEServices.ConnectionState.Disconnected;
                            send_event(OnException, $"Sensor returns no characteristics for service: {service.Uuid}");
                        }

                        _gattDataSensorService = service;
                        GattCharacteristic passwordcharacteristic = characteristics.Characteristics.FirstOrDefault(c => c.Uuid.ToString().ToLower().Equals(BLEServices.CHARACTERISTIC_PASSWORD.ToLower()));
                        if (passwordcharacteristic == null)
                            continue;

                        WriteData(passwordcharacteristic, _devicePassword).GetAwaiter();

                        foreach (var characteristic in characteristics.Characteristics)
                        {
                            if (characteristic.Uuid.ToString().ToLower().Equals(BLEServices.CHARACTERISTIC_PASSWORD.ToLower()))
                            {
                                _gattPasswordCharacteristic = characteristic;
                                WriteData(_gattPasswordCharacteristic, _devicePassword).GetAwaiter();
                            }

                            if (characteristic.Uuid.ToString().ToLower().Equals(BLEServices.CHARACTERISTIC_DATA_SENSORS.ToLower()))
                                _gattDataSensorsCharacteristic = characteristic;
                            else if (characteristic.Uuid.ToString().ToLower().Equals(BLEServices.CHARACTERISTIC_WRITE_STATUS.ToLower()))
                                _gattWriteStatusCharacteristic = characteristic;
                        }

                        bool nullcharacteristic = false;
                        if (_gattDataSensorsCharacteristic == null)
                        {
                            nullcharacteristic = true;
                            send_event(OnException, "_gattDataSensorsCharacteristic is null");
                        }

                        if (_gattWriteStatusCharacteristic == null)
                        {
                            nullcharacteristic = true;
                            send_event(OnException, "_gattWriteStatusCharacteristic is null");
                        }

                        if (nullcharacteristic)
                        {
                            Previous_State = State;
                            State = BLEServices.ConnectionState.Disconnected;
                            send_event(OnException, "Can't continue without characteristics");
                            return;
                        }

                        _allGatt.AddRange(characteristics.Characteristics);
                    }

                    if (_allGatt.Count == 0)
                    {
                        Disconnect();
                        return;
                    }

                    Previous_State = State;
                    State = BLEServices.ConnectionState.Connected;
                }
            };

            BleWatcher.Start();
        }

        public void Disconnect()
        {
            Previous_State = State;
            State = BLEServices.ConnectionState.Disconnected;

            ResetSensor();

            try
            {
                if (BleWatcher != null)
                    if (BleWatcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
                        BleWatcher.Stop();
            }
            catch { }

            _allGatt.Clear();
            try
            {
                _gattDataSensorService.Dispose();
                if (Device != null)
                    Device.Dispose();
                Device = null;
            }
            catch { }
        }

        public async Task StartStream()
        {
            SetStatusNotificationReceived(false);
            SetStatusNotificationReceived();

            _notificationsStatus = await _gattDataSensorsCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);

            Previous_State = State;
            State = BLEServices.ConnectionState.Streaming;
        }

        public async Task StopStream()
        {
            SetStatusNotificationReceived(false);

            _notificationsStatus =
                await _gattDataSensorsCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.None);

            Previous_State = State;
            State = BLEServices.ConnectionState.Connected;
        }
        #endregion

        #region WriteW&Read
        public async Task WriteData(GattCharacteristic gattChar, byte[] toSend)
        {
            if (gattChar == null)
            {
                send_event(OnException, $"Error writing data - NULL Characteristic");
                return;
            }

            IBuffer writer = toSend.AsBuffer();
            try
            {
                // BT_Code: Writes the value from the buffer to the characteristic.         
                var result = await gattChar.WriteValueAsync(writer);
                //if (result == GattCommunicationStatus.Success)
                //Debug.WriteLine($"Data written successfully: {toSend.Select(b => b.ToString()).ToList().ToString()}");
                //else
                //Debug.WriteLine("Error writing data");
            }
            catch (Exception exc)
            {
                send_event(OnException, $"Exception writing data\n{PrintException(exc)}");
            }
        }

        public async Task<byte[]> ReadData(GattCharacteristic gattChar)
        {
            byte[] input = new byte[0];
            GattReadResult result = await gattChar.ReadValueAsync();
            if (result.Status == GattCommunicationStatus.Success)
            {
                var reader = DataReader.FromBuffer(result.Value);
                input = new byte[reader.UnconsumedBufferLength];
                reader.ReadBytes(input);
            }

            return input;
        }
        #endregion

        #region Notification
        private void SetStatusNotificationReceived(bool put = true)
        {
            if (put)
                _gattDataSensorsCharacteristic.ValueChanged += NotificationReceived;
            else
                _gattDataSensorsCharacteristic.ValueChanged -= NotificationReceived;
        }

        private void NotificationReceived(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            DataReader dataReader = DataReader.FromBuffer(args.CharacteristicValue);
            var unread = dataReader.UnconsumedBufferLength;

            for (int i = 0; i < unread; i++)
                FullBuffer.Add(dataReader.ReadByte());

            if (FullBuffer.Count() % 45 == 0)
            {
                for (int i = 0; i < FullBuffer.Count() / 45; i++)
                {
                    double[] parsed = Helpers.Services.SensorDataAlgorithms.ParseBinaryData(FullBuffer.Skip(i * 45).Take(45).ToArray());
                    send_event(OnPacketReceived, parsed[0]);
                }
                FullBuffer.Clear();
            }
            else
            {
                send_event(OnSensorMisaligned);
                SetStatusNotificationReceived(false);
                Disconnect();
                return;
            }
        }
        #endregion

        #region Sensor Config
        public async void ResetSensor()
        {
            await WriteData(_gattWriteStatusCharacteristic, new byte[] { 0x01 });
        }

        #endregion

        public string PrintException(Exception exception)
        {
            var stringBuilder = new System.Text.StringBuilder();

            while (exception != null)
            {
                stringBuilder.AppendLine(exception.Message);
                stringBuilder.AppendLine(exception.StackTrace);

                exception = exception.InnerException;
            }

            return stringBuilder.ToString();
        }
    }
}
