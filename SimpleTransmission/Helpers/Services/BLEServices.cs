namespace SimpleTransimission.Helpers.Services
{
    public class BLEServices
    {
        #region Characteristics
        public static readonly string CHARACTERISTIC_DATA_SENSORS = "0000ff84-0000-1000-8000-00805f9b34fb";
        public static readonly string CHARACTERISTIC_WRITE_STATUS = "0000ff88-0000-1000-8000-00805f9b34fb";
        public static readonly string CHARACTERISTIC_PASSWORD = "0000ff81-0000-1000-8000-00805f9b34fb";
        #endregion

        #region Config
        public enum ConnectionState
        {
            Connected = 0,
            Disconnected = 1,
            Connecting = 2,
            Streaming = 3,
        }
        #endregion
    }
}
