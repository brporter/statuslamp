namespace StatusLamp
{
    using System;
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

            private byte[] AsPayload<T>(T e) where T : struct, IConvertible
            {
                if (!typeof(T).IsEnum)
                    throw new ArgumentException("e must be an enum type");

                var name = Enum.GetName(typeof(T), e);

                return System.Text.Encoding.UTF8.GetBytes(name);
            }
        }
    }
}
