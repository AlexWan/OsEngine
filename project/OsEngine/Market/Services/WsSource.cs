using System;
using System.Collections.Generic;
using SuperSocket.ClientEngine;
using WebSocket4Net;

namespace OsEngine.Market.Services
{
    public class WsSource
    {
        private WebSocket _wsClient;

        private WsSource() { }

        public WsSource(string uri)
        {
            _wsClient = new WebSocket(uri);

            _wsClient.AutoSendPingInterval = 3;
            _wsClient.EnableAutoSendPing = true;
            SubscribeEvents();
        }

        public WsSource(string uri, List<KeyValuePair<string, string>> headers)
        {
            _wsClient = new WebSocket(uri, customHeaderItems: headers);

            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            _wsClient.Opened += WsClientOnOpened;
            _wsClient.DataReceived += WsClientOnDataReceived;
            _wsClient.MessageReceived += WsClientOnMessageReceived;
            _wsClient.Error += WsClientOnError;
            _wsClient.Closed += WsClientOnClosed;
        }

        private void UnsubscribeEvents()
        {
            _wsClient.Opened -= WsClientOnOpened;
            _wsClient.DataReceived -= WsClientOnDataReceived;
            _wsClient.MessageReceived -= WsClientOnMessageReceived;
            _wsClient.Error -= WsClientOnError;
            _wsClient.Closed -= WsClientOnClosed;
        }

        private void WsClientOnOpened(object sender, EventArgs e)
        {
            MessageEvent?.Invoke(WsMessageType.Opened, "");
        }

        private void WsClientOnDataReceived(object sender, DataReceivedEventArgs e)
        {
            ByteDataEvent?.Invoke(WsMessageType.ByteData, e.Data);
        }

        private void WsClientOnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            MessageEvent?.Invoke(WsMessageType.StringData, e.Message);
        }

        private void WsClientOnError(object sender, ErrorEventArgs e)
        {
            MessageEvent?.Invoke(WsMessageType.Error, e.Exception.Message);
        }

        private void WsClientOnClosed(object sender, EventArgs e)
        {
            MessageEvent?.Invoke(WsMessageType.Closed, "");
        }

        public void Start()
        {
            _wsClient.Open();
        }

        private void Stop()
        {
            _wsClient.Close();
            UnsubscribeEvents();
        }

        public void Dispose()
        {
            Stop();
            _wsClient.Dispose();
            _wsClient = null;
            MessageEvent?.Invoke(WsMessageType.Closed, "");
        }

        public void SendMessage(string message)
        {
            _wsClient.Send(message);
        }

        public event Action<WsMessageType, string> MessageEvent;

        public event Action<WsMessageType, byte[]> ByteDataEvent;
    }

    public enum WsMessageType
    {
        Opened,
        Closed,
        StringData,
        ByteData,
        Error
    }
}