using System;

namespace SimpleTransimission.Helpers.Services
{
    public static class SensorDataAlgorithms
    {
        public static double[] ParseBinaryData(byte[] data)
        {
            var Accel_LSB_X_IMU = data[7];
            var Accel_MSB_X_IMU = data[8];
            var Accel_LSB_Y_IMU = data[9];
            var Accel_MSB_Y_IMU = data[10];
            var Accel_LSB_Z_IMU = data[11];
            var Accel_MSB_Z_IMU = data[12];

            var accelX = GetSignedDecimal(Accel_LSB_X_IMU, Accel_MSB_X_IMU);
            var accelY = GetSignedDecimal(Accel_LSB_Y_IMU, Accel_MSB_Y_IMU);
            var accelZ = GetSignedDecimal(Accel_LSB_Z_IMU, Accel_MSB_Z_IMU);

            return new double[3] {
                accelX,
                accelY,
                accelZ,
            };
        }

        public static double GetSignedDecimal(byte byte1, byte byte2)
        {
            return BitConverter.ToInt16(new byte[] { byte1, byte2 }, 0);
        }
    }
}
