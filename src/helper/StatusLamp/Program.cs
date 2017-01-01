namespace StatusLamp
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using Microsoft.Lync.Model;
    using System.Net;
    using System.Net.Sockets;
    using System.Windows.Forms;
    using Microsoft.Win32;

    static class Program
    {
        const int BroadcastPort = 43123;
        const int OperationInterval = 10000;

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
            readonly Dictionary<ContactAvailability, byte[]> _colorMap = new Dictionary<ContactAvailability, byte[]>()
            {
                { ContactAvailability.Away, new byte[] { Color.Yellow.R, Color.Yellow.G, Color.Yellow.B } },
                { ContactAvailability.TemporarilyAway,  new byte[] { Color.Yellow.R, Color.Yellow.G, Color.Yellow.B }  },
                { ContactAvailability.Busy,  new byte[] { Color.Red.R, Color.Red.G, Color.Red.B }  },
                { ContactAvailability.DoNotDisturb,  new byte[] { Color.Purple.R, Color.Purple.G, Color.Purple.B }  },
                { ContactAvailability.Free,  new byte[] { Color.Green.R, Color.Green.G, Color.Green.B }  },
                { ContactAvailability.Offline,  new byte[] { Color.Black.R, Color.Black.G, Color.Black.B }  }
            };

            readonly IPEndPoint _broadcastEndPoint;
            readonly NotifyIcon _notifyIcon;
            readonly ContextMenu _contextMenu;

            Client _client;
            System.Threading.Timer _broadcastTimer;
            Timer _initializationTimer;

            ContactAvailability _currentAvailability;

            public StatusSpammer()
            {
                SystemEvents.PowerModeChanged += (sender, powerArgs) => { _currentAvailability = ContactAvailability.Offline; SendAvailability(); };

                _currentAvailability = ContactAvailability.Offline;

                _broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, BroadcastPort);
                _notifyIcon = new NotifyIcon();
                _contextMenu = new ContextMenu();

                _contextMenu.MenuItems.Add(new MenuItem("Exit", (sender, args) => { Application.Exit(); }));
                _notifyIcon.Text = "Status Lamp";
                _notifyIcon.Icon = new Icon(SystemIcons.Exclamation, 40, 40);

                _notifyIcon.ContextMenu = _contextMenu;
                _notifyIcon.Visible = true;

                _broadcastTimer = new System.Threading.Timer((_) => SendAvailability(), null, 0, OperationInterval);

                _initializationTimer = new Timer();
                _initializationTimer.Tick += (sender, e) => InitializeLyncClient();
                _initializationTimer.Interval = OperationInterval; // 10 seconds
                _initializationTimer.Start();
            }

            private void InitializeLyncClient()
            {
                if (_client != null && _client.State != ClientState.Invalid)
                    return;

                this.Invoke(new Action(() => {
                    try
                    {

                        _client = LyncClient.GetClient();
                        _client.StateChanged += (sender, stateChangeArgs) => AvailabilityEventWireUp();

                        _currentAvailability = FetchAvailability();

                        // Wire for availability events
                        AvailabilityEventWireUp();

                        // Trigger an initial broadcast
                        SendAvailability();
                    }
                    catch (ClientNotFoundException)
                    { }
                }));
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
                byte[] payload = AsPayload(_currentAvailability);

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

            private byte[] AsPayload(ContactAvailability ca)
            {
                return _colorMap[ca];
            }
        }
    }
}
