using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NavimowDesktopController
{
    internal sealed class MqttWsClient : IDisposable
    {
        private ClientWebSocket socket;
        private CancellationTokenSource cancellationTokenSource;
        private Task receiveTask;
        private Task pingTask;
        private int packetIdentifier;

        public event Action<MqttPublishMessage> PublishReceived;

        public bool IsConnected
        {
            get
            {
                return this.socket != null && this.socket.State == WebSocketState.Open;
            }
        }

        public async Task ConnectAsync(Uri uri, string username, string password, IDictionary<string, string> headers, string clientId)
        {
            await this.DisconnectAsync().ConfigureAwait(false);

            this.cancellationTokenSource = new CancellationTokenSource();
            this.socket = new ClientWebSocket();
            this.socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

            if (headers != null)
            {
                foreach (var pair in headers)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
                    {
                        this.socket.Options.SetRequestHeader(pair.Key, pair.Value);
                    }
                }
            }

            await this.socket.ConnectAsync(uri, this.cancellationTokenSource.Token).ConfigureAwait(false);

            var connectPacket = BuildConnectPacket(clientId, username, password);
            await this.socket.SendAsync(new ArraySegment<byte>(connectPacket), WebSocketMessageType.Binary, true, this.cancellationTokenSource.Token).ConfigureAwait(false);

            var connAck = await this.ReadSingleMessageAsync(this.cancellationTokenSource.Token).ConfigureAwait(false);
            ValidateConnAck(connAck);

            this.receiveTask = Task.Run(() => this.ReceiveLoopAsync(this.cancellationTokenSource.Token));
            this.pingTask = Task.Run(() => this.PingLoopAsync(this.cancellationTokenSource.Token));
        }

        public async Task SubscribeAsync(string topic)
        {
            if (!this.IsConnected)
            {
                throw new InvalidOperationException("MQTT ist nicht verbunden.");
            }

            unchecked
            {
                this.packetIdentifier++;
                if (this.packetIdentifier <= 0)
                {
                    this.packetIdentifier = 1;
                }
            }

            var packet = BuildSubscribePacket((ushort)this.packetIdentifier, topic);
            await this.socket.SendAsync(new ArraySegment<byte>(packet), WebSocketMessageType.Binary, true, this.cancellationTokenSource.Token).ConfigureAwait(false);
        }

        public async Task DisconnectAsync()
        {
            if (this.socket == null)
            {
                return;
            }

            try
            {
                if (this.socket.State == WebSocketState.Open)
                {
                    var disconnectPacket = new byte[] { 0xE0, 0x00 };
                    await this.socket.SendAsync(new ArraySegment<byte>(disconnectPacket), WebSocketMessageType.Binary, true, CancellationToken.None).ConfigureAwait(false);
                    await this.socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch
            {
            }

            try
            {
                if (this.cancellationTokenSource != null)
                {
                    this.cancellationTokenSource.Cancel();
                }
            }
            catch
            {
            }

            try
            {
                if (this.receiveTask != null)
                {
                    await this.receiveTask.ConfigureAwait(false);
                }
            }
            catch
            {
            }

            try
            {
                if (this.pingTask != null)
                {
                    await this.pingTask.ConfigureAwait(false);
                }
            }
            catch
            {
            }

            this.socket.Dispose();
            this.socket = null;

            if (this.cancellationTokenSource != null)
            {
                this.cancellationTokenSource.Dispose();
                this.cancellationTokenSource = null;
            }
        }

        public void Dispose()
        {
            this.DisconnectAsync().GetAwaiter().GetResult();
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new List<byte>();

            while (!cancellationToken.IsCancellationRequested && this.socket != null && this.socket.State == WebSocketState.Open)
            {
                byte[] message;

                try
                {
                    message = await this.ReadSingleMessageAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    return;
                }

                if (message == null || message.Length == 0)
                {
                    continue;
                }

                buffer.AddRange(message);
                this.ProcessBuffer(buffer);
            }
        }

        private async Task PingLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && this.socket != null && this.socket.State == WebSocketState.Open)
            {
                await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested || this.socket == null || this.socket.State != WebSocketState.Open)
                {
                    return;
                }

                var packet = new byte[] { 0xC0, 0x00 };
                await this.socket.SendAsync(new ArraySegment<byte>(packet), WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<byte[]> ReadSingleMessageAsync(CancellationToken cancellationToken)
        {
            var chunk = new byte[8192];

            using (var stream = new MemoryStream())
            {
                while (true)
                {
                    var result = await this.socket.ReceiveAsync(new ArraySegment<byte>(chunk), cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return null;
                    }

                    stream.Write(chunk, 0, result.Count);
                    if (result.EndOfMessage)
                    {
                        return stream.ToArray();
                    }
                }
            }
        }

        private void ProcessBuffer(List<byte> buffer)
        {
            int totalLength;
            int headerLength;

            while (TryReadPacketHeader(buffer, out totalLength, out headerLength))
            {
                var packet = buffer.GetRange(0, totalLength).ToArray();
                buffer.RemoveRange(0, totalLength);
                this.HandlePacket(packet, headerLength);
            }
        }

        private void HandlePacket(byte[] packet, int headerLength)
        {
            if (packet == null || packet.Length == 0)
            {
                return;
            }

            var packetType = (packet[0] >> 4) & 0x0F;
            if (packetType != 3)
            {
                return;
            }

            int offset = headerLength;
            if (packet.Length < offset + 2)
            {
                return;
            }

            var topicLength = (packet[offset] << 8) | packet[offset + 1];
            offset += 2;

            if (packet.Length < offset + topicLength)
            {
                return;
            }

            var topic = Encoding.UTF8.GetString(packet, offset, topicLength);
            offset += topicLength;

            var qos = (packet[0] >> 1) & 0x03;
            if (qos > 0)
            {
                offset += 2;
            }

            if (offset > packet.Length)
            {
                return;
            }

            var payload = Encoding.UTF8.GetString(packet, offset, packet.Length - offset);
            var handler = this.PublishReceived;
            if (handler != null)
            {
                handler(new MqttPublishMessage
                {
                    Topic = topic,
                    Payload = payload,
                });
            }
        }

        private static bool TryReadPacketHeader(List<byte> buffer, out int totalLength, out int headerLength)
        {
            totalLength = 0;
            headerLength = 0;

            if (buffer.Count < 2)
            {
                return false;
            }

            int multiplier = 1;
            int remainingLength = 0;
            int lengthBytes = 0;

            for (int i = 1; i < buffer.Count && i < 5; i++)
            {
                lengthBytes++;
                int encodedByte = buffer[i];
                remainingLength += (encodedByte & 127) * multiplier;
                if ((encodedByte & 128) == 0)
                {
                    headerLength = 1 + lengthBytes;
                    totalLength = headerLength + remainingLength;
                    return buffer.Count >= totalLength;
                }

                multiplier *= 128;
            }

            return false;
        }

        private static void ValidateConnAck(byte[] packet)
        {
            if (packet == null || packet.Length < 4)
            {
                throw new InvalidOperationException("Ungültige MQTT-CONNACK-Antwort.");
            }

            var packetType = (packet[0] >> 4) & 0x0F;
            if (packetType != 2)
            {
                throw new InvalidOperationException("MQTT-CONNACK wurde nicht empfangen.");
            }

            if (packet[3] != 0)
            {
                throw new InvalidOperationException("MQTT-Verbindung wurde vom Broker abgelehnt (Code " + packet[3] + ").");
            }
        }

        private static byte[] BuildConnectPacket(string clientId, string username, string password)
        {
            using (var payload = new MemoryStream())
            using (var writer = new BinaryWriter(payload))
            {
                WriteString(writer, "MQTT");
                writer.Write((byte)0x04);

                byte flags = 0x02;
                if (!string.IsNullOrWhiteSpace(username))
                {
                    flags |= 0x80;
                }

                if (!string.IsNullOrWhiteSpace(password))
                {
                    flags |= 0x40;
                }

                writer.Write(flags);
                WriteUInt16(writer, 60);
                WriteString(writer, clientId);

                if (!string.IsNullOrWhiteSpace(username))
                {
                    WriteString(writer, username);
                }

                if (!string.IsNullOrWhiteSpace(password))
                {
                    WriteString(writer, password);
                }

                return BuildPacket(0x10, payload.ToArray());
            }
        }

        private static byte[] BuildSubscribePacket(ushort packetId, string topic)
        {
            using (var payload = new MemoryStream())
            using (var writer = new BinaryWriter(payload))
            {
                WriteUInt16(writer, packetId);
                WriteString(writer, topic);
                writer.Write((byte)0x00);
                return BuildPacket(0x82, payload.ToArray());
            }
        }

        private static byte[] BuildPacket(byte fixedHeader, byte[] payload)
        {
            using (var stream = new MemoryStream())
            {
                stream.WriteByte(fixedHeader);
                var remainingLength = EncodeRemainingLength(payload.Length);
                stream.Write(remainingLength, 0, remainingLength.Length);
                stream.Write(payload, 0, payload.Length);
                return stream.ToArray();
            }
        }

        private static byte[] EncodeRemainingLength(int value)
        {
            var bytes = new List<byte>();
            do
            {
                byte encoded = (byte)(value % 128);
                value /= 128;
                if (value > 0)
                {
                    encoded = (byte)(encoded | 0x80);
                }

                bytes.Add(encoded);
            }
            while (value > 0);

            return bytes.ToArray();
        }

        private static void WriteUInt16(BinaryWriter writer, int value)
        {
            writer.Write((byte)((value >> 8) & 0xFF));
            writer.Write((byte)(value & 0xFF));
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            WriteUInt16(writer, bytes.Length);
            writer.Write(bytes);
        }
    }
}
