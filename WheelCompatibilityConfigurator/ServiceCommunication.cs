using ServiceWire.TcpIp;
using System;
using System.Net;
using XboxWheelCompatibility.CommunicationInterface;

namespace WheelCompatibilityConfigurator
{
    internal class Communicator
    {
        private static readonly IPEndPoint Endpoint = new(IPAddress.Parse("127.0.0.1"), 16581);
        private TcpClient<IWheelCompatibilityService>? _client;
        private readonly object _clientLock = new();

        private TcpClient<IWheelCompatibilityService>? GetClient()
        {
            lock (_clientLock)
            {
                if (_client != null) return _client;

                try
                {
                    _client = new TcpClient<IWheelCompatibilityService>(new TcpEndPoint(Endpoint));
                    return _client;
                }
                catch
                {
                    _client = null;
                    return null;
                }
            }
        }

        private void Invalidate()
        {
            lock (_clientLock)
            {
                try { _client?.Dispose(); } catch { }
                _client = null;
            }
        }

        public int? TryGetMainWheelIndex()
        {
            var client = GetClient();
            if (client == null) return null;

            try
            {
                return client.Proxy.GetMainWheelIndex();
            }
            catch
            {
                Invalidate();
                return null;
            }
        }

        public double? TryGetSensitivity()
        {
            var client = GetClient();
            if (client == null) return null;

            try
            {
                return client.Proxy.GetSensitivity();
            }
            catch
            {
                Invalidate();
                return null;
            }
        }

        public bool TrySetSensitivity(double sensitivity)
        {
            var client = GetClient();
            if (client == null) return false;

            try
            {
                client.Proxy.SetSensitivity(sensitivity);
                return true;
            }
            catch
            {
                Invalidate();
                return false;
            }
        }

        public WheelReadingSnapshot? TryGetReadingSnapshot()
        {
            var client = GetClient();
            if (client == null) return null;

            try
            {
                return client.Proxy.GetReadingSnapshot();
            }
            catch
            {
                Invalidate();
                return null;
            }
        }

        public InjectionDiagnostics? TryGetInjectionDiagnostics()
        {
            var client = GetClient();
            if (client == null) return null;

            try
            {
                return client.Proxy.GetInjectionDiagnostics();
            }
            catch
            {
                Invalidate();
                return null;
            }
        }

        public DeviceStatus? TryGetDeviceStatus()
        {
            var client = GetClient();
            if (client == null) return null;

            try
            {
                return client.Proxy.GetDeviceStatus();
            }
            catch
            {
                Invalidate();
                return null;
            }
        }

        public bool TrySetDeadZone(double deadZone) => TryInvoke(p => p.SetDeadZone(deadZone));

        public bool TrySetSteeringRange(double range) => TryInvoke(p => p.SetSteeringRange(range));

        public double? TryGetSteeringRange()
        {
            var client = GetClient();
            if (client == null) return null;

            try
            {
                return client.Proxy.GetSteeringRange();
            }
            catch
            {
                Invalidate();
                return null;
            }
        }

        public double? TryGetDeadZone()
        {
            var client = GetClient();
            if (client == null) return null;

            try
            {
                return client.Proxy.GetDeadZone();
            }
            catch
            {
                Invalidate();
                return null;
            }
        }

        public bool TrySetDeviceMode(int mode) => TryInvoke(p => p.SetDeviceMode(mode));
        public bool TrySetSteeringAxis(int axis) => TryInvoke(p => p.SetSteeringAxis(axis));
        public bool TrySetInvertSteering(bool invert) => TryInvoke(p => p.SetInvertSteering(invert));

        private bool TryInvoke(Action<IWheelCompatibilityService> action)
        {
            var client = GetClient();
            if (client == null) return false;

            try
            {
                action(client.Proxy);
                return true;
            }
            catch
            {
                Invalidate();
                return false;
            }
        }
    }
}
