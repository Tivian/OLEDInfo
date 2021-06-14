using System;

namespace OLEDInfo {
    interface IDataInterface : IDisposable {
        void Write(params byte[] data);
    }
}
