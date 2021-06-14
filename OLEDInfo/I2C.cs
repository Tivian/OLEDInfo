using System;
using I2CTinyUSB;

namespace OLEDInfo {
    class I2C : IDataInterface, IDisposable {
        public byte Address = 0x00;
        private I2CTiny tiny;

        public I2C() : this(0x00) { }

        public I2C(byte address) {
            Address = address;
            tiny = new I2CTiny();
            tiny.Connect();
        }

        public void Write(byte[] data) {
            tiny.Write(Address, data);
        }

        public void Dispose() {
            tiny.Dispose();
        }
    }
}
