using System;
using System.Collections.Generic;
using System.Drawing;
using Microsoft.Lync.Model;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using Microsoft.Win32;

namespace StatusLamp
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.Run(new StatusSpammer());
        }

        private class StatusSpammer
            : Form
        {
            const int BroadcastPort = 43123;
            const int BroadcastInterval = 10000;
            const string AnimationFileName = "animations.json";

            readonly IPEndPoint _broadcastEndPoint;
            readonly NotifyIcon _notifyIcon;
            readonly ContextMenu _contextMenu;
            readonly Client _client;
            readonly System.Threading.Timer _broadcastTimer;
            readonly Dictionary<ContactAvailability, StateAnimation> _stateAnimations;

            ContactAvailability _currentAvailability;

            public StatusSpammer()
            {
                _broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, BroadcastPort);
                _notifyIcon = new NotifyIcon();
                _contextMenu = new ContextMenu();

                _contextMenu.MenuItems.Add(new MenuItem("Exit", (sender, args) => { Application.Exit(); }));
                _notifyIcon.Text = "Status Lamp";
                _notifyIcon.Icon = new Icon(SystemIcons.Exclamation, 40, 40);

                _notifyIcon.ContextMenu = _contextMenu;
                _notifyIcon.Visible = true;

                _stateAnimations = new Dictionary<ContactAvailability, StateAnimation>();
                var instances = Newtonsoft.Json.JsonConvert.DeserializeObject<StateAnimation[]>(System.IO.File.ReadAllText("animations.json"));
                foreach (var instance in instances)
                {
                    _stateAnimations.Add(instance.State, instance);
                }

                SystemEvents.PowerModeChanged += (sender, powerArgs) => { _currentAvailability = ContactAvailability.Offline; SendAvailability(); };

                _client = LyncClient.GetClient();
                _client.StateChanged += (sender, stateChangeArgs) => AvailabilityEventWireUp();

                _currentAvailability = FetchAvailability();

                _broadcastTimer = new System.Threading.Timer((_) => SendAvailability(), null, 0, 10000);

                // Wire for availability events
                AvailabilityEventWireUp();

                // Trigger an initial broadcast
                SendAvailability();
            }

            private void AvailabilityEventWireUp()
            {
                if (_client.State == ClientState.SignedIn)
                {
                    // Wire up events.
                    _client.Self.Contact.ContactInformationChanged += Contact_ContactInformationChanged;
                }

                _currentAvailability = FetchAvailability();
            }

            private void Contact_ContactInformationChanged(object sender, ContactInformationChangedEventArgs e)
            {
                if (_client.State != ClientState.SignedIn)
                {
                    return;
                }

                if (e.ChangedContactInformation.Contains(ContactInformationType.Availability))
                {
                    _currentAvailability = FetchAvailability();
                    SendAvailability();
                }
            }

            private ContactAvailability FetchAvailability()
            {
                if (_client.State != ClientState.SignedIn)
                {
                    return ContactAvailability.Offline;
                }

                return (ContactAvailability)_client.Self.Contact.GetContactInformation(ContactInformationType.Availability);
            }

            private void SendAvailability()
            {
                byte[] payload = _stateAnimations[_currentAvailability].AsPayload();

                using (var client = new UdpClient())
                {
                    client.Send(payload, payload.Length, _broadcastEndPoint);
                }
            }

            protected override void OnLoad(EventArgs e)
            {
                Visible = false;
                ShowInTaskbar = false;

                base.OnLoad(e);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _notifyIcon.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }

    public class StateAnimation
    {
        byte[] _payload = null;

        public ContactAvailability State { get; set; }
        public StateValue[] Values { get; set; }

        public byte[] AsPayload()
        {
            if (_payload == null && Values != null)
            {
                _payload = new byte[Values.Length * 5];

                for (int i = 0; i < Values.Length; i++)
                {
                    var payloadIndex = i * 5;
                    _payload[payloadIndex++] = Values[i].Pixel;
                    _payload[payloadIndex++] = Values[i].Red;
                    _payload[payloadIndex++] = Values[i].Green;
                    _payload[payloadIndex++] = Values[i].Blue;
                    _payload[payloadIndex] = Convert.ToByte(Values[i].Transition);
                }
            }

            return _payload ?? new byte[] { };
        }
    }

    public class StateValue
    {
        public byte Pixel { get; set; }
        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
        public bool Transition { get; set; }
    }
}
